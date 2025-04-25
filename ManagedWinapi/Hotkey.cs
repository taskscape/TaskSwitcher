using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ManagedWinapi.Windows;

namespace ManagedWinapi
{

    /// <summary>
    /// Specifies a component that creates a global keyboard hotkey.
    /// </summary>
    [DefaultEvent("HotkeyPressed")]
    public class Hotkey : Component
    {

        /// <summary>
        /// Occurs when the hotkey is pressed.
        /// </summary>
        public event EventHandler HotkeyPressed;

        private static Object myStaticLock = new();
        private static int hotkeyCounter = 0xA000;

        private int hotkeyIndex;
        private bool isDisposed, isEnabled, isRegistered;
        private Keys _keyCode;
        private bool _ctrl, _alt, _shift, _windows;
        private readonly IntPtr hWnd;

        /// <summary>
        /// Initializes a new instance of this class.
        /// </summary>
        public Hotkey()
        {
            EventDispatchingNativeWindow.Instance.EventHandler += nw_EventHandler;
            lock (myStaticLock)
            {
                hotkeyIndex = ++hotkeyCounter;
            }

            hWnd = EventDispatchingNativeWindow.Instance.Handle;
        }

        /// <summary>
        /// Enables the hotkey. When the hotkey is enabled, pressing it causes a
        /// <c>HotkeyPressed</c> event instead of being handled by the active 
        /// application.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Enabled
        {
            set
            {
                isEnabled = value;
                updateHotkey(false);
            }
        }

        /// <summary>
        /// The key code of the hotkey.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Keys KeyCode
        {
            get => _keyCode;

            set
            {
                _keyCode = value;
                updateHotkey(true);
            }
        }

        /// <summary>
        /// Whether the shortcut includes the Control modifier.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Ctrl
        {
            get => _ctrl;
            set
            {
                _ctrl = value;
                updateHotkey(true);
            }
        }

        /// <summary>
        /// Whether this shortcut includes the Alt modifier.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Alt
        {
            get => _alt;
            set
            {
                _alt = value;
                updateHotkey(true);
            }
        }

        /// <summary>
        /// Whether this shortcut includes the shift modifier.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Shift
        {
            get => _shift;
            set
            {
                _shift = value;
                updateHotkey(true);
            }
        }

        /// <summary>
        /// Whether this shortcut includes the Windows key modifier. The windows key
        /// is an addition by Microsoft to the keyboard layout. It is located between
        /// Control and Alt and depicts a Windows flag.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool WindowsKey
        {
            get => _windows;
            set
            {
                _windows = value;
                updateHotkey(true);
            }
        }

        private void nw_EventHandler(ref Message m, ref bool handled)
        {
            if (handled) return;
            if (m.Msg != WM_HOTKEY || m.WParam.ToInt32() != hotkeyIndex)
            {
                return;
            }

            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        /// <summary>
        /// Releases all resources used by the System.ComponentModel.Component.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            isDisposed = true;
            updateHotkey(false);
            EventDispatchingNativeWindow.Instance.EventHandler -= nw_EventHandler;
            base.Dispose(disposing);
        }

        private void updateHotkey(bool reregister)
        {
            bool shouldBeRegistered = isEnabled && !isDisposed && !DesignMode;
            if (isRegistered && (!shouldBeRegistered || reregister))
            {
                // unregister hotkey
                UnregisterHotKey(hWnd, hotkeyIndex);
                isRegistered = false;
            }

            if (isRegistered || !shouldBeRegistered)
            {
                return;
            }

            // register hotkey
            bool success = RegisterHotKey(hWnd, hotkeyIndex,
                (_shift ? MOD_SHIFT : 0) + (_ctrl ? MOD_CONTROL : 0) +
                (_alt ? MOD_ALT : 0) + (_windows ? MOD_WIN : 0), (int)_keyCode);
            if (!success) throw new HotkeyAlreadyInUseException();
            isRegistered = true;
        }

        #region PInvoke Declarations

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private static readonly int MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;

        private static readonly int WM_HOTKEY = 0x0312;

        #endregion
    }

    /// <summary>
    /// The exception is thrown when a hotkey should be registered that
    /// has already been registered by another application.
    /// </summary>
    public class HotkeyAlreadyInUseException : Exception
    {
    }
}