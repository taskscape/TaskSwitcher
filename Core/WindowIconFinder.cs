using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskSwitcher.Core
{
    public enum WindowIconSize
    {
        Small,
        Large
    }

    public class WindowIconFinder
    {
        private readonly IconCacheService _cache = IconCacheService.Instance;

        public Icon Find(AppWindow window, WindowIconSize size)
        {
            // Try to get the icon from cache
            Icon cachedIcon = _cache.GetIcon(window.HWnd, size);
            if (cachedIcon != null)
            {
                return cachedIcon;
            }

            // If not in cache, find the icon using the original implementation
            Icon icon = FindIconImplementation(window, size);

            // Cache the icon
            if (icon != null)
            {
                _cache.SetIcon(window.HWnd, size, icon);
            }

            return icon;
        }

        private static Icon FindIconImplementation(AppWindow window, WindowIconSize size)
        {
            Icon icon = null;
            try
            {
                // http://msdn.microsoft.com/en-us/library/windows/desktop/ms632625(v=vs.85).aspx
                IntPtr outValue = WinApi.SendMessageTimeout(window.HWnd, 0x007F,
                    size == WindowIconSize.Small ? new IntPtr(2) : new IntPtr(1),
                    IntPtr.Zero, WinApi.SendMessageTimeoutFlags.SMTO_ABORTIFHUNG, 100, out IntPtr response);

                if (outValue == IntPtr.Zero || response == IntPtr.Zero)
                {
                    response = WinApi.GetClassLongPtr(window.HWnd,
                        size == WindowIconSize.Small
                            ? WinApi.ClassLongFlags.GCLP_HICONSM
                            : WinApi.ClassLongFlags.GCLP_HICON);
                }

                if (response != IntPtr.Zero)
                {
                    try
                    {
                        icon = Icon.FromHandle(response);
                    }
                    catch (ArgumentException)
                    {
                        // Invalid icon handle
                    }
                    catch (ExternalException)
                    {
                        // GDI+ error occurred while creating the icon
                    }
                }
                else
                {
                    string executablePath = window.ExecutablePath;
                    try
                    {
                        icon = Icon.ExtractAssociatedIcon(executablePath);
                    }
                    catch (ArgumentException)
                    {
                        // Invalid executable path or file does not exist
                    }
                    catch (FileNotFoundException)
                    {
                        // Executable file not found
                    }
                }
            }
            catch (Win32Exception)
            {
                // Could not extract icon
            }
            return icon;
        }
    }
}