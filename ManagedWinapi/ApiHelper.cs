using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ManagedWinapi
{
    /// <summary>
    /// Helper class that contains static methods useful for API programming. This
    /// class is not exposed to the user.
    /// </summary>
    internal class ApiHelper
    {
        /// <summary>
        /// Throw a <see cref="Win32Exception"/> if the supplied (return) value is zero.
        /// This exception uses the last Win32 error code as error message.
        /// </summary>
        /// <param name="returnValue">The return value to test.</param>
        internal static int FailIfZero(int returnValue)
        {
            if (returnValue == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return returnValue;
        }

        /// <summary>
        /// Throw a <see cref="Win32Exception"/> if the supplied (return) value is zero.
        /// This exception uses the last Win32 error code as error message.
        /// </summary>
        /// <param name="returnValue">The return value to test.</param>
        internal static IntPtr FailIfZero(IntPtr returnValue)
        {
            if (returnValue == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return returnValue;
        }
    }
}
