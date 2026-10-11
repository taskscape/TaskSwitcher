using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    /// <summary>
    /// Provides the icons shown in the window list as frozen images that can be created on,
    /// and handed over from, a background thread.
    /// </summary>
    internal static class WindowIconImageLoader
    {
        private static readonly IconToBitmapImageConverter IconToBitmapConverter = new();
        private static readonly IconCacheService Cache = IconCacheService.Instance;
        private static readonly object LoadLock = new();

        /// <summary>
        /// Returns the icon of the window if it has been loaded before, otherwise null.
        /// This never queries the window, so it is cheap.
        /// </summary>
        internal static ImageSource GetCached(AppWindow window)
        {
            return Cache.GetBitmapImage(window.CacheIdentity, TaskbarIconSizeProvider.GetPreferredIconSize());
        }

        /// <summary>
        /// Returns the icon of the window, retrieving it from the window when necessary.
        /// </summary>
        /// <remarks>
        /// Retrieving an icon sends a message to the window and waits for the reply, which takes
        /// noticeable time for a busy application. Never call this on the UI thread.
        /// </remarks>
        internal static ImageSource Load(AppWindow window)
        {
            WindowIconSize iconSize = TaskbarIconSizeProvider.GetPreferredIconSize();
            WindowCacheIdentity identity = window.CacheIdentity;

            // One icon at a time: GDI+ objects are not safe for concurrent use, and it prevents
            // two overlapping loads from retrieving the same icon twice.
            lock (LoadLock)
            {
                if (Cache.GetBitmapImage(identity, iconSize) is { } cachedImage)
                {
                    return cachedImage;
                }

                Icon icon = iconSize == WindowIconSize.Small ? window.SmallWindowIcon : window.LargeWindowIcon;
                BitmapImage iconImage = IconToBitmapConverter.Convert(icon);
                if (iconImage == null)
                {
                    // Cached as well, so that a window without an icon is not asked again.
                    iconImage = new BitmapImage();
                    iconImage.Freeze();
                }

                Cache.SetBitmapImage(identity, iconSize, iconImage);

                return iconImage;
            }
        }
    }
}
