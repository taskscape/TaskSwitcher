using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;
using TaskSwitcher.Core.Utilities;

namespace TaskSwitcher.Core.Browsers
{
    /// <summary>
    /// Enhanced Chrome tab detection using UI Automation with multiple strategies
    /// </summary>
    public class ChromeTabAutomation
    {
        // Add timeout to prevent UI Automation hanging
        private const int AutomationTimeoutMs = 3000; // 3 seconds
        
        /// <summary>
        /// Try multiple strategies to get Chrome tabs using UI Automation
        /// </summary>
        public static List<ChromeTabManager.ChromeTab> GetChromeTabs(IntPtr chromeHandle)
        {
            var tabs = new List<ChromeTabManager.ChromeTab>();
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                Logger.Debug("Attempting to find Chrome tabs using UI Automation");
                
                // Get the automation element for the Chrome window
                AutomationElement chromeWindow = null;
                
                try
                {
                    // Add timeout for this operation
                    var timeoutStopwatch = Stopwatch.StartNew();
                    chromeWindow = AutomationElement.FromHandle(chromeHandle);
                    timeoutStopwatch.Stop();
                    Logger.Debug($"AutomationElement.FromHandle took {timeoutStopwatch.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to get automation element for Chrome window: {ex.Message}");
                    return tabs;
                }
                
                if (chromeWindow == null)
                {
                    Logger.Warning("Failed to get automation element for Chrome window");
                    return tabs;
                }
                
                // Try different strategies to find tabs
                var tabsFindStopwatch = Stopwatch.StartNew();
                
                // Set a timeout for the first strategy
                bool firstStrategyTimedOut = false;
                tabs = TryFindTabsByTabControl(chromeWindow, chromeHandle, out firstStrategyTimedOut);
                
                if (firstStrategyTimedOut)
                {
                    Logger.Warning("First tab finding strategy timed out");
                }
                
                if (tabs.Count == 0 && !firstStrategyTimedOut && sw.ElapsedMilliseconds < AutomationTimeoutMs)
                {
                    Logger.Debug("Trying alternate approach: searching for tab items directly");
                    bool secondStrategyTimedOut = false;
                    tabs = TryFindTabsByDirectSearch(chromeWindow, chromeHandle, out secondStrategyTimedOut);
                    
                    if (secondStrategyTimedOut)
                    {
                        Logger.Warning("Second tab finding strategy timed out");
                    }
                }
                
                if (tabs.Count == 0 && sw.ElapsedMilliseconds < AutomationTimeoutMs)
                {
                    Logger.Debug("Trying last approach: searching for Chrome tab strip");
                    bool thirdStrategyTimedOut = false;
                    tabs = TryFindTabsByChromeTabStrip(chromeWindow, chromeHandle, out thirdStrategyTimedOut);
                    
                    if (thirdStrategyTimedOut)
                    {
                        Logger.Warning("Third tab finding strategy timed out");
                    }
                }
                
                tabsFindStopwatch.Stop();
                Logger.Debug($"Tab finding strategies took {tabsFindStopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in GetChromeTabs: {ex.Message}", ex);
            }
            
            sw.Stop();
            Logger.Debug($"Total tabs found using UI Automation: {tabs.Count} in {sw.ElapsedMilliseconds}ms");
            return tabs;
        }
        
        /// <summary>
        /// First strategy: Try to find tab items via a Tab control
        /// </summary>
        private static List<ChromeTabManager.ChromeTab> TryFindTabsByTabControl(
            AutomationElement root, IntPtr chromeHandle, out bool timedOut)
        {
            var tabs = new List<ChromeTabManager.ChromeTab>();
            Stopwatch sw = Stopwatch.StartNew();
            timedOut = false;
            
            try
            {
                Logger.Debug("Finding tabs by Tab control");
                
                // Find tab controls
                var tabControlStopwatch = Stopwatch.StartNew();
                AutomationElementCollection tabControls = null;
                
                try
                {
                    tabControls = root.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tab));
                    
                    tabControlStopwatch.Stop();
                    Logger.Debug($"Finding tab controls took {tabControlStopwatch.ElapsedMilliseconds}ms, found {tabControls?.Count ?? 0} controls");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error finding tab controls: {ex.Message}");
                    return tabs;
                }
                
                if (tabControls == null || tabControls.Count == 0)
                {
                    Logger.Debug("No tab controls found");
                    return tabs;
                }
                
                // Check if we're approaching the timeout
                if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                {
                    timedOut = true;
                    Logger.Warning($"Tab control search timed out after {sw.ElapsedMilliseconds}ms");
                    return tabs;
                }
                
