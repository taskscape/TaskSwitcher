using System;
using ManagedWinapi.Windows;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text;

namespace ManagedWinapi.Hooks
{
    /// <summary>
    /// A hook that intercepts keyboard events.
    /// </summary>
    public class LowLevelKeyboardHook : Hook
    {
        private char _currentDeadChar = '\0';

        /// <summary>
        /// Called when a key has been intercepted.
        /// </summary>
        public event KeyCallback KeyIntercepted;

        /// <summary>
        /// Called when a character has been intercepted.
        /// </summary>
        public event CharCallback CharIntercepted;

        /// <summary>
        /// Called when a key message has been intercepted.
        /// </summary>
        public event LowLevelMessageCallback MessageIntercepted;

        /// <summary>
        /// Represents a method that handles an intercepted key.
        /// </summary>
        public delegate void KeyCallback(int msg, int vkCode, int scanCode, int flags, int time, IntPtr dwExtraInfo, ref bool handled);

        /// <summary>
        /// Represents a method that handles an intercepted character.
        /// </summary>
        /// <param name="msg">The message that caused the character. Usually Either WM_KEYDOWN or WM_SYSKEYDOWN.</param>
        /// <param name="characters">The character(s) that have been typed, or an empty string if a non-character key (like a cursor key) has been pressed.</param>
        /// <param name="deadKeyPending">Whether a dead key is pending. If a dead key is pending, you may not call the ToUnicode method or similar methods, because they will destroy the deadkey state.</param>
        /// <param name="vkCode">The virtual key code of the message that caused the character.</param>
        /// <param name="scancode">The scancode of the message that caused the character.</param>
        /// <param name="flags">The flags of the message that caused the character.</param>
        /// <param name="time">The timestamp of the message that caused the character.</param>
        /// <param name="dwExtraInfo">The extra info of the message that caused the character.</param>
        public delegate void CharCallback(int msg, string characters, bool deadKeyPending, int vkCode, int scancode, int flags, int time, IntPtr dwExtraInfo);

        /// <summary>
        /// Creates a low-level keyboard hook and hooks it.
        /// </summary>
        /// <param name="callback"></param>
        public LowLevelKeyboardHook(KeyCallback callback)
            : this()
        {
            KeyIntercepted = callback;
            StartHook();
        }

        /// <summary>
        /// Creates a low-level keyboard hook.
        /// </summary>
        public LowLevelKeyboardHook()
            : base(HookType.WH_KEYBOARD_LL, false, true)
        {
            Callback += new HookCallback(LowLevelKeyboardHook_Callback);
        }

        private int LowLevelKeyboardHook_Callback(int code, IntPtr wParam, IntPtr lParam, ref bool callNext)
        {
            if (code == HC_ACTION)
            {
                KBDLLHOOKSTRUCT llh = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                bool handled = false;
                int msg = (int)wParam;
                KeyIntercepted?.Invoke(msg, llh.vkCode, llh.scanCode, llh.flags, llh.time, llh.dwExtraInfo, ref handled);
                MessageIntercepted?.Invoke(new LowLevelKeyboardMessage((int)wParam, llh.vkCode, llh.scanCode, llh.flags, llh.time, llh.dwExtraInfo), ref handled);
                if (handled)
                {
                    callNext = false;
                    return 1;
                }

                if (CharIntercepted == null || (msg != 256 && msg != 260)) return 0;
                // Note that dead keys are somehow tricky, since ToUnicode changes their state
                // in the keyboard driver. So, if we catch a dead key and call ToUnicode on it,
                // we will have to stop the hook; otherwise the deadkey appears twice on the screen.
                // On the other hand, we try to avoid calling ToUnicode on the key pressed after
                // the dead key (the one which is modified by the deadkey), because that would
                // drop the deadkey altogether. Resynthesizing the deadkey event is hard since
                // some deadkeys are unshifted but used on shifted characters or vice versa.
                // This solution will not lose any dead keys; its only drawback is that dead
                // keys are not properly translated. Better implementations are welcome.
                if (llh.vkCode == (int)Keys.ShiftKey ||
                    llh.vkCode == (int)Keys.LShiftKey ||
                    llh.vkCode == (int)Keys.RShiftKey ||
                    llh.vkCode == (int)Keys.LControlKey ||
                    llh.vkCode == (int)Keys.RControlKey ||
                    llh.vkCode == (int)Keys.ControlKey ||
                    llh.vkCode == (int)Keys.Menu ||
                    llh.vkCode == (int)Keys.LMenu ||
                    llh.vkCode == (int)Keys.RMenu)
                {
                    // ignore shift keys, they do not get modified by dead keys.
                }
                else if (_currentDeadChar != '\0')
                {
                    CharIntercepted(msg, "" + (llh.vkCode == (int)Keys.Space ? _currentDeadChar : '\x01'), true,
                        llh.vkCode, llh.scanCode, llh.flags, llh.time, llh.dwExtraInfo);
                    _currentDeadChar = '\0';
                }
                else
                {
                    short dummy = new KeyboardKey(Keys.Capital).State; // will refresh CAPS LOCK state for current thread
                    byte[] kbdState = new byte[256];
                    ApiHelper.FailIfZero(GetKeyboardState(kbdState));
                    StringBuilder buff = new(64);
                    int length = ToUnicode((int)llh.vkCode, llh.scanCode, kbdState, buff, 64, 0);
                    if (length == -1)
                    {
                        _currentDeadChar = buff[0];
                        callNext = false;
                        return 1;
                    }
                    if (buff.Length != length)
                        buff.Remove(length, buff.Length - length);
                    CharIntercepted(msg, buff.ToString(), false,
                        llh.vkCode, llh.scanCode, llh.flags, llh.time, llh.dwExtraInfo);
                }
            }
            return 0;
        }

