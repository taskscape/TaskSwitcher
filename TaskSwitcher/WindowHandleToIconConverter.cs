using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    public class WindowHandleToIconConverter : IValueConverter
    {
        private static readonly TimeSpan SettingsCacheDuration = TimeSpan.FromMinutes(120);
        private const string SmallTaskbarIconsCacheKey = "SmallTaskbarIcons";

        private readonly IconToBitmapImageConverter _iconToBitmapConverter;
        private readonly IconCacheService _cache = IconCacheService.Instance;

        public WindowHandleToIconConverter()
        {
            _iconToBitmapConverter = new IconToBitmapImageConverter();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IntPtr handle = (IntPtr)value;
            WindowIconSize iconSize = ShouldUseSmallTaskbarIcons() ? WindowIconSize.Small : WindowIconSize.Large;

            // Try to get from unified cache first
            if (_cache.GetBitmapImage(handle, iconSize) is BitmapImage cachedImage)
            {
                return cachedImage;
            }

            // Create new icon and cache it
            AppWindow window = new AppWindow(handle);
            Icon icon = iconSize == WindowIconSize.Small ? window.SmallWindowIcon : window.LargeWindowIcon;
            BitmapImage iconImage = _iconToBitmapConverter.Convert(icon) ?? new BitmapImage();

            _cache.SetBitmapImage(handle, iconSize, iconImage);

            return iconImage;
        }

        private bool ShouldUseSmallTaskbarIcons()
        {
            if (_cache.TryGetValue<bool>(SmallTaskbarIconsCacheKey, out bool cachedSetting))
            {
                return cachedSetting;
            }

            bool smallTaskbarIcons = ReadSmallTaskbarIconsSetting();
            _cache.SetValue(SmallTaskbarIconsCacheKey, smallTaskbarIcons, SettingsCacheDuration);
            return smallTaskbarIcons;
        }

        private static bool ReadSmallTaskbarIconsSetting()
        {
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");

            object value = registryKey?.GetValue("TaskbarSmallIcons");
            if (value == null)
            {
                return false;
            }

            int.TryParse(value.ToString(), out int intValue);
            return intValue == 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}