using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace TaskSwitcher.Core.Browsers
{
    /// <summary>
    /// Enhanced Chrome tab switching using multiple strategies
    /// </summary>
    public class ChromeTabSwitcher
    {
        /// <summary>
        /// Attempts to switch to a specific tab in Chrome using multiple strategies
        /// </summary>
        public static bool SwitchToTab(string tabIdentifier, bool useAdvancedMethods = true)
        {
            try
            {
                // Parse the tab identifier
                var parts = tabIdentifier.Split(new[] { ':' }, 3);
                if (parts.Length < 2) return false;
                
                // Get the Chrome window handle
                IntPtr hwnd = (IntPtr)long.Parse(parts[0]);
                
                // Get the tab index if available
                int tabIndex = 0;
                bool hasIndex = parts.Length > 2 && int.TryParse(parts[1], out tabIndex);
                
                // Get the tab title
                string tabTitle = parts.Length > 2 ? parts[2] : parts[1];
                
                Debug.WriteLine($"Switching to Chrome tab: {tabTitle} (Index: {tabIndex}, Handle: {hwnd})");
                
                // First, activate the Chrome window
                WinApi.SetForegroundWindow(hwnd);
                Thread.Sleep(100); // Give Chrome time to respond
                
                // If we have a tab index, try using UI Automation to switch to the tab
                if (hasIndex && tabIndex > 0)
                {
                    if (TrySwitchTabUsingUIAutomation(hwnd, tabIndex, tabTitle))
                    {
                        Debug.WriteLine("Successfully switched using UI Automation");
                        return true;
                    }
                }
                
                // If UI Automation didn't work or we don't have an index, try using keyboard shortcuts
                if (useAdvancedMethods)
                {
                    if (TrySwitchTabUsingKeyboardShortcuts(hwnd, tabIndex, tabTitle))
                    {
                        Debug.WriteLine("Successfully switched using keyboard shortcuts");
                        return true;
                    }
                }
                
                // If all else fails, at least the Chrome window is now active
                Debug.WriteLine("Failed to switch to specific tab, but Chrome window is now active");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error switching to Chrome tab: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Try to switch to a tab using UI Automation
        /// </summary>
        private static bool TrySwitchTabUsingUIAutomation(IntPtr hwnd, int tabIndex, string tabTitle)
        {
            try
            {
                // Get automation element for the Chrome window
                AutomationElement chromeWindow = AutomationElement.FromHandle(hwnd);
                if (chromeWindow == null) return false;
                
                // First try to find tab item by name (most reliable)
                AutomationElement tabItem = FindTabByName(chromeWindow, tabTitle);
                
                // If we couldn't find by name, try to find by index
                if (tabItem == null && tabIndex >= 0)
                {
                    tabItem = FindTabByIndex(chromeWindow, tabIndex);
                }
                
                // If we found a tab, try to click it
                if (tabItem != null)
                {
                    // Try using InvokePattern first
                    try
                    {
                        var invokePattern = tabItem.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                        if (invokePattern != null)
                        {
                            invokePattern.Invoke();
                            return true;
                        }
                    }
                    catch { /* If this fails, we'll try SelectionItemPattern next */ }
                    
                    // Try using SelectionItemPattern
                    try
                    {
                        var selectionPattern = tabItem.GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
                        if (selectionPattern != null)
                        {
                            selectionPattern.Select();
                            return true;
                        }
                    }
                    catch { /* If this fails too, we'll continue to the next method */ }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TrySwitchTabUsingUIAutomation: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Find a tab item by its name/title
        /// </summary>
        private static AutomationElement FindTabByName(AutomationElement root, string tabName)
        {
            try
            {
                // Try direct search first
                Condition nameCondition = new PropertyCondition(AutomationElement.NameProperty, tabName);
                Condition tabItemCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
                Condition combinedCondition = new AndCondition(nameCondition, tabItemCondition);
                
                AutomationElement tabItem = root.FindFirst(TreeScope.Descendants, combinedCondition);
                if (tabItem != null) return tabItem;
                
                // If direct search failed, try searching for tab items and compare names
                var tabItems = root.FindAll(TreeScope.Descendants, tabItemCondition);
                
                foreach (AutomationElement item in tabItems)
                {
                    string itemName = item.Current.Name;
                    
                    // Clean up name
                    if (itemName.EndsWith(" Close Tab"))
                    {
                        itemName = itemName.Substring(0, itemName.Length - 10);
                    }
                    
                    // Check if this is the tab we're looking for
                    if (string.Equals(itemName, tabName, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindTabByName: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Find a tab item by its index position
        /// </summary>
        private static AutomationElement FindTabByIndex(AutomationElement root, int tabIndex)
        {
            try
            {
                // Find the tab strip first
                var tabControl = root.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tab));
                
                if (tabControl == null)
                {
                    // Try to find tab items directly
                    var tabItems = root.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
                    
                    if (tabItems.Count > tabIndex)
                    {
                        return tabItems[tabIndex];
                    }
                }
                else
                {
                    // Find tab items within the tab control
                    var tabItems = tabControl.FindAll(
                        TreeScope.Children,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
                    
                    if (tabItems.Count > tabIndex)
                    {
                        return tabItems[tabIndex];
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindTabByIndex: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Try to switch to a specific tab using keyboard shortcuts
        /// </summary>
        private static bool TrySwitchTabUsingKeyboardShortcuts(IntPtr hwnd, int tabIndex, string tabTitle)
        {
            try
            {
                // Make sure Chrome window is active
                if (!WinApi.SetForegroundWindow(hwnd))
                    return false;
                
                Thread.Sleep(100); // Give Chrome time to respond
                
                // If we have a tab index, try using Ctrl+1-9 shortcuts
                if (tabIndex >= 0 && tabIndex < 9)
                {
                    // Press Ctrl+[tabIndex+1] (Ctrl+1 for first tab, Ctrl+2 for second, etc.)
                    SendKeys.SendWait("^{" + (tabIndex + 1) + "}");
                    return true;
                }
                else
                {
                    // Try to use Ctrl+Tab to cycle to the tab
                    int maxCycles = 30; // Safety limit
                    
                    // First, get the initial title to know when we've gone through all tabs
                    string initialTitle = GetChromeWindowTitle(hwnd);
                    string currentTitle = initialTitle;
                    
                    for (int i = 0; i < maxCycles; i++)
                    {
                        // Send Ctrl+Tab to go to next tab
                        SendKeys.SendWait("^{TAB}");
                        Thread.Sleep(50);
                        
                        // Get the new title
                        string newTitle = GetChromeWindowTitle(hwnd);
                        
                        // If we're back at the start or have found the tab, stop
                        if (i > 0 && (newTitle == initialTitle || IsTabMatch(newTitle, tabTitle)))
                        {
                            return IsTabMatch(newTitle, tabTitle);
                        }
                        
                        currentTitle = newTitle;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TrySwitchTabUsingKeyboardShortcuts: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get the current window title of a Chrome window
        /// </summary>
        private static string GetChromeWindowTitle(IntPtr hwnd)
        {
            try
            {
                var proc = Process.GetProcessById(WinApi.GetWindowProcessId(hwnd));
                string title = proc.MainWindowTitle;
                
                // Clean up the title
                if (title.EndsWith(" - Google Chrome"))
                {
                    title = title.Substring(0, title.Length - 16);
                }
                
                return title;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Check if the current Chrome window title matches the target tab title
        /// </summary>
        private static bool IsTabMatch(string windowTitle, string tabTitle)
        {
            return string.Equals(windowTitle, tabTitle, StringComparison.OrdinalIgnoreCase);
        }
    }
}