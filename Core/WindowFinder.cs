using System.Collections.Generic;
using System.Linq;

namespace TaskSwitcher.Core
{
    public class WindowFinder
    {
        /// <summary>
        /// Gets all windows in a lazily evaluated manner
        /// </summary>
        /// <returns>An enumerable of windows that meet the AltTab criteria</returns>
        public IEnumerable<AppWindow> GetWindowsLazy()
        {
            return AppWindow.AllToplevelWindows
                .Where(a => a.IsAltTabWindow());
        }
    }
}