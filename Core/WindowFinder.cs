using System;
using System.Collections.Generic;
using System.Linq;
using TaskSwitcher.Core.Browsers;

namespace TaskSwitcher.Core
{
    public class WindowFinder
    {
        private const int DefaultPageSize = 100;
        private readonly bool _includeBrowserTabs;
        
        public WindowFinder(bool includeBrowserTabs = true)
        {
            _includeBrowserTabs = includeBrowserTabs;
        }
        
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
            // Get regular application windows
            var appWindows = AppWindow.AllToplevelWindows
                .Where(a => a.IsAltTabWindow());
            
            if (!_includeBrowserTabs)
                return appWindows;
                
            // Get Chrome tab windows
            var chromeTabs = ChromeTabWindow.GetAllChromeTabs();
            
            // Combine regular windows and Chrome tabs
            return appWindows.Concat(chromeTabs);
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