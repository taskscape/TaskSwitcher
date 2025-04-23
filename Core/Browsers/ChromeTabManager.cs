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
                        AutomationElement root = AutomationElement.FromHandle(chromeWindow.HWnd);
                        Condition condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
                        var tabs = root.FindAll(TreeScope.Descendants, condition);

                        int tabIndex = 0;
                        foreach (AutomationElement tabitem in tabs)
                        {
                            Console.WriteLine(tabitem.Current.Name);
                            string tabIdentifier = $"{chromeWindow.HWnd}:{tabIndex}:{tabitem.Current.Name}";
                            chromeTabs.Add(new ChromeTab
                            {
                                Title = tabitem.Current.Name,
                                BrowserHandle = chromeWindow.HWnd,
                                TabIndex = tabIndex,
                                TabIdentifier = tabIdentifier
                            });
                            tabIndex++;
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