using System;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace TaskSwitcher.Core
{
    /// <summary>
    /// Provides centralized caching for window icons to avoid redundant memory usage
    /// across different components.
    /// </summary>
    public interface IIconCacheService
    {
        /// <summary>
        /// Gets a cached Icon by window lifetime identity and size, or null if not cached.
        /// </summary>
        Icon GetIcon(WindowCacheIdentity windowIdentity, WindowIconSize size);

        /// <summary>
        /// Caches an Icon by window lifetime identity and size.
        /// </summary>
        void SetIcon(WindowCacheIdentity windowIdentity, WindowIconSize size, Icon icon);

        /// <summary>
        /// Gets a cached BitmapSource by window lifetime identity and size, or null if not cached.
        /// </summary>
        BitmapSource GetBitmapImage(WindowCacheIdentity windowIdentity, WindowIconSize size);

        /// <summary>
        /// Caches a BitmapSource by window handle and size with sliding expiration.
        /// </summary>
        void SetBitmapImage(WindowCacheIdentity windowIdentity, WindowIconSize size, BitmapSource bitmapImage);

        /// <summary>
        /// Gets or sets a generic cached value by key.
        /// Uses double-checked locking to ensure thread-safe initialization.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
        T GetOrSet<T>(string key, Func<T> factory, TimeSpan expiration) where T : class;

        /// <summary>
        /// Gets a cached value by key, or default if not found.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
        bool TryGetValue<T>(string key, out T value);

        /// <summary>
        /// Sets a cached reference type value by key with the specified expiration.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
        void Set<T>(string key, T value, TimeSpan expiration) where T : class;

        /// <summary>
        /// Sets a cached value type by key with the specified expiration.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key is null or whitespace.</exception>
        void SetValue<T>(string key, T value, TimeSpan expiration) where T : struct;

        /// <summary>
        /// Clears all cached icons.
        /// </summary>
        void Clear();
    }
}
