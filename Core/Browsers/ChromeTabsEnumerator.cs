using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TaskSwitcher.Core.Browsers
{
    /// <summary>
    /// Alternative tab enumerator that uses Windows API and keyboard simulation
    /// to enumerate Chrome tabs as a fallback
    /// </summary>
    public class ChromeTabsEnumerator
    {
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_CONTROL = 0x11;
        private const int VK_TAB = 0x09;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// Enumerates Chrome tabs by simulating Ctrl+Tab keystrokes
        /// This is a fallback method that won't work in all cases but might help
        /// when UI Automation fails
        /// </summary>
        public static List<string> EnumerateTabsByCycling(IntPtr chromeWindowHandle)
        {
            var tabs = new List<string>();
            
            try
            {
                // First, get the currently visible tab from the window title
                var proc = Process.GetProcessById(WinApi.GetWindowProcessId(chromeWindowHandle));
                string originalTitle = proc.MainWindowTitle;
                string currentTitle = originalTitle;
                
                if (string.IsNullOrEmpty(currentTitle))
                    return tabs;
                
                // Clean up the title
                if (currentTitle.EndsWith(" - Google Chrome"))
                {
                    currentTitle = currentTitle.Substring(0, currentTitle.Length - 16);
                }
                
                // Add the first tab
                tabs.Add(currentTitle);
                
                // Bring the window to foreground first
                WinApi.SetForegroundWindow(chromeWindowHandle);
                Thread.Sleep(100);

                // We'll cycle through tabs until we see the first one again or reach a max count
                int maxTabs = 50; // Safety limit
                int tabCount = 1;
                
                while (tabCount < maxTabs)
                {
                    // Press Ctrl+Tab to cycle to next tab
                    PressCtrlTab(chromeWindowHandle);
                    
                    // Wait a bit for the tab to switch
                    Thread.Sleep(100);
                    
                    // Get the new window title
                    string newTitle = proc.MainWindowTitle;
                    
                    // Clean up the title
                    if (newTitle.EndsWith(" - Google Chrome"))
                    {
                        newTitle = newTitle.Substring(0, newTitle.Length - 16);
                    }
                    
                    // If we're back at the first tab, we're done
                    if (newTitle == currentTitle || newTitle == originalTitle)
                        break;
                    
                    // Add this tab if it's not already in the list
                    if (!tabs.Contains(newTitle))
                    {
                        tabs.Add(newTitle);
                    }
                    
                    tabCount++;
                }
                
                // Go back to the original tab
                // One way to do this is to press Ctrl+1
                PressCtrl1(chromeWindowHandle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enumerating tabs by cycling: {ex.Message}");
            }
            
            return tabs;
        }
        
        /// <summary>
        /// Simulates pressing Ctrl+Tab
        /// </summary>
        private static void PressCtrlTab(IntPtr windowHandle)
        {
            try
            {
                // Press Ctrl down
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                
                // Press Tab down and up
                keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
                keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                
                // Release Ctrl
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error simulating Ctrl+Tab: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Simulates pressing Ctrl+1 to go to the first tab
        /// </summary>
        private static void PressCtrl1(IntPtr windowHandle)
        {
            try
            {
                // Press Ctrl down
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                
                // Press 1 down and up
                keybd_event(0x31, 0, 0, UIntPtr.Zero); // 0x31 is the virtual key code for '1'
                keybd_event(0x31, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                
                // Release Ctrl
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error simulating Ctrl+1: {ex.Message}");
            }
        }
    }
}