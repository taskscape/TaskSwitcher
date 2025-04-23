// To solve the hanging problem, you can implement a more radical approach
// by replacing the GetAllChromeTabs method with one that always returns an empty list.
// This will completely bypass the Chrome tab detection functionality while keeping
// the rest of the application working.

// Replace the GetAllChromeTabs method in ChromeTabWindow.cs with:

public static IEnumerable<ChromeTabWindow> GetAllChromeTabs()
{
    // Skip all tab detection to avoid hangs, just return empty collection
    TaskSwitcher.Core.Utilities.Logger.Info("Chrome tab detection is disabled to prevent hanging");
    return Enumerable.Empty<ChromeTabWindow>();
}

// Additionally, add a debug menu item to the system tray context menu that enables/disables
// Chrome tab detection for testing purposes. In MainWindow.cs:

// 1. Add this field to the MainWindow class:
private ToolStripMenuItem _enableChromeTabsMenuItem;

// 2. In the SetUpNotifyIcon method, add this to the ContextMenuStrip items:
_enableChromeTabsMenuItem = new ToolStripMenuItem("Enable Chrome Tabs (Debug)", null, 
    (s, e) => ToggleChromeTabsDebug())
{
    Checked = Settings.Default.IncludeBrowserTabs
};

// 3. Add this method to the MainWindow class:
private void ToggleChromeTabsDebug()
{
    // Toggle the setting
    Settings.Default.IncludeBrowserTabs = !Settings.Default.IncludeBrowserTabs;
    Settings.Default.Save();
    
    // Update menu item
    _enableChromeTabsMenuItem.Checked = Settings.Default.IncludeBrowserTabs;
    
    // Log the change
    TaskSwitcher.Core.Utilities.Logger.Info($"Chrome tabs detection {(Settings.Default.IncludeBrowserTabs ? "enabled" : "disabled")} via debug menu");
}
