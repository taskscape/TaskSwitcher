using System;

namespace TaskSwitcher.Core
{
    /// <summary>
    /// Identifies the lifetime of a window for metadata caching. HWND and process IDs
    /// can both be reused, so the owning process start time is included when available.
    /// A negative per-wrapper discriminator is used when that start time cannot be read.
    /// </summary>
    public readonly record struct WindowCacheIdentity(
        IntPtr WindowHandle,
        uint ProcessId,
        long ProcessStartTimeUtcTicks)
    {
        internal string BuildCacheKey(string prefix)
        {
            return $"{prefix}{WindowHandle}-{ProcessId}-{ProcessStartTimeUtcTicks}";
        }
    }
}
