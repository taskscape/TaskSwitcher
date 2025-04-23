using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using TaskSwitcher.Core.Utilities;

namespace TaskSwitcher.Core.Browsers
{
    /// <summary>
    /// Manages detection and interaction with Google Chrome browser tabs
    /// </summary>
    public class ChromeTabManager
    {
        private const string ChromeClassName = "Chrome_WidgetWin_1";
        private const string ChromeProcessName = "chrome";
        private const string TabControlClassName = "Chrome_RenderWidgetHostHWND";
        
        // Add timeouts to prevent hangs
        private const int DetectionTimeoutMs = 5000; // 5 seconds

        /// <summary>
        /// Represents a Chrome browser tab
        /// </summary>
        public class ChromeTab
        {
            public string Title { get; set; }
            public IntPtr BrowserHandle { get; set; }
            public int TabIndex { get; set; }
            
            // This is the identifier we'll use to switch to this specific tab
            public string TabIdentifier { get; set; }
        }

        /// <summary>
        /// Gets all open Chrome tabs across all Chrome windows
        /// </summary>
        public IEnumerable<ChromeTab> GetAllChromeTabs()
        {
            var chromeTabs = new List<ChromeTab>();
            Stopwatch sw = Stopwatch.StartNew();
            
            try
            {
                Logger.Info("Starting Chrome tab detection");
                
                // Find all Chrome windows
                var stopwatchWindows = Stopwatch.StartNew();
                Logger.Debug("Finding Chrome windows...");
                
                var chromeWindows = AppWindow.AllToplevelWindows
                    .Where(w => 
                    {
                        // Add timeout to prevent hangs
                        if (sw.ElapsedMilliseconds > DetectionTimeoutMs)
                        {
                            Logger.Warning($"Chrome window detection timed out after {DetectionTimeoutMs}ms");
                            return false;
                        }
                        
                        try
                        {
                            return w.ClassName == ChromeClassName && 
                                   w.Process.ProcessName.ToLowerInvariant() == ChromeProcessName &&
                                   w.Visible &&
                                   !string.IsNullOrEmpty(w.Title);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error checking window: {ex.Message}");
                            return false;
                        }
                    })
                    .Select(w => new AppWindow(w.HWnd))
                    .ToList();
                
                stopwatchWindows.Stop();
                Logger.Info($"Found {chromeWindows.Count} Chrome windows in {stopwatchWindows.ElapsedMilliseconds}ms");

                foreach (var chromeWindow in chromeWindows)
                {
                    // Check timeout for overall process
                    if (sw.ElapsedMilliseconds > DetectionTimeoutMs)
                    {
                        Logger.Warning($"Chrome tab detection timed out after {DetectionTimeoutMs}ms");
                        break;
                    }
                    
                    try
                    {
                        // Get the active tab from the window title
                        var activeTabTitle = chromeWindow.Title;
                        
                        // Clean up the title by removing trailing " - Google Chrome"
                        if (activeTabTitle.EndsWith(" - Google Chrome"))
                        {
                            activeTabTitle = activeTabTitle.Substring(0, activeTabTitle.Length - 16);
                        }
                        
                        Logger.Debug($"Processing Chrome window: {activeTabTitle}");
                        
                        // Use UI Automation to find all tabs in this Chrome window
                        var stopwatchTabs = Stopwatch.StartNew();
                        var tabs = GetChromeTabs(chromeWindow.HWnd);
                        stopwatchTabs.Stop();
                        
                        if (tabs.Count > 0)
                        {
                            Logger.Debug($"Found {tabs.Count} tabs using UI Automation in {stopwatchTabs.ElapsedMilliseconds}ms");
                            // Add all tabs found using UI Automation
                            foreach (var tab in tabs)
                            {
                                chromeTabs.Add(tab);
                            }
                        }
                        else
                        {
                            Logger.Debug("UI Automation failed, trying fallback methods");
                            
                            // Try the keyboard simulation fallback approach if advanced detection is enabled
                            var tabTitles = new List<string>();
                            bool useAdvancedDetection = true; // Default value
                            
                            if (useAdvancedDetection)
                            {
                                var stopwatchFallback = Stopwatch.StartNew();
                                tabTitles = ChromeTabsEnumerator.EnumerateTabsByCycling(chromeWindow.HWnd);
                                stopwatchFallback.Stop();
                                Logger.Debug($"Keyboard enumeration took {stopwatchFallback.ElapsedMilliseconds}ms");
                            }
                            
                            if (tabTitles.Count > 0)
                            {
                                Logger.Debug($"Found {tabTitles.Count} tabs using keyboard simulation");
                                
                                // Add tabs found using keyboard simulation
                                int tabIndex = 0;
                                foreach (var tabTitle in tabTitles)
                                {
                                    string tabIdentifier = $"{chromeWindow.HWnd}:{tabIndex}:{tabTitle}";
                                    
                                    chromeTabs.Add(new ChromeTab
                                    {
                                        Title = tabTitle,
                                        BrowserHandle = chromeWindow.HWnd,
                                        TabIndex = tabIndex,
                                        TabIdentifier = tabIdentifier
                                    });
                                    
                                    tabIndex++;
                                }
                            }
                            else
                            {
                                Logger.Debug("All methods failed, adding just the active tab");
                                // Last resort - add just the active tab
                                string tabIdentifier = $"{chromeWindow.HWnd}:0:{activeTabTitle}";
                                
                                chromeTabs.Add(new ChromeTab
                                {
                                    Title = activeTabTitle,
                                    BrowserHandle = chromeWindow.HWnd,
                                    TabIndex = 0,
                                    TabIdentifier = tabIdentifier
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error processing Chrome window: {ex.Message}", ex);
                    }
                }
                
                sw.Stop();
                Logger.Info($"Total Chrome tabs found: {chromeTabs.Count} (Total time: {sw.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"Error getting Chrome tabs: {ex.Message}", ex);
            }

            return chromeTabs;
        }
        
        /// <summary>
        /// Use UI Automation to get all tabs in a Chrome window
        /// </summary>
        private List<ChromeTab> GetChromeTabs(IntPtr chromeWindowHandle)
        {
            Stopwatch sw = Stopwatch.StartNew();
            
            try
            {
                Logger.Debug($"Getting Chrome tabs for window handle: {chromeWindowHandle}");
                var tabs = ChromeTabAutomation.GetChromeTabs(chromeWindowHandle);
                sw.Stop();
                Logger.Debug($"ChromeTabAutomation.GetChromeTabs took {sw.ElapsedMilliseconds}ms");
                return tabs;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"Error in GetChromeTabs: {ex.Message}", ex);
                return new List<ChromeTab>();
            }
        }
        
        /// <summary>
        /// Switches to a specific Chrome tab using its identifier
        /// </summary>
        public bool SwitchToTab(string tabIdentifier, bool useAdvancedMethods = true)
        {
            Stopwatch sw = Stopwatch.StartNew();
            
            try
            {
                Logger.Debug($"Switching to Chrome tab: {tabIdentifier}");
                var result = ChromeTabSwitcher.SwitchToTab(tabIdentifier, useAdvancedMethods);
                sw.Stop();
                Logger.Debug($"ChromeTabSwitcher.SwitchToTab result: {result} in {sw.ElapsedMilliseconds}ms");
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error($"Error switching to Chrome tab: {ex.Message}", ex);
                return false;
            }
        }
    }
}