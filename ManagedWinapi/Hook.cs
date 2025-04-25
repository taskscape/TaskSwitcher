using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ManagedWinapi.Hooks
{
    /// <summary>
    /// A hook is a point in the system message-handling mechanism where an application
    /// can install a subroutine to monitor the message traffic in the system and process 
    /// certain types of messages before they reach the target window procedure.
    /// </summary>
    public class Hook : Component
    {
        internal bool hooked;
        private IntPtr hHook;
        private bool wrapCallback, global;
        private IntPtr wrappedDelegate;
        private IntPtr hWrapperInstance;
        private readonly HookProc managedDelegate;

        /// <summary>
        /// Occurs when the hook's callback is called.
        /// </summary>
        public event HookCallback Callback;

        /// <summary>
        /// Represents a method that handles a callback from a hook.
        /// </summary>
        public delegate int HookCallback(int code, IntPtr wParam, IntPtr lParam, ref bool callNext);

        /// <summary>
        /// Creates a new hook and hooks it.
        /// </summary>
        public Hook(HookType type, HookCallback callback, bool wrapCallback, bool global)
            : this(type, wrapCallback, global)
        {
            Callback += callback;
            StartHook();
        }

        /// <summary>
        /// Creates a new hook.
        /// </summary>
        protected Hook(HookType type, bool wrapCallback, bool global)
            : this()
        {
            Type = type;
            this.wrapCallback = wrapCallback;
            this.global = global;
        }

        /// <summary>
        /// Creates a new hook.
        /// </summary>
        public Hook(IContainer container)
            : this()
        {
            container.Add(this);
        }

        /// <summary>
        /// Creates a new hook.
        /// </summary>
        public Hook()
        {
            managedDelegate = InternalCallback;
        }

        /// <summary>
        /// The type of the hook.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        /// <summary>
        /// The type of the hook.
        /// </summary>
        private HookType Type { get; set; }

        /// <summary>
        /// Whether this hook has been started.
        /// </summary>
        protected bool Hooked => hooked;

        /// <summary>
        /// Hooks the hook.
        /// </summary>
        public virtual void StartHook()
        {
            if (hooked) return;
            IntPtr delegt = Marshal.GetFunctionPointerForDelegate(managedDelegate);
            if (wrapCallback)
            {
                wrappedDelegate = AllocHookWrapper(delegt);
                hWrapperInstance = LoadLibrary("ManagedWinapiNativeHelper.dll");
                hHook = SetWindowsHookEx(Type, wrappedDelegate, hWrapperInstance, 0);
            }
            else if (global)
            {
                // http://stackoverflow.com/a/17898148/198065
                IntPtr moduleHandle = LoadLibrary("user32.dll");
                hHook = SetWindowsHookEx(Type, delegt, moduleHandle, 0);
            }
            else
            {
                hHook = SetWindowsHookEx(Type, delegt, IntPtr.Zero, getThreadID());
            }

            if (hHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            hooked = true;
        }

        private uint getThreadID()
        {
#pragma warning disable 0618
            return (uint) AppDomain.GetCurrentThreadId();
#pragma warning restore 0618
        }

        /// <summary>
        /// Unhooks the hook.
        /// </summary>
        public virtual void Unhook()
        {
            if (!hooked) return;
            if (!UnhookWindowsHookEx(hHook)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (wrapCallback)
            {
                if (!FreeHookWrapper(wrappedDelegate) || !FreeLibrary(hWrapperInstance)) throw new Win32Exception();
            }

            hooked = false;
        }

        /// <summary>
        /// Unhooks the hook if necessary.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (hooked)
            {
                Unhook();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Override this method if you want to prevent a call
        /// to the CallNextHookEx method or if you want to return
        /// a different return value. For most hooks this is not needed.
        /// </summary>
        protected virtual int InternalCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0 || Callback == null) 
                return CallNextHookEx(hHook, code, wParam, lParam);
            bool callNext = true;
            int retval = Callback(code, wParam, lParam, ref callNext);
            return !callNext ? retval : CallNextHookEx(hHook, code, wParam, lParam);
        }

        #region PInvoke Declarations

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(HookType hook, IntPtr callback,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        internal static extern int CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam,
            IntPtr lParam);

        [DllImport("ManagedWinapiNativeHelper.dll")]
        private static extern IntPtr AllocHookWrapper(IntPtr callback);

        [DllImport("ManagedWinapiNativeHelper.dll")]
        private static extern bool FreeHookWrapper(IntPtr wrapper);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private delegate int HookProc(int code, IntPtr wParam, IntPtr lParam);

        internal static readonly int HC_ACTION = 0,
            HC_GETNEXT = 1,
            HC_SKIP = 2,
            HC_NOREMOVE = 3,
            HC_SYSMODALON = 4,
            HC_SYSMODALOFF = 5;

        #endregion
    }

    /// <summary>
    /// A hook that intercepts local window messages.
    /// </summary>
    public class LocalMessageHook : Hook
    {
        /// <summary>
        /// Called when a message has been intercepted.
        /// </summary>
        public event MessageCallback MessageOccurred;

        /// <summary>
        /// Represents a method that handles a message from a message hook.
        /// </summary>
        /// <param name="msg"></param>
        public delegate void MessageCallback(Message msg);

        /// <summary>
        /// Creates a local message hook.
        /// </summary>
        public LocalMessageHook()
            : base(HookType.WH_GETMESSAGE, false, false)
        {
            Callback += MessageHookCallback;
        }

        private int MessageHookCallback(int code, IntPtr lParam, IntPtr wParam, ref bool callNext)
        {
            if (code != HC_ACTION) return 0;
            Message msg = (Message) Marshal.PtrToStructure(wParam, typeof(Message));
            MessageOccurred?.Invoke(msg);

            return 0;
        }
    }

    /// <summary>
    /// Hook Types. See the documentation of SetWindowsHookEx for reference.
    /// </summary>
    public enum HookType
    {
        ///
        WH_JOURNALPLAYBACK = 1,

        ///
        WH_GETMESSAGE = 3,

        ///
        WH_KEYBOARD_LL = 13,

        ///
        WH_MOUSE_LL = 14
    }
}