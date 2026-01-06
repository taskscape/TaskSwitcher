using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ManagedWinapi;
using ManagedWinapi.Windows;
using TaskSwitcher.Core;
using TaskSwitcher.Core.Matchers;
using TaskSwitcher.Properties;
using Application = System.Windows.Application;
using // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
MenuItem = // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
System.Windows.Forms.ToolStripMenuItem;
using MessageBox = System.Windows.MessageBox;

namespace TaskSwitcher
{
    public partial class MainWindow : Window, IDisposable
    {
        private static readonly HttpClient HttpClient = new();

        private WindowCloser _windowCloser;
        private List<AppWindowViewModel> _unfilteredWindowList;
        private ObservableCollection<AppWindowViewModel> _filteredWindowList;
        private NotifyIcon _notifyIcon;
        private HotKey _hotkey;

        public static readonly RoutedUICommand CloseWindowCommand = new();
        public static readonly RoutedUICommand SwitchToWindowCommand = new();
        public static readonly RoutedUICommand ScrollListDownCommand = new();
        public static readonly RoutedUICommand ScrollListUpCommand = new();
        private OptionsWindow _optionsWindow;
        private AboutWindow _aboutWindow;
        private AltTabHook _altTabHook;
        private SystemWindow _foregroundWindow;
        private bool _altTabAutoSwitch;

        public MainWindow()
        {
            InitializeComponent();

            SetUpKeyBindings();

            SetUpNotifyIcon();

            SetUpHotKey();

            SetUpAltTabHook();

            CheckForUpdates();

            Opacity = 0;
        }

        /// =================================

        #region Private Methods

        /// =================================

        private void SetUpKeyBindings()
        {
            // Enter and Esc bindings are not executed before the keys have been released.
            // This is done to prevent that the window being focused after the key presses
            // to get 'KeyUp' messages.

            KeyDown += (sender, args) =>
            {
                // Opacity is set to 0 right away so it appears that action has been taken right away...
                if (args.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Opacity = 0;
                }
                else if (args.Key == Key.Escape)
                {
                    Opacity = 0;
                }
                else if (args.SystemKey == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    _altTabAutoSwitch = false;
                    tb.Text = "";
                    tb.IsEnabled = true;
                    tb.Focus();
                }
            };

            KeyUp += (sender, args) =>
            {
                // ... But only when the keys are release, the action is actually executed
                if (args.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Switch();
                }
                else if (args.Key == Key.Escape)
                {
                    HideWindow();
                }
                else if (args.SystemKey == Key.LeftAlt && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Switch();
                }
                else if (args.Key == Key.LeftAlt && _altTabAutoSwitch)
                {
                    Switch();
                }
            };
        }

