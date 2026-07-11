using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    public class WindowHandleToIconConverter : IValueConverter
    {
        private readonly IconToBitmapImageConverter _iconToBitmapConverter;
        private readonly IconCacheService _cache = IconCacheService.Instance;

        public WindowHandleToIconConverter()
        {
            _iconToBitmapConverter = new IconToBitmapImageConverter();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IntPtr handle = (IntPtr)value;
            WindowIconSize iconSize = TaskbarIconSizeProvider.GetPreferredIconSize();
            AppWindow window = new(handle);

            // Try to get from unified cache first
            if (_cache.GetBitmapImage(window.CacheIdentity, iconSize) is BitmapImage cachedImage)
            {
                return cachedImage;
            }

            // Create new icon and cache it
            Icon icon = iconSize == WindowIconSize.Small ? window.SmallWindowIcon : window.LargeWindowIcon;
            BitmapImage iconImage = _iconToBitmapConverter.Convert(icon);
            if (iconImage == null)
            {
                iconImage = new BitmapImage();
                iconImage.Freeze();
            }

            _cache.SetBitmapImage(window.CacheIdentity, iconSize, iconImage);

            return iconImage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
