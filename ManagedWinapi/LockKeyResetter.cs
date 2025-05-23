using System;
using System.Windows.Forms;
using static System.Windows.Forms.Application;

namespace ManagedWinapi
{
    /// <summary>
    /// Utility class that can be used to create key events with all current 
    /// locking keys (like Caps Lock) disabled. Other modifier keys (like Ctrl or Shift)
    /// are also ignored if they are currently pressed on the "real" keyboard.
    /// </summary>
    /// <example>
    /// <code>
    /// using (new LockKeysResetter()) {
    ///     SendKeys.Send("Hello");
    /// }
    /// </code>
    /// </example>
    public class LockKeyResetter :IDisposable
    {
        static readonly Keys[] MODIFIER_KEYS = { 
            Keys.RShiftKey, Keys.LShiftKey, Keys.ShiftKey,
            Keys.RMenu, Keys.LMenu, Keys.Menu,
            Keys.RControlKey, Keys.LControlKey, Keys.LMenu,
            Keys.RWin, Keys.LWin, Keys.CapsLock
        };

        bool capslock;
        bool[] simpleModifiers = new bool[MODIFIER_KEYS.Length];

        /// <summary>
        /// Reset all modifier keys and remember in this object which modifier keys
        /// have been set.
        /// </summary>
        public LockKeyResetter()
        {
            for (int i = 0; i < MODIFIER_KEYS.Length; i++)
            {
                KeyboardKey k = new KeyboardKey(MODIFIER_KEYS[i]);
                short dummy = k.AsyncState; // reset remembered status
                if (k.AsyncState != 0)
                {
                    simpleModifiers[i] = true;
                    k.Release();
                }
            }
            KeyboardKey capslockKey = new(Keys.CapsLock);
            int capslockstate = capslockKey.State;
            capslock = ((capslockstate & 0x01) == 0x01);
            if (!capslock) return;
            
            // press caps lock
            capslockKey.PressAndRelease();
            DoEvents();
            if ((capslockKey.State & 0x01) == 0x01)
            {
                // press shift
                new KeyboardKey(Keys.ShiftKey).PressAndRelease();
            }
            DoEvents();
            if ((capslockKey.State & 0x01) == 0x01)
            {
                throw new Exception("Cannot disable caps lock.");
            }
        }

        /// <summary>
        /// Set all modifier keys that have been set before. Since this class implements
        /// <see cref="IDisposable"/>, you can use the <c>using</c> 
        /// keyword in C# to automatically set modifier keys when you have finished.
        /// </summary>
        public void Dispose()
        {
            if (capslock) {
                // press caps lock
                KeyboardKey capslockKey = new(Keys.CapsLock);
                capslockKey.PressAndRelease();
                DoEvents();
                if ((capslockKey.State & 0x01) != 0x01)
                    throw new Exception("Cannot enable caps lock.");
            }
            for (int i = MODIFIER_KEYS.Length-1; i >= 0; i--)
            {
                if (simpleModifiers[i])
                {
                    new KeyboardKey(MODIFIER_KEYS[i]).Press();
                }
            }
        }

        /// <summary>
        /// Convenience method to send keys with all modifiers disabled.
        /// </summary>
        /// <param name="keys">The keys to send</param>
        public static void Send(string keys)
        {
            using (new LockKeyResetter())
            {
                SendKeys.Send(keys);
            }
        }

        /// <summary>
        /// Convenience method to send keys and wait for them (like 
        /// <see cref="SendKeys.SendWait">SendKeys.SendWait</see>) with all modifiers disabled.
        /// </summary>
        /// <param name="keys"></param>
        public static void SendWait(string keys)
        {
            using (new LockKeyResetter())
            {
                SendKeys.SendWait(keys);
            }
        }
    }
}
