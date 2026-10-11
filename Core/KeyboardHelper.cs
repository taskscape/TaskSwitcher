using System;

namespace TaskSwitcher.Core
{
    // Convert a keycode to the relevant display character
    // http://stackoverflow.com/a/375047/198065
    public class KeyboardHelper
    {
        public static string CodeToString(uint virtualKey)
        {
            // Zero is the calling thread, which is the UI thread whose layout the user is typing with.
            IntPtr hkl = WinApi.GetKeyboardLayout(0);

            if (hkl == IntPtr.Zero)
            {
                return string.Empty;
            }

            uint scanCode = WinApi.MapVirtualKeyEx(virtualKey, WinApi.MapVirtualKeyMapTypes.MAPVK_VK_TO_VSC, hkl);

            // All keys are reported as released so that the character is the unmodified one.
            // ToUnicodeEx always reads 256 bytes, so the array must really be that long.
            byte[] keyStates = new byte[256];
            char[] buffer = new char[10];
            int length = WinApi.ToUnicodeEx(virtualKey, scanCode, keyStates, buffer, buffer.Length,
                WinApi.ToUnicodeDoNotChangeKeyboardState, hkl);

            // Zero means no translation and a negative value a dead key. Neither has a display
            // character, so the caller falls back to the name of the key.
            if (length <= 0)
            {
                return string.Empty;
            }

            length = Math.Min(length, buffer.Length);
            int terminator = Array.IndexOf(buffer, '\0', 0, length);
            return new string(buffer, 0, terminator >= 0 ? terminator : length);
        }
    }
}
