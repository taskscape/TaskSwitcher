using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace TaskSwitcher.Core
{
    public class WindowFinder
    {
        private const int DefaultPageSize = 100;
        
        /// <summary>
        /// Gets all windows (eager loading)
        /// </summary>
        /// <returns>A list of all windows that meet the AltTab criteria</returns>
        public List<AppWindow> GetWindows()
        {
            return GetWindowsLazy().ToList();
        }
        
        /// <summary>
        /// Gets all windows in a lazily evaluated manner
        /// </summary>
        /// <returns>An enumerable of windows that meet the AltTab criteria</returns>
        public IEnumerable<AppWindow> GetWindowsLazy()
        {
            return AppWindow.AllToplevelWindows
                .Where(IsAltTabWindow);
        }

        private static bool IsAltTabWindow(AppWindow window)
        {
            try
            {
                return window.IsAltTabWindow();
            }
            catch (Win32Exception)
            {
                // Windows come and go while they are being enumerated. One that was destroyed
                // while it was inspected must not abort the listing of all the others.
                return false;
            }
        }
        
        /// <summary>
        /// Gets windows with pagination support
        /// </summary>
        /// <param name="pageIndex">Zero-based page index</param>
        /// <param name="pageSize">Number of windows per page</param>
        /// <returns>A paginated list of windows</returns>
        public List<AppWindow> GetWindowsPaged(int pageIndex, int pageSize = DefaultPageSize)
        {
            return GetWindowsLazy()
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();
        }
        
        /// <summary>
        /// Gets the total count of windows without loading all window data
        /// </summary>
        /// <returns>The total number of windows</returns>
        public int GetWindowCount()
        {
            return GetWindowsLazy().Count();
        }
    }
}