// This file contains the code that should replace the existing MainWindow constructor
// Copy and paste this into MainWindow.xaml.cs

public MainWindow()
{
    // Initialize logging system first
    TaskSwitcher.Core.Utilities.Logger.Initialize();
    
    try
    {
        TaskSwitcher.Core.Utilities.Logger.Info("MainWindow initialization starting");
        
        InitializeComponent();

        SetUpKeyBindings();

        SetUpNotifyIcon();

        SetUpHotKey();

        SetUpAltTabHook();

        CheckForUpdates();

        Opacity = 0;
        
        // Temporarily disable browser tabs by default to prevent hangs
        Settings.Default.IncludeBrowserTabs = false;
        Settings.Default.Save();
        
        TaskSwitcher.Core.Utilities.Logger.Info("MainWindow initialization completed");
    }
    catch (Exception ex)
    {
        TaskSwitcher.Core.Utilities.Logger.Error("Error initializing MainWindow", ex);
        MessageBox.Show($"Error initializing application: {ex.Message}\nSee log file for details.", 
            "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}