        #region PInvoke Declarations

        [StructLayout(LayoutKind.Sequential)]
        private class KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        private static extern int GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern int ToUnicode(int wVirtKey, int wScanCode, byte[] lpKeyState,
           [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)] StringBuilder pwszBuff, int cchBuff,
           uint wFlags);

        #endregion
    }

    /// <summary>
    /// A hook that intercepts mouse events
    /// </summary>
    public class LowLevelMouseHook : Hook
    {

        /// <summary>
        /// Called when a mouse action has been intercepted.
        /// </summary>
        public event MouseCallback MouseIntercepted;

        /// <summary>
        /// Called when a mouse message has been intercepted.
        /// </summary>
        public event LowLevelMessageCallback MessageIntercepted;

        /// <summary>
        /// Represents a method that handles an intercepted mouse action.
        /// </summary>
        public delegate void MouseCallback(int msg, POINT pt, int mouseData, int flags, int time, IntPtr dwExtraInfo, ref bool handled);

        /// <summary>
        /// Creates a low-level mouse hook and hooks it.
        /// </summary>
        public LowLevelMouseHook(MouseCallback callback)
            : this()
        {
            MouseIntercepted = callback;
            StartHook();
        }

        /// <summary>
        /// Creates a low-level mouse hook.
        /// </summary>
        public LowLevelMouseHook()
            : base(HookType.WH_MOUSE_LL, false, true)
        {
            Callback += new HookCallback(LowLevelMouseHook_Callback);
        }

        private int LowLevelMouseHook_Callback(int code, IntPtr wParam, IntPtr lParam, ref bool callNext)
        {
            if (code == HC_ACTION)
            {
                MSLLHOOKSTRUCT llh = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                bool handled = false;
                if (MouseIntercepted != null)
                {
                    MouseIntercepted((int)wParam, llh.pt, llh.mouseData, llh.flags, llh.time, llh.dwExtraInfo, ref handled);
                }
                if (MessageIntercepted != null)
                {
                    MessageIntercepted(new LowLevelMouseMessage((int)wParam, llh.pt, llh.mouseData, llh.flags, llh.time, llh.dwExtraInfo), ref handled);
                }
                if (handled)
                {
                    callNext = false;
                    return 1;
                }
            }
            return 0;
        }

        #region PInvoke Declarations

        [StructLayout(LayoutKind.Sequential)]
        private class MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        #endregion
    }

    /// <summary>
    /// Represents a method that handles an intercepted low-level message.
    /// </summary>
    public delegate void LowLevelMessageCallback(LowLevelMessage evt, ref bool handled);

    /// <summary>
    /// A message that has been intercepted by a low-level hook
    /// </summary>
    public abstract class LowLevelMessage
    {
        internal LowLevelMessage(int msg, int flags, int time, IntPtr dwExtraInfo)
        {
            this.Message = msg;
            this.Flags = flags;
            this.Time = time;
            ExtraInfo = dwExtraInfo;
        }

        /// <summary>
        /// The time this message happened.
        /// </summary>
        public int Time { get; set; }

        /// <summary>
        /// Flags of the message. Its contents depend on the message.
        /// </summary>
        public int Flags { get; }

        /// <summary>
        /// The message identifier.
        /// </summary>
        public int Message { get; }

        /// <summary>
        /// Extra information. Its contents depend on the message.
        /// </summary>
        public IntPtr ExtraInfo { get; }

        /// <summary>
        /// Replays this event as if the user did it again.
        /// </summary>
        public abstract void ReplayEvent();
    }

    /// <summary>
    /// A message that has been intercepted by a low-level mouse hook
    /// </summary>
    public class LowLevelMouseMessage : LowLevelMessage
    {
        /// <summary>
        /// Creates a new low-level mouse message.
        /// </summary>
        public LowLevelMouseMessage(int msg, POINT pt, int mouseData, int flags, int time, IntPtr dwExtraInfo)
            : base(msg, flags, time, dwExtraInfo)
        {
            this.Point = pt;
            this.MouseData = mouseData;
        }

        /// <summary>
        /// The mouse position where this message occurred.
        /// </summary>
        public POINT Point { get; }

        /// <summary>
        /// Additional mouse data, depending on the type of event.
        /// </summary>
        public int MouseData { get; }

        /// <summary>
        /// Mouse event flags needed to replay this message.
        /// </summary>
        private uint MouseEventFlags
        {
            get
            {
                switch (Message)
                {
                    case WM_LBUTTONDOWN:
                        return (uint)MouseEventFlagValues.LEFTDOWN;
                    case WM_LBUTTONUP:
                        return (uint)MouseEventFlagValues.LEFTUP;
                    case WM_MOUSEMOVE:
                        return (uint)MouseEventFlagValues.MOVE;
                    case WM_MOUSEWHEEL:
                        return (uint)MouseEventFlagValues.WHEEL;
                    case WM_MOUSEHWHEEL:
                        return (uint)MouseEventFlagValues.HWHEEL;
                    case WM_RBUTTONDOWN:
                        return (uint)MouseEventFlagValues.RIGHTDOWN;
                    case WM_RBUTTONUP:
                        return (uint)MouseEventFlagValues.RIGHTUP;
                    case WM_MBUTTONDOWN:
                        return (uint)MouseEventFlagValues.MIDDLEDOWN;
                    case WM_MBUTTONUP:
                        return (uint)MouseEventFlagValues.MIDDLEUP;
                    case WM_MBUTTONDBLCLK:
                    case WM_RBUTTONDBLCLK:
                    case WM_LBUTTONDBLCLK:
                        return 0;
                }
                throw new Exception("Unsupported message");
            }
        }


        /// <summary>
        /// Replays this event.
        /// </summary>
        public override void ReplayEvent()
        {
            Cursor.Position = Point;
            if (MouseEventFlags != 0)
                KeyboardKey.InjectMouseEvent(MouseEventFlags, 0, 0, (uint)MouseData >> 16, new UIntPtr((ulong)ExtraInfo.ToInt64()));
        }

        #region PInvoke Declarations
        [Flags]
        private enum MouseEventFlagValues
        {
            LEFTDOWN = 0x00000002,
            LEFTUP = 0x00000004,
            MIDDLEDOWN = 0x00000020,
            MIDDLEUP = 0x00000040,
            MOVE = 0x00000001,
            RIGHTDOWN = 0x00000008,
            RIGHTUP = 0x00000010,
            WHEEL = 0x00000800,
            HWHEEL = 0x00001000
        }

        private const int WM_MOUSEMOVE = 0x200;
        private const int WM_LBUTTONDOWN = 0x201;
        private const int WM_LBUTTONUP = 0x202;
        private const int WM_LBUTTONDBLCLK = 0x203;
        private const int WM_RBUTTONDOWN = 0x204;
        private const int WM_RBUTTONUP = 0x205;
        private const int WM_RBUTTONDBLCLK = 0x206;
        private const int WM_MBUTTONDOWN = 0x207;
        private const int WM_MBUTTONUP = 0x208;
        private const int WM_MBUTTONDBLCLK = 0x209;
        private const int WM_MOUSEWHEEL = 0x20A;
        private const int WM_MOUSEHWHEEL = 0x020E;
        #endregion
    }

    /// <summary>
    /// A message that has been intercepted by a low-level mouse hook
    /// </summary>
    public class LowLevelKeyboardMessage : LowLevelMessage
    {
        /// <summary>
        /// Creates a new low-level keyboard message.
        /// </summary>
        public LowLevelKeyboardMessage(int msg, int vkCode, int scanCode, int flags, int time, IntPtr dwExtraInfo)
            : base(msg, flags, time, dwExtraInfo)
        {
            this.VirtualKeyCode = vkCode;
            this.ScanCode = scanCode;
        }

        /// <summary>
        /// The virtual key code that caused this message.
        /// </summary>
        public int VirtualKeyCode { get; }

        /// <summary>
        /// The scan code that caused this message.
        /// </summary>
        public int ScanCode { get; }

        /// <summary>
        /// Flags needed to replay this event.
        /// </summary>
        private uint KeyboardEventFlags
        {
            get
            {
                switch (Message)
                {
                    case WM_KEYDOWN:
                    case WM_SYSKEYDOWN:
                        return 0;
                    case WM_KEYUP:
                    case WM_SYSKEYUP:
                        return KEYEVENTF_KEYUP;
                }
                throw new Exception("Unsupported message");
            }
        }

        /// <summary>
        /// Replays this event.
        /// </summary>
        public override void ReplayEvent()
        {
            KeyboardKey.InjectKeyboardEvent((Keys)VirtualKeyCode, (byte)ScanCode, KeyboardEventFlags, new UIntPtr((ulong)ExtraInfo.ToInt64()));
        }

        #region PInvoke Declarations

        private const int KEYEVENTF_KEYUP = 0x2;

        private const int WM_KEYDOWN = 0x100,
            WM_KEYUP = 0x101,
            WM_SYSKEYDOWN = 0x104,
            WM_SYSKEYUP = 0x105;
        #endregion
    }
}
