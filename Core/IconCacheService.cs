using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Caching.Memory;

[assembly: InternalsVisibleTo("TaskSwitcher.Core.UnitTests")]

namespace TaskSwitcher.Core
{
    /// <summary>
    /// Provides centralized caching for window icons to avoid redundant memory usage
    /// across different components.
    /// </summary>
    public sealed class IconCacheService : IIconCacheService, IDisposable
    {
        private const string CacheName = "UnifiedWindowIconCache";
        
        private static readonly Lazy<IconCacheService> LazyInstance = new(() => new IconCacheService());
        
        /// <summary>
        /// Gets the singleton instance. For new code, prefer dependency injection with <see cref="IIconCacheService"/>.
        /// </summary>
        public static IconCacheService Instance => LazyInstance.Value;

        private volatile MemoryCache _iconCache;
        private readonly TimeProvider _timeProvider;
        private readonly Lock _getOrSetLock = new();
        private bool _disposed;
        
        // Cache configuration
        private static readonly TimeSpan LongCacheDuration = TimeSpan.FromMinutes(10);

        private IconCacheService() : this(TimeProvider.System)
        {
        }

        /// <summary>
        /// Constructor for dependency injection with default time provider.
        /// </summary>
        /// <param name="memoryCache">Optional pre-configured memory cache. If null, a new cache is created.</param>
        public IconCacheService(MemoryCache memoryCache) : this(memoryCache, TimeProvider.System)
        {
        }

        /// <summary>
        /// Constructor for testing or DI with a custom TimeProvider.
        /// </summary>
        /// <param name="timeProvider">The time provider to use for cache expiration calculations.</param>
        internal IconCacheService(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            _iconCache = CreateCache();
        }

        /// <summary>
        /// Constructor for full dependency injection support.
        /// </summary>
        /// <param name="memoryCache">Optional pre-configured memory cache. If null, a new cache is created.</param>
        /// <param name="timeProvider">The time provider to use for cache expiration calculations.</param>
        internal IconCacheService(MemoryCache memoryCache, TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            _iconCache = memoryCache ?? CreateCache();
        }

        private static MemoryCache CreateCache()
        {
            return new MemoryCache(new MemoryCacheOptions { TrackStatistics = false });
        }

        /// <summary>
        /// Gets a cached Icon by window lifetime identity and size, or null if not cached.
        /// </summary>
        public Icon GetIcon(WindowCacheIdentity windowIdentity, WindowIconSize size)
        {
            var cache = _iconCache;
            string cacheKey = BuildIconCacheKey(windowIdentity, size);
            return cache.Get<Icon>(cacheKey);
        }

        /// <summary>
        /// Caches an Icon by window lifetime identity and size.
        /// </summary>
        public void SetIcon(WindowCacheIdentity windowIdentity, WindowIconSize size, Icon icon)
        {
            if (icon == null) return;

            var cache = _iconCache;
            string cacheKey = BuildIconCacheKey(windowIdentity, size);
            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = LongCacheDuration
            };
            options.RegisterPostEvictionCallback(DisposeIconOnRemoval);
            cache.Set(cacheKey, icon, options);
        }

        /// <summary>
        /// Callback to dispose Icon resources when removed from cache.
        /// </summary>
        private static void DisposeIconOnRemoval(object key, object value, EvictionReason reason, object state)
        {
            (value as Icon)?.Dispose();
        }

        /// <summary>
        /// Callback to dispose resources when removed from cache if they implement IDisposable.
        /// </summary>
        private static void DisposeOnRemoval(object key, object value, EvictionReason reason, object state)
        {
            (value as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Gets a cached BitmapSource by window lifetime identity and size, or null if not cached.
        /// </summary>
        public BitmapSource GetBitmapImage(WindowCacheIdentity windowIdentity, WindowIconSize size)
        {
            var cache = _iconCache;
            string cacheKey = BuildBitmapCacheKey(windowIdentity, size);
            return cache.Get<BitmapSource>(cacheKey);
        }

        /// <summary>
        /// Caches a BitmapSource by window handle and size with sliding expiration.
        /// </summary>
        public void SetBitmapImage(WindowCacheIdentity windowIdentity, WindowIconSize size, BitmapSource bitmapImage)
        {
            if (bitmapImage == null) return;

            var cache = _iconCache;
            string cacheKey = BuildBitmapCacheKey(windowIdentity, size);
            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = LongCacheDuration
            };
            options.RegisterPostEvictionCallback(DisposeOnRemoval);
            cache.Set(cacheKey, bitmapImage, options);
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
            if (cache.TryGetValue(key, out T cached))
            {
                return cached;
            }

            lock (_getOrSetLock)
            {
                // Double-check after acquiring lock to avoid redundant factory calls
                if (cache.TryGetValue(key, out T cachedAgain))
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
            return cache.TryGetValue(key, out value);
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
            var newCache = CreateCache();
            var oldCache = Interlocked.Exchange(ref _iconCache, newCache);
            oldCache.Dispose();
        }

        /// <summary>
        /// Disposes the cache and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _iconCache?.Dispose();
        }

        private static string BuildIconCacheKey(WindowCacheIdentity windowIdentity, WindowIconSize size)
        {
            const string Prefix = "Icon-";
            return BuildCacheKey(Prefix, windowIdentity, size);
        }

        private static string BuildBitmapCacheKey(WindowCacheIdentity windowIdentity, WindowIconSize size)
        {
            const string Prefix = "BitmapImage-";
            return BuildCacheKey(Prefix, windowIdentity, size);
        }

        internal static string BuildCacheKey(
            string prefix,
            WindowCacheIdentity windowIdentity,
            WindowIconSize size)
        {
            return $"{windowIdentity.BuildCacheKey(prefix)}-{GetSizeString(size)}";
        }

        private static string GetSizeString(WindowIconSize size) => size switch
        {
            WindowIconSize.Small => "Small",
            WindowIconSize.Large => "Large",
            _ => size.ToString()
        };
    }
}
