using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ManagedWinapi.Windows;

namespace TaskSwitcher.Core.Browsers
{
    /// <summary>
    /// Represents a Chrome tab as a switchable window in the application
    /// </summary>
    public class ChromeTabWindow : AppWindow
    {
        private static readonly ChromeTabManager ChromeTabManager = new ChromeTabManager();
        
        /// <summary>
        /// The tab information
        /// </summary>
        public ChromeTabManager.ChromeTab TabInfo { get; }
        
        // Since we can't override Title directly, we'll use a private field
        private readonly string _customTitle;
        
        /// <summary>
        /// The title to display in the TaskSwitcher
        /// We can't override Title directly since it's not virtual
        /// </summary>
        public string DisplayTitle => _customTitle;
        
        /// <summary>
        /// Process title for Chrome tabs should indicate it's a tab
        /// </summary>
        public override string ProcessTitle => "Chrome Tab";
        
        /// <summary>
        /// Gets a unique identifier for this Chrome tab
        /// </summary>
        public string TabIdentifier => TabInfo.TabIdentifier;
        
        /// <summary>
        /// Whether to use advanced tab switching methods
        /// </summary>
        public static bool UseAdvancedTabSwitching { get; set; } = true;
        
        /// <summary>
        /// Create a new ChromeTabWindow instance
        /// </summary>
        public ChromeTabWindow(ChromeTabManager.ChromeTab tabInfo)
            : base(tabInfo.BrowserHandle)
        {
            TabInfo = tabInfo;
            // Use a better format that indicates it's a Chrome tab
            _customTitle = $"🌐 Chrome Tab: {TabInfo.Title}";
        }
        
        /// <summary>
        /// Switch to this Chrome tab
        /// </summary>
        public override void SwitchTo()
        {
            ChromeTabManager.SwitchToTab(TabIdentifier, UseAdvancedTabSwitching);
        }
        
        /// <summary>
        /// Switch to the last visible active popup, which for Chrome tabs is the tab itself
        /// </summary>
        public override void SwitchToLastVisibleActivePopup()
        {
            SwitchTo();
        }
        
        /// <summary>
        /// Get all Chrome tabs as ChromeTabWindow objects
        /// </summary>
        public static IEnumerable<ChromeTabWindow> GetAllChromeTabs()
        {
            try
            {
                return ChromeTabManager.GetAllChromeTabs()
                    .Select(tab => new ChromeTabWindow(tab));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Chrome tabs: {ex.Message}");
                return Enumerable.Empty<ChromeTabWindow>();
            }
        }
        
        /// <summary>
        /// Determine if this is a Chrome tab window
        /// </summary>
        public bool IsTabWindow => true;
    }
}