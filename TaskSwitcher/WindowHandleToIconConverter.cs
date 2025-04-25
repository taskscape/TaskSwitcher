using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.Caching;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    public class WindowHandleToIconConverter : IValueConverter
    {
        private readonly IconToBitmapImageConverter _iconToBitmapConverter;

        public WindowHandleToIconConverter()
        {
            _iconToBitmapConverter = new IconToBitmapImageConverter();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IntPtr handle = (IntPtr) value;
            string key = "IconImage-" + handle;
            string shortCacheKey = key + "-shortCache";
            string longCacheKey = key + "-longCache";
            if (MemoryCache.Default.Get(shortCacheKey) is BitmapImage iconImage) return iconImage;
            AppWindow window = new(handle);
            Icon icon = ShouldUseSmallTaskbarIcons() ? window.SmallWindowIcon : window.LargeWindowIcon;
            iconImage = _iconToBitmapConverter.Convert(icon) ?? new BitmapImage();
            MemoryCache.Default.Set(shortCacheKey, iconImage, DateTimeOffset.Now.AddSeconds(5));
            MemoryCache.Default.Set(longCacheKey, iconImage, DateTimeOffset.Now.AddMinutes(120));

            return iconImage;
        }

        private static bool ShouldUseSmallTaskbarIcons()
        {
            string cacheKey = "SmallTaskbarIcons";

            if (MemoryCache.Default.Get(cacheKey) is bool cachedSetting)
            {
                return cachedSetting;
            }

            using (
                RegistryKey registryKey =
                    Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
            {
                object value = registryKey?.GetValue("TaskbarSmallIcons");
                if (value == null)
                {
                    return false;
                }

                int.TryParse(value.ToString(), out int intValue);
                bool smallTaskbarIcons = intValue == 1;
                MemoryCache.Default.Set(cacheKey, smallTaskbarIcons, DateTimeOffset.Now.AddMinutes(120));
                return smallTaskbarIcons;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}