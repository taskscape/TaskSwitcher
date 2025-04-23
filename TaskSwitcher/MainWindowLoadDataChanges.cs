// This file contains the code that should replace the existing LoadData method
// Copy and paste this into MainWindow.xaml.cs

/// <summary>
/// Populates the window list with the current running windows.
/// </summary>
private async void LoadData(InitialFocus focus)
{
    Stopwatch performanceTimer = Stopwatch.StartNew();
    TaskSwitcher.Core.Utilities.Logger.Info("LoadData started");

    try
    {
        // Cancel any previous loading operation
        if (_loadCancellationTokenSource != null)
        {
            TaskSwitcher.Core.Utilities.Logger.Debug("Cancelling previous load operation");
            _loadCancellationTokenSource.Cancel();
            _loadCancellationTokenSource.Dispose();
        }
        
        // Create a new cancellation token for this operation
        _loadCancellationTokenSource = new System.Threading.CancellationTokenSource();
        
        // Create a timeout to avoid hangs - cancel after 10 seconds
        _loadCancellationTokenSource.CancelAfter(10000);
        
        var cancellationToken = _loadCancellationTokenSource.Token;
        
        // Initial UI feedback - could show a loading indicator here
        TaskSwitcher.Core.Utilities.Logger.Debug("Setting up window finder");
        
        // Add timeout to Chrome tab detection
        if (Settings.Default.IncludeBrowserTabs)
        {
            TaskSwitcher.Core.Utilities.Logger.Info("Browser tabs detection enabled");
            
            // Configure Chrome tab detection
            Core.Browsers.ChromeTabWindow.UseAdvancedTabSwitching = Settings.Default.UseAdvancedTabDetection;
            
            TaskSwitcher.Core.Utilities.Logger.Debug($"Chrome tab settings: UseAdvancedTabDetection={Settings.Default.UseAdvancedTabDetection}");
            
            try
            {
                var chromeDetectionSw = Stopwatch.StartNew();
                var chromeTabs = Core.Browsers.ChromeTabWindow.GetAllChromeTabs().ToList();
                chromeDetectionSw.Stop();
                
                TaskSwitcher.Core.Utilities.Logger.Info($"Found {chromeTabs.Count} Chrome tabs in {chromeDetectionSw.ElapsedMilliseconds}ms");
                
                foreach (var tab in chromeTabs)
                {
                    TaskSwitcher.Core.Utilities.Logger.Debug($"Tab: {tab.DisplayTitle} (ID: {tab.TabIdentifier})");
                }
            }
            catch (Exception ex)
            {
                TaskSwitcher.Core.Utilities.Logger.Error("Error detecting Chrome tabs", ex);
            }
        }
        else
        {
            TaskSwitcher.Core.Utilities.Logger.Info("Browser tabs detection disabled");
        }
        
        WindowFinder windowFinder = new WindowFinder(Settings.Default.IncludeBrowserTabs);
        
        // Perform window loading on a background thread
        var windowsLoadSw = Stopwatch.StartNew();
        TaskSwitcher.Core.Utilities.Logger.Debug("Starting window loading");
        
        _unfilteredWindowList = await Task.Run(() => 
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                TaskSwitcher.Core.Utilities.Logger.Debug("Retrieving windows from WindowFinder");
                var windows = windowFinder.GetWindowsLazy().Select(window => new AppWindowViewModel(window)).ToList();
                TaskSwitcher.Core.Utilities.Logger.Debug($"Retrieved {windows.Count} windows");
                return windows;
            }
            catch (Exception ex)
            {
                TaskSwitcher.Core.Utilities.Logger.Error($"Error getting windows: {ex.Message}", ex);
                throw;
            }
        }, cancellationToken);
        
        windowsLoadSw.Stop();
        TaskSwitcher.Core.Utilities.Logger.Info($"Window loading completed in {windowsLoadSw.ElapsedMilliseconds}ms, found {_unfilteredWindowList?.Count ?? 0} windows");
        
        // Check for cancellation before proceeding
        cancellationToken.ThrowIfCancellationRequested();
        
        AppWindowViewModel firstWindow = _unfilteredWindowList.FirstOrDefault();
        bool foregroundWindowMovedToBottom = false;
        
        // Move first window to the bottom of the list if it's related to the foreground window
        if (firstWindow != null && AreWindowsRelated(firstWindow.AppWindow, _foregroundWindow))
        {
            TaskSwitcher.Core.Utilities.Logger.Debug("Moving foreground window to bottom of list");
            _unfilteredWindowList.RemoveAt(0);
            _unfilteredWindowList.Add(firstWindow);
            foregroundWindowMovedToBottom = true;
        }
        
        // Check for cancellation before updating the UI
        cancellationToken.ThrowIfCancellationRequested();
        
        TaskSwitcher.Core.Utilities.Logger.Debug("Creating filtered window list");
        _filteredWindowList = new ObservableCollection<AppWindowViewModel>(_unfilteredWindowList);
        _windowCloser = new WindowCloser();
        
        // Update UI before starting background formatting
        TaskSwitcher.Core.Utilities.Logger.Debug("Updating UI with window list");
        lb.DataContext = null;
        lb.DataContext = _filteredWindowList;
        
        TaskSwitcher.Core.Utilities.Logger.Debug("Focusing and positioning UI elements");
        FocusItemInList(focus, foregroundWindowMovedToBottom);
        tb.Clear();
        tb.Focus();
        CenterWindow();
        ScrollSelectedItemIntoView();
        
        // Process window title formatting in the background for better UI responsiveness
        // Use the same cancellation token to ensure formatting is canceled if a new load starts
        TaskSwitcher.Core.Utilities.Logger.Debug("Starting background formatting of window titles");
        var formattingSw = Stopwatch.StartNew();
        
        await Task.Run(() => FormatWindowTitles(_unfilteredWindowList, cancellationToken), cancellationToken);
        
        formattingSw.Stop();
        TaskSwitcher.Core.Utilities.Logger.Debug($"Window title formatting completed in {formattingSw.ElapsedMilliseconds}ms");
        
        performanceTimer.Stop();
        TaskSwitcher.Core.Utilities.Logger.Info($"LoadData completed successfully in {performanceTimer.ElapsedMilliseconds}ms");
    }
    catch (TaskCanceledException)
    {
        performanceTimer.Stop();
        TaskSwitcher.Core.Utilities.Logger.Warning($"LoadData was cancelled after {performanceTimer.ElapsedMilliseconds}ms");
        // Expected when operation is canceled
    }
    catch (OperationCanceledException)
    {
        performanceTimer.Stop();
        TaskSwitcher.Core.Utilities.Logger.Warning($"LoadData operation was cancelled after {performanceTimer.ElapsedMilliseconds}ms");
        // Expected when operation is canceled
    }
    catch (Exception ex)
    {
        performanceTimer.Stop();
        // Log unexpected errors
        TaskSwitcher.Core.Utilities.Logger.Error($"Error during window loading after {performanceTimer.ElapsedMilliseconds}ms: {ex.Message}", ex);
        
        MessageBox.Show($"Error loading windows: {ex.Message}\nCheck log file for details.",
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}