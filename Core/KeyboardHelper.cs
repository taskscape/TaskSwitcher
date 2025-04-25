using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace TaskSwitcher.Core
{
    // Convert a keycode to the relevant display character
    // http://stackoverflow.com/a/375047/198065
    public class KeyboardHelper
    {
        public static string CodeToString(uint virtualKey)
        {
            uint thread = WinApi.GetWindowThreadProcessId(Process.GetCurrentProcess().MainWindowHandle, out uint procId);
            IntPtr hkl = WinApi.GetKeyboardLayout(thread);

            if (hkl == IntPtr.Zero)
            {
                return string.Empty;
            }

            Keys[] keyStates = new Keys[256];
            if (!WinApi.GetKeyboardState(keyStates))
            {
                return string.Empty;
            }

            uint scanCode = WinApi.MapVirtualKeyEx(virtualKey, WinApi.MapVirtualKeyMapTypes.MAPVK_VK_TO_CHAR, hkl);

            StringBuilder stringBuilder = new(10);
            WinApi.ToUnicodeEx(virtualKey, scanCode, Array.Empty<Keys>(), stringBuilder, stringBuilder.Capacity, 0, hkl);
            return stringBuilder.ToString();
        }
    }
}