        private void SetUpHotKey()
        {
            _hotkey = new HotKey();
            _hotkey.LoadSettings();

            Application.Current.Properties["hotkey"] = _hotkey;

            _hotkey.HotkeyPressed += hotkey_HotkeyPressed;
            try
            {
                _hotkey.Enabled = Settings.Default.EnableHotKey;
            }
            catch (HotkeyAlreadyInUseException)
            {
                string boxText = "The current hotkey for activating TaskSwitcher is in use by another program." +
                              Environment.NewLine +
                              Environment.NewLine +
                              "You can change the hotkey by right-clicking the TaskSwitcher icon in the system tray and choosing 'Options'.";
                MessageBox.Show(boxText, "Hotkey already in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SetUpAltTabHook()
        {
            _altTabHook = new AltTabHook();
            _altTabHook.Pressed += AltTabPressed;
        }

        private void SetUpNotifyIcon()
        {
            Icon icon = Properties.Resources.icon;

            // TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            ToolStripMenuItem runOnStartupMenuItem = new("Run on Startup", null, (s, e) => RunOnStartup(s as ToolStripMenuItem))
            {
                Checked = new AutoStart().IsEnabled
            };

            // TODO ContextMenu is no longer supported. Use ContextMenuStrip instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
            _notifyIcon = new NotifyIcon
            {
                Text = "TaskSwitcher",
                Icon = icon,
                Visible = true,

                ContextMenuStrip = new ContextMenuStrip
                {
                    Items =
    {
        new ToolStripMenuItem("Options",
                              null,
                              (s, e) => Options()),
        runOnStartupMenuItem,
        new ToolStripMenuItem("About", null, (s, e) => About()),
        new ToolStripMenuItem("Exit", null, (s, e) => Quit())
    }
                }

            };
        }

        private static void RunOnStartup(// TODO MenuItem is no longer supported. Use ToolStripMenuItem instead. For more details see https://docs.microsoft.com/en-us/dotnet/core/compatibility/winforms#removed-controls
MenuItem menuItem)
        {
            try
            {
                AutoStart autoStart = new()
                {
                    IsEnabled = !menuItem.Checked
                };
                menuItem.Checked = autoStart.IsEnabled;
            }
            catch (AutoStartException e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void CheckForUpdates()
        {
            Version currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            if (currentVersion == new Version(0, 0, 0, 0))
            {
                return;
            }

            DispatcherTimer timer = new();

            timer.Tick += async (sender, args) =>
            {
                timer.Stop();
                Version latestVersion = await GetLatestVersion();
                if (latestVersion != null && latestVersion > currentVersion)
                {
                    MessageBoxResult result = MessageBox.Show(
                        string.Format(
                            "TaskSwitcher v{0} is available (you have v{1}).\r\n\r\nDo you want to download it?",
                            latestVersion, currentVersion),
                        "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start("https://github.com/Taskscape/TaskSwitcher/releases/latest");
                    }
                }
                else
                {
                    timer.Interval = new TimeSpan(24, 0, 0);
                    timer.Start();
                }
            };

            timer.Interval = new TimeSpan(0, 0, 0);
            timer.Start();
        }

        private static async Task<Version> GetLatestVersion()
        {
            try
            {
                string versionAsString = await HttpClient.GetStringAsync(
                    "https://raw.github.com/taskscape/TaskSwitcher/update/version.txt");

                Version newVersion;
                if (Version.TryParse(versionAsString, out newVersion))
                {
                    return newVersion;
                }
            }
            catch (HttpRequestException)
            {
                // Log or handle the exception
            }
            return null;
        }

        // Cancellation token source for window loading operations
        private System.Threading.CancellationTokenSource _loadCancellationTokenSource;
        
        /// <summary>
        /// Populates the window list with the current running windows.
        /// </summary>
        private async void LoadData(InitialFocus focus)
        {
            // Cancel any previous loading operation
            if (_loadCancellationTokenSource != null)
            {
                _loadCancellationTokenSource.Cancel();
                _loadCancellationTokenSource.Dispose();
            }
            
            // Create a new cancellation token for this operation
            _loadCancellationTokenSource = new System.Threading.CancellationTokenSource();
            var cancellationToken = _loadCancellationTokenSource.Token;
            
            try
            {
                // Initial UI feedback - could show a loading indicator here
                
                // Use the lazy loading approach to avoid loading all windows upfront
                WindowFinder windowFinder = new();
                
                // Perform window loading on a background thread
                _unfilteredWindowList = await Task.Run(() => 
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return windowFinder.GetWindowsLazy().Select(window => new AppWindowViewModel(window)).ToList();
                }, cancellationToken);
                
                // Check for cancellation before proceeding
                cancellationToken.ThrowIfCancellationRequested();
                
                AppWindowViewModel firstWindow = _unfilteredWindowList.FirstOrDefault();
                bool foregroundWindowMovedToBottom = false;
                
                // Move first window to the bottom of the list if it's related to the foreground window
                if (firstWindow != null && AreWindowsRelated(firstWindow.AppWindow, _foregroundWindow))
                {
                    _unfilteredWindowList.RemoveAt(0);
                    _unfilteredWindowList.Add(firstWindow);
                    foregroundWindowMovedToBottom = true;
                }
                
                // Check for cancellation before updating the UI
                cancellationToken.ThrowIfCancellationRequested();
                
                _filteredWindowList = [.. _unfilteredWindowList];
                _windowCloser = new WindowCloser();
                
                // Update UI before starting background formatting
                lb.DataContext = null;
                lb.DataContext = _filteredWindowList;
                
                FocusItemInList(focus, foregroundWindowMovedToBottom);
                tb.Clear();
                tb.Focus();
                CenterWindow();
                ScrollSelectedItemIntoView();
                
                // Process window title formatting in the background for better UI responsiveness
                // Use the same cancellation token to ensure formatting is canceled if a new load starts
                await Task.Run(() => FormatWindowTitles(_unfilteredWindowList, cancellationToken), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when operation is canceled
            }
            catch (OperationCanceledException)
            {
                // Expected when operation is canceled
            }
            catch (Exception ex)
            {
                // Log unexpected errors
                Debug.WriteLine($"Error during window loading: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Formats window titles in the background to improve UI responsiveness
        /// </summary>
        private void FormatWindowTitles(List<AppWindowViewModel> windows, System.Threading.CancellationToken cancellationToken)
        {
            const int batchSize = 10;
            int processedCount = 0;
            
            while (processedCount < windows.Count)
            {
                // Check for cancellation before processing each batch
                if (cancellationToken.IsCancellationRequested)
                    return;
                
                int end = Math.Min(processedCount + batchSize, windows.Count);
                var batch = new List<AppWindowViewModel>();
                
                // Collect the current batch
                for (int i = processedCount; i < end; i++)
                {
                    batch.Add(windows[i]);
                }
                
                // Update the UI on the dispatcher thread
                Dispatcher.Invoke(() => 
                {
                    foreach (AppWindowViewModel window in batch)
                    {
                        window.FormattedTitle = new XamlHighlighter().Highlight(new[] { new StringPart(window.AppWindow.Title) });
                        window.FormattedProcessTitle = new XamlHighlighter().Highlight(new[] { new StringPart(window.AppWindow.ProcessTitle) });
                    }
                });
                
                processedCount = end;
                
                // Small delay to keep the UI responsive
                System.Threading.Thread.Sleep(10);
            }
        }

        /// <summary>
        /// Formats window titles in the background to improve UI responsiveness
        /// </summary>
        private void FormatWindowTitles(List<AppWindowViewModel> windows)
        {
            const int batchSize = 10;
            int processedCount = 0;
            
            Action processNextBatch = null;
            processNextBatch = () =>
            {
                int end = Math.Min(processedCount + batchSize, windows.Count);
                
                for (int i = processedCount; i < end; i++)
                {
                    AppWindowViewModel window = windows[i];
                    window.FormattedTitle = new XamlHighlighter().Highlight(new[] { new StringPart(window.AppWindow.Title) });
                    window.FormattedProcessTitle = new XamlHighlighter().Highlight(new[] { new StringPart(window.AppWindow.ProcessTitle) });
                }
                
                processedCount = end;
                
                if (processedCount < windows.Count)
                {
                    Dispatcher.BeginInvoke(processNextBatch, DispatcherPriority.Background);
                }
            };
            
            processNextBatch();
        }

        private static bool AreWindowsRelated(SystemWindow window1, SystemWindow window2)
        {
            return window1.HWnd == window2.HWnd || window1.Process.Id == window2.Process.Id;
        }

        private void FocusItemInList(InitialFocus focus, bool foregroundWindowMovedToBottom)
        {
            if (focus == InitialFocus.PreviousItem)
            {
                int previousItemIndex = lb.Items.Count - 1;
                if (foregroundWindowMovedToBottom)
                {
                    previousItemIndex--;
                }

                lb.SelectedIndex = previousItemIndex > 0 ? previousItemIndex : 0;
            }
            else
            {
                lb.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Place the TaskSwitcher window in the center of the screen
        /// </summary>
        private void CenterWindow()
        {
            // Reset height every time to ensure that resolution changes take effect
            Border.MaxHeight = SystemParameters.PrimaryScreenHeight;

            // Force a rendering before repositioning the window
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;

            // Position the window in the center of the screen
            Left = (SystemParameters.PrimaryScreenWidth/2) - (ActualWidth/2);
            Top = (SystemParameters.PrimaryScreenHeight/2) - (ActualHeight/2);
        }

        /// <summary>
        /// Switches the window associated with the selected item.
        /// </summary>
        private void Switch()
        {
            if (lb.Items.Count > 0)
            {
                AppWindowViewModel win = (AppWindowViewModel) (lb.SelectedItem ?? lb.Items[0]);
                win.AppWindow.SwitchToLastVisibleActivePopup();
            }

            HideWindow();
        }

        private void HideWindow()
        {
            if (_windowCloser != null)
            {
                _windowCloser.Dispose();
                _windowCloser = null;
            }

            _altTabAutoSwitch = false;
            Opacity = 0;
            Dispatcher.BeginInvoke(new Action(Hide), DispatcherPriority.Input);
        }

        #endregion

        /// =================================

        #region Right-click menu functions

        /// =================================
        /// <summary>
        /// Show Options dialog.
        /// </summary>
        private void Options()
        {
            if (_optionsWindow == null)
            {
                _optionsWindow = new OptionsWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                _optionsWindow.Closed += (sender, args) => _optionsWindow = null;
                _optionsWindow.ShowDialog();
            }
            else
            {
                _optionsWindow.Activate();
            }
        }

        /// <summary>
        /// Show About dialog.
        /// </summary>
        private void About()
        {
            if (_aboutWindow == null)
            {
                _aboutWindow = new AboutWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                _aboutWindow.Closed += (sender, args) => _aboutWindow = null;
                _aboutWindow.ShowDialog();
            }
            else
            {
                _aboutWindow.Activate();
            }
        }

        /// <summary>
        /// Quit TaskSwitcher
        /// </summary>
        private void Quit()
        {
            Dispose();
            Application.Current.Shutdown();
        }
        
        #region IDisposable Implementation
        
        private bool _disposed;
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            
            if (disposing)
            {
                // Dispose managed resources
                _notifyIcon?.Dispose();
                _hotkey?.Dispose();
                _altTabHook?.Dispose();
                _windowCloser?.Dispose();
                
                // Clean up the cancellation token sources
                if (_filterCancellationTokenSource != null)
                {
                    _filterCancellationTokenSource.Cancel();
                    _filterCancellationTokenSource.Dispose();
                    _filterCancellationTokenSource = null;
                }
                
                if (_loadCancellationTokenSource != null)
                {
                    _loadCancellationTokenSource.Cancel();
                    _loadCancellationTokenSource.Dispose();
                    _loadCancellationTokenSource = null;
                }
                
                // Dispose window view models
                if (_unfilteredWindowList != null)
                {
                    foreach (var window in _unfilteredWindowList)
                    {
                        window.Dispose();
                    }
                }
                
                // Unregister event handlers
                if (_hotkey != null)
                    _hotkey.HotkeyPressed -= hotkey_HotkeyPressed;
                
                if (_altTabHook != null)
                    _altTabHook.Pressed -= AltTabPressed;
                
                // Clean up references
                _notifyIcon = null;
                _hotkey = null;
                _altTabHook = null;
                _windowCloser = null;
                _unfilteredWindowList = null;
                _filteredWindowList = null;
                _foregroundWindow = null;
            }
            
            // Free unmanaged resources
            
            _disposed = true;
        }
        
        ~MainWindow()
        {
            Dispose(false);
        }
        
        #endregion

        #endregion

        /// =================================

        #region Event Handlers

        /// =================================
        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            HideWindow();
        }
        
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        private void hotkey_HotkeyPressed(object sender, EventArgs e)
        {
            if (!Settings.Default.EnableHotKey)
            {
                return;
            }

            if (Visibility != Visibility.Visible)
            {
                tb.IsEnabled = true;

                _foregroundWindow = SystemWindow.ForegroundWindow;
                Show();
                Activate();
                Keyboard.Focus(tb);
                LoadData(InitialFocus.NextItem);
                Opacity = 1;
            }
            else
            {
                HideWindow();
            }
        }

        private void AltTabPressed(object sender, AltTabHookEventArgs e)
        {
            if (!Settings.Default.AltTabHook)
            {
                // Ignore Alt+Tab presses if the hook is not activated by the user
                return;
            }

            e.Handled = true;

            if (Visibility != Visibility.Visible)
            {
                tb.IsEnabled = true;

                _foregroundWindow = SystemWindow.ForegroundWindow;

                ActivateAndFocusMainWindow();

                Keyboard.Focus(tb);
                if (e.ShiftDown)
                {
                    LoadData(InitialFocus.PreviousItem);
                }
                else
                {
                    LoadData(InitialFocus.NextItem);
                }

                if (Settings.Default.AutoSwitch && !e.CtrlDown)
                {
                    _altTabAutoSwitch = true;
                    tb.IsEnabled = false;
                    tb.Text = "Press Alt + S to search";
                }

                Opacity = 1;
            }
            else
            {
                if (e.ShiftDown)
                {
                    PreviousItem();
                }
                else
                {
                    NextItem();
                }
            }
        }

        private void ActivateAndFocusMainWindow()
        {
            // What happens below looks a bit weird, but for TaskSwitcher to get focus when using the Alt+Tab hook,
            // it is needed to simulate an Alt keypress will bring TaskSwitcher to the foreground. Otherwise TaskSwitcher
            // will become the foreground window, but the previous window will retain focus, and receive keep getting
            // the keyboard input.
            // http://www.codeproject.com/Tips/76427/How-to-bring-window-to-top-with-SetForegroundWindo

            IntPtr thisWindowHandle = new WindowInteropHelper(this).Handle;
            AppWindow thisWindow = new(thisWindowHandle);

            KeyboardKey altKey = new(Keys.Alt);
            bool altKeyPressed = false;

            // Press the Alt key if it is not already being pressed
            if ((altKey.AsyncState & 0x8000) == 0)
            {
                altKey.Press();
                altKeyPressed = true;
            }

            // Bring the TaskSwitcher window to the foreground
            Show();
            SystemWindow.ForegroundWindow = thisWindow;
            Activate();

            // Release the Alt key if it was pressed above
            if (altKeyPressed)
            {
                altKey.Release();
            }
        }

        // A cancellation token source to cancel previous filtering operations when new input arrives
        private System.Threading.CancellationTokenSource _filterCancellationTokenSource;
        
        private async void TextChanged(object sender, TextChangedEventArgs args)
        {
            if (!tb.IsEnabled)
            {
                return;
            }

            // Cancel any previous filtering operation
            if (_filterCancellationTokenSource != null)
            {
                _filterCancellationTokenSource.Cancel();
                _filterCancellationTokenSource.Dispose();
            }
            
            // Create a new cancellation token for this operation
            _filterCancellationTokenSource = new System.Threading.CancellationTokenSource();
            var cancellationToken = _filterCancellationTokenSource.Token;

            try
            {
                string query = tb.Text;
                
                // Show some immediate feedback to the user
                if (!string.IsNullOrEmpty(query))
                {
                    // Display a "Filtering..." status or progress indicator here if desired
                }

                // Create the filter context
                var context = new WindowFilterContext<AppWindowViewModel>
                {
                    Windows = _unfilteredWindowList,
                    ForegroundWindowProcessTitle = new AppWindow(_foregroundWindow.HWnd).ProcessTitle
                };

                // Perform filtering on a background thread
                List<FilterResult<AppWindowViewModel>> filterResults = await Task.Run(() => 
                {
                    // Check for cancellation before starting work
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    return WindowFilterer.Filter(context, query).ToList();
                }, cancellationToken);
                
                // Check for cancellation before formatting titles
                cancellationToken.ThrowIfCancellationRequested();
                
                // Process the formatted titles in batches to avoid UI freezing
                await Task.Run(() => 
                {
                    foreach (FilterResult<AppWindowViewModel> filterResult in filterResults)
                    {
                        // Check for cancellation during title formatting
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        filterResult.AppWindow.FormattedTitle =
                            GetFormattedTitleFromBestResult(filterResult.WindowTitleMatchResults);
                        filterResult.AppWindow.FormattedProcessTitle =
                            GetFormattedTitleFromBestResult(filterResult.ProcessTitleMatchResults);
                    }
                }, cancellationToken);

                // Check for cancellation before updating UI
                cancellationToken.ThrowIfCancellationRequested();
                
                // Update the UI on the main thread
                _filteredWindowList = new ObservableCollection<AppWindowViewModel>(filterResults.Select(r => r.AppWindow));
                lb.DataContext = _filteredWindowList;
                
                if (lb.Items.Count > 0)
                {
                    lb.SelectedItem = lb.Items[0];
                }
            }
            catch (TaskCanceledException)
            {
                // The operation was canceled because a new filter request came in - this is expected
            }
            catch (OperationCanceledException)
            {
                // The operation was canceled because a new filter request came in - this is expected
            }
            catch (Exception ex)
            {
                // Log unexpected errors
                Debug.WriteLine($"Error during filtering: {ex.Message}");
            }
        }

        private static string GetFormattedTitleFromBestResult(IList<MatchResult> matchResults)
        {
            MatchResult bestResult = matchResults.FirstOrDefault(r => r.Matched) ?? matchResults.First();
            return new XamlHighlighter().Highlight(bestResult.StringParts);
        }

        private void OnEnterPressed(object sender, ExecutedRoutedEventArgs e)
        {
            Switch();
            e.Handled = true;
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Switch();
            e.Handled = true;
        }

        private async void CloseWindow(object sender, ExecutedRoutedEventArgs e)
        {
            if (lb.Items.Count > 0)
            {
                AppWindowViewModel win = (AppWindowViewModel) lb.SelectedItem;
                if (win != null)
                {
                    bool isClosed = await _windowCloser.TryCloseAsync(win);
                    if (isClosed)
                        RemoveWindow(win);
                }
            }
            else
            {
                HideWindow();
            }
            e.Handled = true;
        }

        private void RemoveWindow(AppWindowViewModel window)
        {
            int index = _filteredWindowList.IndexOf(window);
            if (index < 0)
                return;

            if (lb.SelectedIndex == index)
            {
                if (_filteredWindowList.Count > index + 1)
                    lb.SelectedIndex++;
                else
                {
                    if (index > 0)
                        lb.SelectedIndex--;
                }
            }

            _filteredWindowList.Remove(window);
            _unfilteredWindowList.Remove(window);
            
            // Properly dispose the removed window
            window.Dispose();
        }

        private void ScrollListUp(object sender, ExecutedRoutedEventArgs e)
        {
            PreviousItem();
            e.Handled = true;
        }

        private void PreviousItem()
        {
            if (lb.Items.Count > 0)
            {
                if (lb.SelectedIndex != 0)
                {
                    lb.SelectedIndex--;
                }
                else
                {
                    lb.SelectedIndex = lb.Items.Count - 1;
                }

                ScrollSelectedItemIntoView();
            }
        }

        private void ScrollListDown(object sender, ExecutedRoutedEventArgs e)
        {
            NextItem();
            e.Handled = true;
        }

        private void NextItem()
        {
            if (lb.Items.Count > 0)
            {
                if (lb.SelectedIndex != lb.Items.Count - 1)
                {
                    lb.SelectedIndex++;
                }
                else
                {
                    lb.SelectedIndex = 0;
                }

                ScrollSelectedItemIntoView();
            }
        }

        private void ScrollSelectedItemIntoView()
        {
            object selectedItem = lb.SelectedItem;
            if (selectedItem != null)
            {
                lb.ScrollIntoView(selectedItem);
            }
        }

        private void MainWindow_OnLostFocus(object sender, EventArgs e)
        {
            HideWindow();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            DisableSystemMenu();
        }

        private void DisableSystemMenu()
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            SystemWindow window = new(windowHandle);
            window.Style = window.Style & ~WindowStyleFlags.SYSMENU;
        }

        private void ShowHelpTextBlock_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Duration duration = new(TimeSpan.FromSeconds(0.150));
            int newHeight = HelpPanel.Height > 0 ? 0 : +17;
            HelpPanel.BeginAnimation(HeightProperty, new DoubleAnimation(HelpPanel.Height, newHeight, duration));
        }

        #endregion

        private enum InitialFocus
        {
            NextItem,
            PreviousItem
        }
    }
}
