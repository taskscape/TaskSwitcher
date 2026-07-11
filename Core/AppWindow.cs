using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ManagedWinapi.Windows;

namespace TaskSwitcher.Core
{
    /// <summary>
    /// This class is a wrapper around the Win32 api window handles
    /// </summary>
    public class AppWindow(IntPtr HWnd) : SystemWindow(HWnd)
    {
        private static readonly TimeSpan ProcessTitleCacheDuration = TimeSpan.FromHours(1);
        private static long _nextFallbackCacheIdentity;
        private readonly object _cacheIdentityLock = new();
        private WindowCacheIdentity? _cacheIdentity;

        public WindowCacheIdentity CacheIdentity
        {
            get
            {
                if (_cacheIdentity.HasValue)
                {
                    return _cacheIdentity.Value;
                }

                lock (_cacheIdentityLock)
                {
                    if (_cacheIdentity.HasValue)
                    {
                        return _cacheIdentity.Value;
                    }

                    WindowCacheIdentity identity = CreateCacheIdentity();
                    _cacheIdentity = identity;
                    return identity;
                }
            }
        }

        public string ProcessTitle
        {
            get
            {
                WindowCacheIdentity identity = CacheIdentity;
                string key = identity.BuildCacheKey("ProcessTitle-");
                return IconCacheService.Instance.GetOrSet(key, () =>
                {
                    using Process process = Process.GetProcessById((int)identity.ProcessId);
                    return process.ProcessName;
                }, ProcessTitleCacheDuration);
            }
        }

        public Icon LargeWindowIcon => new WindowIconFinder().Find(this, WindowIconSize.Large);

        public Icon SmallWindowIcon => new WindowIconFinder().Find(this, WindowIconSize.Small);

        public string ExecutablePath => GetExecutablePath((int)CacheIdentity.ProcessId);

        /// <summary>
        /// Sets the focus to this window and brings it to the foreground.
        /// </summary>
        public void SwitchTo()
        {
            // This function is deprecated, so should probably be replaced.
            WinApi.SwitchToThisWindow(HWnd, true);
        }

        public void SwitchToLastVisibleActivePopup()
        {
            IntPtr lastActiveVisiblePopup = GetLastActiveVisiblePopup();
            WinApi.SwitchToThisWindow(lastActiveVisiblePopup, true);
        }

        public new AppWindow Owner
        {
            get
            {
                IntPtr ownerHandle = WinApi.GetWindow(HWnd, WinApi.GetWindowCmd.GW_OWNER);
                return ownerHandle == IntPtr.Zero ? null : new AppWindow(ownerHandle);
            }
        }

        public new static IEnumerable<AppWindow> AllToplevelWindows
        {
            get
            {
                return SystemWindow.AllToplevelWindows
                    .Select(w => new AppWindow(w.HWnd));
            }
        }

        public bool IsAltTabWindow()
        {
            if (!Visible) return false;
            if (!HasWindowTitle()) return false;
            if (IsAppWindow()) return true;
            if (IsToolWindow()) return false;
            if (IsNoActivate()) return false;
            if (!IsOwnerOrOwnerNotVisible()) return false;
            if (HasITaskListDeletedProperty()) return false;
            if (IsCoreWindow()) return false;
            return !IsApplicationFrameWindow() || HasAppropriateApplicationViewCloakType();
        }

        private bool HasWindowTitle()
        {
            return !string.IsNullOrEmpty(Title);
        }

        private bool IsToolWindow()
        {
            return (ExtendedStyle & WindowExStyleFlags.TOOLWINDOW) == WindowExStyleFlags.TOOLWINDOW
                    || (Style & WindowStyleFlags.TOOLWINDOW) == WindowStyleFlags.TOOLWINDOW;
        }

        private bool IsAppWindow()
        {
            return (ExtendedStyle & WindowExStyleFlags.APPWINDOW) == WindowExStyleFlags.APPWINDOW;
        }

        private bool IsNoActivate()
        {
            return (ExtendedStyle & WindowExStyleFlags.NOACTIVATE) == WindowExStyleFlags.NOACTIVATE;
        }

