using System;
using System.Drawing;
using System.Runtime.Caching;

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

        private readonly MemoryCache _iconCache;
        
        // Cache configuration
        private static readonly TimeSpan ShortCacheDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LongCacheDuration = TimeSpan.FromMinutes(10);

        private IconCacheService()
        {
            _iconCache = new MemoryCache("UnifiedWindowIconCache");
        }

        /// <summary>
        /// Gets a cached Icon by window handle and size, or null if not cached.
        /// </summary>
        public Icon GetIcon(IntPtr windowHandle, WindowIconSize size)
        {
            string cacheKey = BuildIconCacheKey(windowHandle, size);
            return _iconCache.Get(cacheKey) as Icon;
        }

        /// <summary>
        /// Caches an Icon by window handle and size.
        /// </summary>
        public void SetIcon(IntPtr windowHandle, WindowIconSize size, Icon icon)
        {
            if (icon == null) return;

            string cacheKey = BuildIconCacheKey(windowHandle, size);
            var policy = new CacheItemPolicy
            {
                SlidingExpiration = LongCacheDuration
            };
            _iconCache.Set(cacheKey, icon, policy);
        }

        /// <summary>
        /// Gets a cached BitmapImage by window handle and size, or null if not cached.
        /// </summary>
        public object GetBitmapImage(IntPtr windowHandle, WindowIconSize size)
        {
            string cacheKey = BuildBitmapCacheKey(windowHandle, size);
            return _iconCache.Get(cacheKey);
        }

        /// <summary>
        /// Caches a BitmapImage by window handle and size with short and long expiration.
        /// </summary>
        public void SetBitmapImage(IntPtr windowHandle, WindowIconSize size, object bitmapImage)
        {
            if (bitmapImage == null) return;

            string shortCacheKey = BuildBitmapCacheKey(windowHandle, size);
            string longCacheKey = shortCacheKey + "-long";

            _iconCache.Set(shortCacheKey, bitmapImage, DateTimeOffset.Now.Add(ShortCacheDuration));
            _iconCache.Set(longCacheKey, bitmapImage, DateTimeOffset.Now.Add(LongCacheDuration));
        }

        /// <summary>
        /// Gets or sets a generic cached value by key.
        /// </summary>
        public T GetOrSet<T>(string key, Func<T> factory, TimeSpan expiration) where T : class
        {
            if (_iconCache.Get(key) is T cached)
            {
                return cached;
            }

            T value = factory();
            if (value != null)
            {
                _iconCache.Set(key, value, DateTimeOffset.Now.Add(expiration));
            }
            return value;
        }

        /// <summary>
        /// Gets a cached value by key, or default if not found.
        /// </summary>
        public bool TryGetValue<T>(string key, out T value)
        {
            var cached = _iconCache.Get(key);
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
        public void Set<T>(string key, T value, TimeSpan expiration) where T : class
        {
            if (value == null) return;
            _iconCache.Set(key, value, DateTimeOffset.Now.Add(expiration));
        }

        /// <summary>
        /// Sets a cached value type by key with the specified expiration.
        /// </summary>
        public void SetValue<T>(string key, T value, TimeSpan expiration) where T : struct
        {
            _iconCache.Set(key, value, DateTimeOffset.Now.Add(expiration));
        }

        /// <summary>
        /// Clears all cached icons.
        /// </summary>
        public void Clear()
        {
            _iconCache.Dispose();
        }

        private static string BuildIconCacheKey(IntPtr windowHandle, WindowIconSize size) 
            => $"Icon-{windowHandle}-{size}";

        private static string BuildBitmapCacheKey(IntPtr windowHandle, WindowIconSize size) 
            => $"BitmapImage-{windowHandle}-{size}";
    }
}