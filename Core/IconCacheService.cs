using System;
using System.Drawing;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("Core.UnitTests")]

namespace TaskSwitcher.Core
{
    /// <summary>
    /// Provides centralized caching for window icons to avoid redundant memory usage
    /// across different components.
    /// </summary>
    public sealed class IconCacheService
    {
        private static readonly Lazy<IconCacheService> LazyInstance = new(() => new IconCacheService());
        
        public static IconCacheService Instance => LazyInstance.Value;

        private volatile MemoryCache _iconCache;
        private readonly TimeProvider _timeProvider;
        private readonly Lock _getOrSetLock = new();
        
        // Cache configuration
        private static readonly TimeSpan ShortCacheDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LongCacheDuration = TimeSpan.FromMinutes(10);

        private IconCacheService() : this(TimeProvider.System)
        {
        }

        /// <summary>
        /// Constructor for testing with a custom TimeProvider.
        /// </summary>
        /// <param name="timeProvider">The time provider to use for cache expiration calculations.</param>
        internal IconCacheService(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            _iconCache = new MemoryCache("UnifiedWindowIconCache");
        }

        /// <summary>
        /// Gets a cached Icon by window handle and size, or null if not cached.
        /// </summary>
        public Icon GetIcon(IntPtr windowHandle, WindowIconSize size)
        {
            var cache = _iconCache;
            string cacheKey = BuildIconCacheKey(windowHandle, size);
            return cache.Get(cacheKey) as Icon;
        }

        /// <summary>
        /// Caches an Icon by window handle and size.
        /// </summary>
        public void SetIcon(IntPtr windowHandle, WindowIconSize size, Icon icon)
        {
            if (icon == null) return;

            var cache = _iconCache;
            string cacheKey = BuildIconCacheKey(windowHandle, size);
            var policy = new CacheItemPolicy
            {
                SlidingExpiration = LongCacheDuration,
                RemovedCallback = DisposeIconOnRemoval
            };
            cache.Set(cacheKey, icon, policy);
        }

        /// <summary>
        /// Callback to dispose Icon resources when removed from cache.
        /// </summary>
        private static void DisposeIconOnRemoval(CacheEntryRemovedArguments args)
        {
            (args.CacheItem.Value as Icon)?.Dispose();
        }

        /// <summary>
        /// Callback to dispose resources when removed from cache if they implement IDisposable.
        /// </summary>
        private static void DisposeOnRemoval(CacheEntryRemovedArguments args)
        {
            (args.CacheItem.Value as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Gets a cached BitmapImage by window handle and size, or null if not cached.
        /// </summary>
        public object GetBitmapImage(IntPtr windowHandle, WindowIconSize size)
        {
            var cache = _iconCache;
            string cacheKey = BuildBitmapCacheKey(windowHandle, size);
            return cache.Get(cacheKey);
        }

        /// <summary>
        /// Caches a BitmapImage by window handle and size with sliding expiration.
        /// </summary>
        public void SetBitmapImage(IntPtr windowHandle, WindowIconSize size, object bitmapImage)
        {
            if (bitmapImage == null) return;

            var cache = _iconCache;
            string cacheKey = BuildBitmapCacheKey(windowHandle, size);
            var policy = new CacheItemPolicy
            {
                SlidingExpiration = LongCacheDuration,
                RemovedCallback = DisposeOnRemoval
            };
            cache.Set(cacheKey, bitmapImage, policy);
        }

        /// <summary>
        /// Gets or sets a generic cached value by key.
        /// Uses double-checked locking to ensure thread-safe initialization.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
        public T GetOrSet<T>(string key, Func<T> factory, TimeSpan expiration) where T : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var cache = _iconCache;
            if (cache.Get(key) is T cached)
            {
                return cached;
            }

            lock (_getOrSetLock)
            {
                // Double-check after acquiring lock to avoid redundant factory calls
                if (cache.Get(key) is T cachedAgain)
                {
                    return cachedAgain;
                }

                T value = factory();
                if (value != null)
                {
                    cache.Set(key, value, _timeProvider.GetUtcNow().Add(expiration));
                }
                return value;
            }
        }

        /// <summary>
        /// Gets a cached value by key, or default if not found.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
        public bool TryGetValue<T>(string key, out T value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var cache = _iconCache;
            var cached = cache.Get(key);
            if (cached is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Sets a cached reference type value by key with the specified expiration.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
        public void Set<T>(string key, T value, TimeSpan expiration) where T : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (value == null) return;

            var cache = _iconCache;
            cache.Set(key, value, _timeProvider.GetUtcNow().Add(expiration));
        }

        /// <summary>
        /// Sets a cached value type by key with the specified expiration.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
        public void SetValue<T>(string key, T value, TimeSpan expiration) where T : struct
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var cache = _iconCache;
            cache.Set(key, value, _timeProvider.GetUtcNow().Add(expiration));
        }

        /// <summary>
        /// Clears all cached icons and recreates the cache.
        /// </summary>
        public void Clear()
        {
            var newCache = new MemoryCache("UnifiedWindowIconCache");
            var oldCache = Interlocked.Exchange(ref _iconCache, newCache);
            oldCache.Dispose();
        }

        private static string BuildIconCacheKey(IntPtr windowHandle, WindowIconSize size)
        {
            const string Prefix = "Icon-";
            return BuildCacheKey(Prefix, windowHandle, size);
        }

        private static string BuildBitmapCacheKey(IntPtr windowHandle, WindowIconSize size)
        {
            const string Prefix = "BitmapImage-";
            return BuildCacheKey(Prefix, windowHandle, size);
        }

        private static string BuildCacheKey(string prefix, IntPtr windowHandle, WindowIconSize size)
        {
            var sizeString = GetSizeString(size);
            var handle = (nint)windowHandle;

            return string.Create(
                prefix.Length + 20 + 1 + sizeString.Length, // 20 = max nint digits, 1 = separator
                (prefix, handle, sizeString),
                static (span, state) =>
                {
                    state.prefix.AsSpan().CopyTo(span);
                    int pos = state.prefix.Length;

                    state.handle.TryFormat(span[pos..], out int written);
                    pos += written;

                    span[pos++] = '-';

                    state.sizeString.AsSpan().CopyTo(span[pos..]);
                    pos += state.sizeString.Length;

                    // Truncate to actual length (string.Create handles this via the span)
                });
        }

        private static string GetSizeString(WindowIconSize size) => size switch
        {
            WindowIconSize.Small => "Small",
            WindowIconSize.Large => "Large",
            _ => size.ToString()
        };
    }
}