        private IntPtr GetLastActiveVisiblePopup()
        {
            // Which windows appear in the Alt+Tab list? -Raymond Chen
            // http://blogs.msdn.com/b/oldnewthing/archive/2007/10/08/5351207.aspx

            // Start at the root owner
            IntPtr hwndWalk = WinApi.GetAncestor(HWnd, WinApi.GetAncestorFlags.GetRootOwner);

            // See if we are the last active visible popup
            IntPtr hwndTry = IntPtr.Zero;
            while (hwndWalk != hwndTry)
            {
                hwndTry = hwndWalk;
                hwndWalk = WinApi.GetLastActivePopup(hwndTry);
                if (WinApi.IsWindowVisible(hwndWalk))
                {
                    return hwndWalk;
                }
            }
            return hwndWalk;
        }

        private bool IsOwnerOrOwnerNotVisible()
        {
            return Owner == null || !Owner.Visible;
        }

        private bool HasITaskListDeletedProperty()
        {
            return WinApi.GetProp(HWnd, "ITaskList_Deleted") != IntPtr.Zero;
        }

        private bool IsCoreWindow()
        {
            // Avoids double entries for Windows Store Apps on Windows 10
            return ClassName == "Windows.UI.Core.CoreWindow";
        }

        private bool IsApplicationFrameWindow()
        {
            return ClassName == "ApplicationFrameWindow";
        }

        private bool HasAppropriateApplicationViewCloakType()
        {
            // The ApplicationFrameWindows that host Windows Store Apps like to
            // hang around in Windows 10 even after the underlying program has been
            // closed. A way to figure out if the ApplicationFrameWindow is
            // currently hosting an application is to check if it has a property called
            // "ApplicationViewCloakType", and that the value != 1.
            //
            // I've stumbled upon these values of "ApplicationViewCloakType":
            //    0 = Program is running on current virtual desktop
            //    1 = Program is not running
            //    2 = Program is running on a different virtual desktop

            bool hasAppropriateApplicationViewCloakType = false;
            WinApi.EnumPropsEx(HWnd, (hwnd, lpszString, data, dwData) =>  
            {
                string propName = Marshal.PtrToStringAnsi(lpszString);
                if (propName != "ApplicationViewCloakType") return 1;
                hasAppropriateApplicationViewCloakType = data != 1;
                return 0;
            }, IntPtr.Zero);

            return hasAppropriateApplicationViewCloakType;
        }

        // This method only works on Windows >= Windows Vista
        private static string GetExecutablePath(int processId)
        {
            StringBuilder buffer = new(1024);
            IntPtr hprocess = WinApi.OpenProcess(WinApi.ProcessAccess.QueryLimitedInformation, false, processId);
            if (hprocess == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                uint bufferLength = (uint) buffer.Capacity;
                if (WinApi.QueryFullProcessImageName(hprocess, 0, buffer, ref bufferLength))
                {
                    return buffer.ToString(0, (int) bufferLength);
                }

                // Capture the query error before CloseHandle runs in the finally block.
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                WinApi.CloseHandle(hprocess);
            }
        }

        private WindowCacheIdentity CreateCacheIdentity()
        {
            uint processId = (uint)ProcessId;
            long processStartTimeUtcTicks = 0;

            if (processId != 0)
            {
                try
                {
                    using Process process = Process.GetProcessById((int)processId);
                    processStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
                                                  NotSupportedException or Win32Exception)
                {
                    // Some protected or short-lived processes do not expose their start time.
                }
            }

            if (processStartTimeUtcTicks == 0)
            {
                // Do not share cached metadata when a stable process lifetime cannot be read.
                // A negative per-wrapper discriminator cannot collide with real DateTime ticks.
                processStartTimeUtcTicks = -Interlocked.Increment(ref _nextFallbackCacheIdentity);
            }

            return new WindowCacheIdentity(HWnd, processId, processStartTimeUtcTicks);
        }
    }
}
