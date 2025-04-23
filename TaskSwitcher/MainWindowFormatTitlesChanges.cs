// This file contains the code that should replace the existing FormatWindowTitles method
// Copy and paste this into MainWindow.xaml.cs

/// <summary>
/// Formats window titles in the background to improve UI responsiveness
/// </summary>
private void FormatWindowTitles(List<AppWindowViewModel> windows, System.Threading.CancellationToken cancellationToken)
{
    if (windows == null || windows.Count == 0)
    {
        TaskSwitcher.Core.Utilities.Logger.Debug("No windows to format titles for");
        return;
    }
    
    TaskSwitcher.Core.Utilities.Logger.Debug($"Formatting titles for {windows.Count} windows in batches");
    const int batchSize = 10;
    int processedCount = 0;
    Stopwatch sw = Stopwatch.StartNew();
    
    while (processedCount < windows.Count)
    {
        // Check for cancellation before processing each batch
        if (cancellationToken.IsCancellationRequested)
        {
            TaskSwitcher.Core.Utilities.Logger.Debug("Title formatting cancelled");
            return;
        }
        
        int end = Math.Min(processedCount + batchSize, windows.Count);
        var batch = new List<AppWindowViewModel>();
        
        // Collect the current batch
        for (int i = processedCount; i < end; i++)
        {
            batch.Add(windows[i]);
        }
        
        TaskSwitcher.Core.Utilities.Logger.Debug($"Formatting batch of {batch.Count} windows (progress: {processedCount}/{windows.Count})");
        
        try
        {
            // Update the UI on the dispatcher thread with timeout protection
            bool dispatcherCompleted = false;
            Task dispatcherTask = Dispatcher.InvokeAsync(() => 
            {
                try
                {
                    foreach (AppWindowViewModel window in batch)
                    {
                        try
                        {
                            window.FormattedTitle = new XamlHighlighter().Highlight(
                                new[] { new StringPart(window.WindowTitle) });
                            window.FormattedProcessTitle = new XamlHighlighter().Highlight(
                                new[] { new StringPart(window.ProcessTitle) });
                        }
                        catch (Exception ex)
                        {
                            TaskSwitcher.Core.Utilities.Logger.Error($"Error formatting window title: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    TaskSwitcher.Core.Utilities.Logger.Error($"Error in dispatcher batch processing: {ex.Message}", ex);
                }
            }, System.Windows.Threading.DispatcherPriority.Background).Task;
            
            // Add a timeout to dispatcher operations to prevent hangs
            if (!dispatcherTask.Wait(1000, cancellationToken))
            {
                TaskSwitcher.Core.Utilities.Logger.Warning("Dispatcher operation timed out while formatting window titles");
            }
            else
            {
                dispatcherCompleted = true;
            }
            
            if (!dispatcherCompleted)
            {
                TaskSwitcher.Core.Utilities.Logger.Warning(
                    "Incomplete dispatcher operation during title formatting. This might indicate a UI thread hang.");
            }
        }
        catch (Exception ex)
        {
            TaskSwitcher.Core.Utilities.Logger.Error($"Error in title formatting: {ex.Message}", ex);
        }
        
        processedCount = end;
        
        // Small delay to keep the UI responsive
        try
        {
            System.Threading.Thread.Sleep(10);
        }
        catch
        {
            // Ignore sleep exceptions
        }
    }
    
    sw.Stop();
    TaskSwitcher.Core.Utilities.Logger.Debug($"Window title formatting completed in {sw.ElapsedMilliseconds}ms");
}