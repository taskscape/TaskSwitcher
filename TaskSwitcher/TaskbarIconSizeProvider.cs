using System;
using Microsoft.Win32;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    internal static class TaskbarIconSizeProvider
    {
        private static readonly TimeSpan SettingsCacheDuration = TimeSpan.FromMinutes(120);
        private const string SmallTaskbarIconsCacheKey = "SmallTaskbarIcons";

        internal static WindowIconSize GetPreferredIconSize()
        {
            IconCacheService cache = IconCacheService.Instance;
            if (cache.TryGetValue<bool>(SmallTaskbarIconsCacheKey, out bool cachedSetting))
            {
                return cachedSetting ? WindowIconSize.Small : WindowIconSize.Large;
            }

            bool useSmallIcons = ReadSmallTaskbarIconsSetting();
            cache.SetValue(SmallTaskbarIconsCacheKey, useSmallIcons, SettingsCacheDuration);
            return useSmallIcons ? WindowIconSize.Small : WindowIconSize.Large;
        }

        private static bool ReadSmallTaskbarIconsSetting()
        {
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");

            object value = registryKey?.GetValue("TaskbarSmallIcons");
            return value != null && int.TryParse(value.ToString(), out int intValue) && intValue == 1;
        }
    }
}