                foreach (AutomationElement tabControl in tabControls)
                {
                    // Check if we're approaching timeout
                    if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                    {
                        timedOut = true;
                        Logger.Warning($"Tab item search timed out after {sw.ElapsedMilliseconds}ms");
                        break;
                    }
                    
                    // Find tab items within this tab control
                    var tabItemsStopwatch = Stopwatch.StartNew();
                    AutomationElementCollection tabItems = null;
                    
                    try
                    {
                        tabItems = tabControl.FindAll(
                            TreeScope.Children,
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
                        
                        tabItemsStopwatch.Stop();
                        Logger.Debug($"Finding tab items took {tabItemsStopwatch.ElapsedMilliseconds}ms, found {tabItems?.Count ?? 0} items");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error finding tab items: {ex.Message}");
                        continue;
                    }
                    
                    if (tabItems == null || tabItems.Count == 0)
                    {
                        Logger.Debug("No tab items found in this tab control");
                        continue;
                    }
                    
                    int tabIndex = 0;
                    foreach (AutomationElement tabItem in tabItems)
                    {
                        // Check for timeout
                        if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                        {
                            timedOut = true;
                            Logger.Warning($"Processing tab items timed out after {sw.ElapsedMilliseconds}ms");
                            break;
                        }
                        
                        try
                        {
                            // Try to get name from the name property
                            string tabName = tabItem.Current.Name;
                            
                            // Clean up the name
                            if (string.IsNullOrEmpty(tabName))
                            {
                                Logger.Debug($"Tab item at index {tabIndex} has empty name, skipping");
                                continue;
                            }

                            // Remove "Close Tab" button text if present
                            if (tabName.EndsWith(" Close Tab"))
                            {
                                tabName = tabName.Substring(0, tabName.Length - 10);
                            }
                            
                            // Create unique identifier
                            string tabIdentifier = $"{chromeHandle}:{tabIndex}:{tabName}";
                            
                            Logger.Debug($"Found tab: {tabName} (index: {tabIndex})");
                            
                            tabs.Add(new ChromeTabManager.ChromeTab
                            {
                                Title = tabName,
                                BrowserHandle = chromeHandle,
                                TabIndex = tabIndex,
                                TabIdentifier = tabIdentifier
                            });
                            
                            tabIndex++;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error processing tab item: {ex.Message}");
                        }
                    }
                    
                    // We found some tabs, so return them
                    if (tabs.Count > 0)
                    {
                        Logger.Debug($"Successfully found {tabs.Count} tabs in tab control");
                        return tabs;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in TryFindTabsByTabControl: {ex.Message}", ex);
            }
            
            Logger.Debug($"TryFindTabsByTabControl took {sw.ElapsedMilliseconds}ms, found {tabs.Count} tabs");
            return tabs;
        }
        
        /// <summary>
        /// Second strategy: Try to find tab items directly without requiring a tab control parent
        /// </summary>
        private static List<ChromeTabManager.ChromeTab> TryFindTabsByDirectSearch(
            AutomationElement root, IntPtr chromeHandle, out bool timedOut)
        {
            var tabs = new List<ChromeTabManager.ChromeTab>();
            Stopwatch sw = Stopwatch.StartNew();
            timedOut = false;
            
            try
            {
                Logger.Debug("Finding tabs by direct search");
                
                // Find all tab items directly
                var tabItemsStopwatch = Stopwatch.StartNew();
                AutomationElementCollection tabItems = null;
                
                try
                {
                    tabItems = root.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
                    
                    tabItemsStopwatch.Stop();
                    Logger.Debug($"Direct search for tab items took {tabItemsStopwatch.ElapsedMilliseconds}ms, found {tabItems?.Count ?? 0} items");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in direct tab item search: {ex.Message}");
                    return tabs;
                }
                
                if (tabItems == null || tabItems.Count == 0)
                {
                    Logger.Debug("No tab items found via direct search");
                    return tabs;
                }
                
                int tabIndex = 0;
                foreach (AutomationElement tabItem in tabItems)
                {
                    // Check for timeout
                    if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                    {
                        timedOut = true;
                        Logger.Warning($"Direct search tab processing timed out after {sw.ElapsedMilliseconds}ms");
                        break;
                    }
                    
                    try
                    {
                        string tabName = tabItem.Current.Name;
                        
                        // Skip if no name
                        if (string.IsNullOrEmpty(tabName))
                        {
                            Logger.Debug($"Tab item at index {tabIndex} has empty name, skipping");
                            continue;
                        }
                        
                        // Check if this looks like a tab (has a name and is clickable)
                        if (tabItem.Current.IsEnabled)
                        {
                            // Clean up name
                            if (tabName.EndsWith(" Close Tab"))
                            {
                                tabName = tabName.Substring(0, tabName.Length - 10);
                            }
                            
                            // Create unique identifier
                            string tabIdentifier = $"{chromeHandle}:{tabIndex}:{tabName}";
                            
                            Logger.Debug($"Found tab via direct search: {tabName} (index: {tabIndex})");
                            
                            tabs.Add(new ChromeTabManager.ChromeTab
                            {
                                Title = tabName,
                                BrowserHandle = chromeHandle,
                                TabIndex = tabIndex,
                                TabIdentifier = tabIdentifier
                            });
                            
                            tabIndex++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error processing tab item (direct): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in TryFindTabsByDirectSearch: {ex.Message}", ex);
            }
            
            Logger.Debug($"TryFindTabsByDirectSearch took {sw.ElapsedMilliseconds}ms, found {tabs.Count} tabs");
            return tabs;
        }
        
        /// <summary>
        /// Third strategy: Try to find tabs by looking for Chrome-specific tab strip controls
        /// </summary>
        private static List<ChromeTabManager.ChromeTab> TryFindTabsByChromeTabStrip(
            AutomationElement root, IntPtr chromeHandle, out bool timedOut)
        {
            var tabs = new List<ChromeTabManager.ChromeTab>();
            Stopwatch sw = Stopwatch.StartNew();
            timedOut = false;
            
            try
            {
                Logger.Debug("Finding tabs by Chrome tab strip");
                
                // Chrome tab strips are often implemented as a toolbar or custom pane
                AutomationElement tabStrip = null;
                
                // Try finding the toolbar first
                var toolbarStopwatch = Stopwatch.StartNew();
                AutomationElementCollection toolbars = null;
                
                try
                {
                    toolbars = root.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar));
                    
                    toolbarStopwatch.Stop();
                    Logger.Debug($"Toolbar search took {toolbarStopwatch.ElapsedMilliseconds}ms, found {toolbars?.Count ?? 0} toolbars");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error searching for toolbars: {ex.Message}");
                }
                
                // Check for timeout
                if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                {
                    timedOut = true;
                    Logger.Warning($"Toolbar search timed out after {sw.ElapsedMilliseconds}ms");
                    return tabs;
                }
                
                // Check if we found any toolbars
                if (toolbars != null && toolbars.Count > 0)
                {
                    foreach (AutomationElement toolbar in toolbars)
                    {
                        // Check for timeout
                        if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                        {
                            timedOut = true;
                            Logger.Warning($"Toolbar processing timed out after {sw.ElapsedMilliseconds}ms");
                            break;
                        }
                        
                        try
                        {
                            // Check if this toolbar contains what look like tabs
                            var childrenStopwatch = Stopwatch.StartNew();
                            AutomationElementCollection children = null;
                            
                            try
                            {
                                children = toolbar.FindAll(TreeScope.Children, Condition.TrueCondition);
                                childrenStopwatch.Stop();
                                Logger.Debug($"Finding toolbar children took {childrenStopwatch.ElapsedMilliseconds}ms, found {children?.Count ?? 0} children");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Error finding toolbar children: {ex.Message}");
                                continue;
                            }
                            
                            if (children == null || children.Count == 0)
                            {
                                continue;
                            }
                            
                            bool containsTabs = false;
                            
                            foreach (AutomationElement child in children)
                            {
                                // Check for timeout
                                if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                                {
                                    timedOut = true;
                                    Logger.Warning($"Toolbar children processing timed out after {sw.ElapsedMilliseconds}ms");
                                    break;
                                }
                                
                                try
                                {
                                    // Look for elements that might be tabs
                                    if (!string.IsNullOrEmpty(child.Current.Name) && 
                                        (child.Current.ControlType == ControlType.Button || 
                                         child.Current.ControlType == ControlType.Custom))
                                    {
                                        containsTabs = true;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"Error checking toolbar child: {ex.Message}");
                                }
                            }
                            
                            if (containsTabs)
                            {
                                tabStrip = toolbar;
                                Logger.Debug("Found potential tab strip in toolbar");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error processing toolbar: {ex.Message}");
                        }
                    }
                }
                
                // If we couldn't find a toolbar, try to find a pane that might be the tab strip
                if (tabStrip == null && sw.ElapsedMilliseconds < AutomationTimeoutMs)
                {
                    Logger.Debug("No tab strip found in toolbars, trying panes");
                    
                    var panesStopwatch = Stopwatch.StartNew();
                    AutomationElementCollection panes = null;
                    
                    try
                    {
                        panes = root.FindAll(
                            TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane));
                        
                        panesStopwatch.Stop();
                        Logger.Debug($"Pane search took {panesStopwatch.ElapsedMilliseconds}ms, found {panes?.Count ?? 0} panes");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error searching for panes: {ex.Message}");
                    }
                    
                    // Check for timeout
                    if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                    {
                        timedOut = true;
                        Logger.Warning($"Pane search timed out after {sw.ElapsedMilliseconds}ms");
                        return tabs;
                    }
                    
                    if (panes != null && panes.Count > 0)
                    {
                        foreach (AutomationElement pane in panes)
                        {
                            // Check for timeout
                            if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                            {
                                timedOut = true;
                                Logger.Warning($"Pane processing timed out after {sw.ElapsedMilliseconds}ms");
                                break;
                            }
                            
                            try
                            {
                                // Check if this pane contains what look like tabs
                                var childrenStopwatch = Stopwatch.StartNew();
                                AutomationElementCollection children = null;
                                
                                try
                                {
                                    children = pane.FindAll(TreeScope.Children, Condition.TrueCondition);
                                    childrenStopwatch.Stop();
                                    Logger.Debug($"Finding pane children took {childrenStopwatch.ElapsedMilliseconds}ms, found {children?.Count ?? 0} children");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"Error finding pane children: {ex.Message}");
                                    continue;
                                }
                                
                                if (children == null || children.Count == 0)
                                {
                                    continue;
                                }
                                
                                bool containsTabs = false;
                                
                                foreach (AutomationElement child in children)
                                {
                                    // Check for timeout
                                    if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                                    {
                                        timedOut = true;
                                        Logger.Warning($"Pane children processing timed out after {sw.ElapsedMilliseconds}ms");
                                        break;
                                    }
                                    
                                    try
                                    {
                                        if (!string.IsNullOrEmpty(child.Current.Name) && 
                                            (child.Current.ControlType == ControlType.Button || 
                                             child.Current.ControlType == ControlType.Custom))
                                        {
                                            containsTabs = true;
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error($"Error checking pane child: {ex.Message}");
                                    }
                                }
                                
                                if (containsTabs)
                                {
                                    tabStrip = pane;
                                    Logger.Debug("Found potential tab strip in pane");
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Error processing pane: {ex.Message}");
                            }
                        }
                    }
                }
                
                // Process tab strip if found
                if (tabStrip != null)
                {
                    Logger.Debug("Processing found tab strip");
                    
                    // Get all children of the tab strip
                    var tabItemsStopwatch = Stopwatch.StartNew();
                    AutomationElementCollection tabItems = null;
                    
                    try
                    {
                        tabItems = tabStrip.FindAll(TreeScope.Children, Condition.TrueCondition);
                        tabItemsStopwatch.Stop();
                        Logger.Debug($"Finding tab strip children took {tabItemsStopwatch.ElapsedMilliseconds}ms, found {tabItems?.Count ?? 0} potential tabs");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error finding tab strip children: {ex.Message}");
                        return tabs;
                    }
                    
                    if (tabItems == null || tabItems.Count == 0)
                    {
                        Logger.Debug("No tab items found in tab strip");
                        return tabs;
                    }
                    
                    int tabIndex = 0;
                    foreach (AutomationElement item in tabItems)
                    {
                        // Check for timeout
                        if (sw.ElapsedMilliseconds > AutomationTimeoutMs)
                        {
                            timedOut = true;
                            Logger.Warning($"Tab strip item processing timed out after {sw.ElapsedMilliseconds}ms");
                            break;
                        }
                        
                        try
                        {
                            string tabName = item.Current.Name;
                            
                            // Skip if no name or doesn't look like a tab
                            if (string.IsNullOrEmpty(tabName) || 
                                tabName.Equals("New tab", StringComparison.OrdinalIgnoreCase) ||
                                tabName.Equals("List all tabs", StringComparison.OrdinalIgnoreCase) ||
                                tabName.Equals("Customize", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            
                            // Clean up name
                            if (tabName.EndsWith(" Close Tab"))
                            {
                                tabName = tabName.Substring(0, tabName.Length - 10);
                            }
                            
                            // Create unique identifier
                            string tabIdentifier = $"{chromeHandle}:{tabIndex}:{tabName}";
                            
                            Logger.Debug($"Found tab in tab strip: {tabName} (index: {tabIndex})");
                            
                            tabs.Add(new ChromeTabManager.ChromeTab
                            {
                                Title = tabName,
                                BrowserHandle = chromeHandle,
                                TabIndex = tabIndex,
                                TabIdentifier = tabIdentifier
                            });
                            
                            tabIndex++;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error processing tab strip item: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in TryFindTabsByChromeTabStrip: {ex.Message}", ex);
            }
            
            Logger.Debug($"TryFindTabsByChromeTabStrip took {sw.ElapsedMilliseconds}ms, found {tabs.Count} tabs");
            return tabs;
        }
    }
}