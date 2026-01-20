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
        /// Gets a cached BitmapImage by window handle and size, or null if not cached.
        /// Falls back to long-lived cache if short-lived cache has expired.
        /// </summary>
        public object GetBitmapImage(IntPtr windowHandle, WindowIconSize size)
        {
            var cache = _iconCache;
            string shortCacheKey = BuildBitmapCacheKey(windowHandle, size);
            var cached = cache.Get(shortCacheKey);
            
            if (cached != null)
            {
                return cached;
            }

            // Fallback to long-lived cache
            string longCacheKey = shortCacheKey + "-long";
            return cache.Get(longCacheKey);
        }

        /// <summary>
        /// Caches a BitmapImage by window handle and size with short and long expiration.
        /// </summary>
        public void SetBitmapImage(IntPtr windowHandle, WindowIconSize size, object bitmapImage)
        {
            if (bitmapImage == null) return;

            var cache = _iconCache;
            string shortCacheKey = BuildBitmapCacheKey(windowHandle, size);
            string longCacheKey = shortCacheKey + "-long";

            DateTimeOffset now = _timeProvider.GetUtcNow();
            cache.Set(shortCacheKey, bitmapImage, now.Add(ShortCacheDuration));
            cache.Set(longCacheKey, bitmapImage, now.Add(LongCacheDuration));
        }

        /// <summary>
        /// Gets or sets a generic cached value by key.
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

            T value = factory();
            if (value != null)
            {
                cache.Set(key, value, _timeProvider.GetUtcNow().Add(expiration));
            }
            return value;
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
            => $"Icon-{windowHandle}-{size}";

        private static string BuildBitmapCacheKey(IntPtr windowHandle, WindowIconSize size) 
            => $"BitmapImage-{windowHandle}-{size}";
    }
}