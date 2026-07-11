using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    public class WindowHandleToCachedIconConverter : IValueConverter
    {
        private readonly IconCacheService _cache = IconCacheService.Instance;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not IntPtr handle || handle == IntPtr.Zero)
            {
                return DependencyProperty.UnsetValue;
            }

            AppWindow window = new(handle);
            WindowIconSize iconSize = TaskbarIconSizeProvider.GetPreferredIconSize();
            ImageSource cachedImage = _cache.GetBitmapImage(window.CacheIdentity, iconSize);
            return cachedImage ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
