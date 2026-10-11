using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
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
        private static readonly TimeSpan WindowCloseTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FilterDebounce = TimeSpan.FromMilliseconds(100);

        private readonly WindowCloser _windowCloser = new();
        private CancellationTokenSource _closeCancellationTokenSource;
        private List<AppWindowViewModel> _unfilteredWindowList;
        private ObservableCollection<AppWindowViewModel> _filteredWindowList;
        private NotifyIcon _notifyIcon;
        private HotKey _hotkey;
        private string _foregroundProcessTitle;

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

            UpdateAltTabHook();

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

        /// <summary>
        /// Installs the Alt+Tab keyboard hook while that option is enabled and removes it otherwise.
        /// </summary>
        private void UpdateAltTabHook()
        {
            // Every key press of the whole system passes through the hook for as long as it is
            // installed, so it only exists while it is actually wanted.
            bool hookWanted = Settings.Default.AltTabHook;
            if (hookWanted == (_altTabHook != null))
            {
                return;
            }

            if (!hookWanted)
            {
                _altTabHook.Pressed -= AltTabPressed;
                DisposeSafely(_altTabHook, nameof(_altTabHook));
                _altTabHook = null;
                return;
            }

            try
            {
                AltTabHook altTabHook = new();
                altTabHook.Pressed += AltTabPressed;
                _altTabHook = altTabHook;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                DiagnosticLogger.LogException("MainWindow.UpdateAltTabHook", ex);
                MessageBox.Show(
                    "TaskSwitcher could not take over Alt+Tab, so the task switcher of Windows stays in use." +
                    Environment.NewLine +
                    Environment.NewLine +
                    "The hotkey for activating TaskSwitcher is not affected.",
                    "Unable to Use Alt+Tab", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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

            // Refresh menu state before showing
            _notifyIcon.ContextMenuStrip.Opening += (s, e) =>
            {
                runOnStartupMenuItem.Checked = new AutoStart().IsEnabled;
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
                try
                {
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
                            try
                            {
                                using Process browserProcess = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "https://github.com/Taskscape/TaskSwitcher/releases/latest",
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                DiagnosticLogger.LogException("MainWindow.OpenReleasePage", ex);
                                MessageBox.Show(
                                    "TaskSwitcher could not open the download page in your default browser.",
                                    "Unable to Open Browser",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            }
                        }

                        return;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    // HttpClient reports request timeouts as cancellation. Do not allow an
                    // exception from this async event handler to reach the WPF dispatcher.
                    DiagnosticLogger.LogException("MainWindow.CheckForUpdates.Canceled", ex);
                }
                catch (Exception ex)
                {
                    // DispatcherTimer event handlers are async void, so every exception must
                    // be contained here to prevent an update failure from terminating the app.
                    DiagnosticLogger.LogException("MainWindow.CheckForUpdates", ex);
                }

                timer.Interval = TimeSpan.FromDays(1);
                timer.Start();
            };

            timer.Interval = new TimeSpan(0, 0, 0);
            timer.Start();
        }

        private static async Task<Version> GetLatestVersion()
        {
            using var perf = PerfRecorder.Measure("GetLatestVersion");
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
            catch (HttpRequestException ex)
            {
                // Log the exception when logging is enabled
                DiagnosticLogger.LogException("MainWindow.GetLatestVersion", ex);
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
            using var perf = PerfRecorder.Measure("LoadData");

            // Cancel any previous loading operation and create a new token for this one
            CancellationToken cancellationToken = RenewCancellation(ref _loadCancellationTokenSource);

            // A filter that has not been applied yet belongs to the previous list of windows.
            CancelPendingFilter();

            // The search box is reset here, before the first await and so before this method
            // returns to its caller: the caller may put a hint into the box right afterwards,
            // and whatever the user types while the windows are being loaded has to be kept.
            ClearSearchText();

            IntPtr foregroundWindowHandle = _foregroundWindow?.HWnd ?? IntPtr.Zero;

            try
            {
                // Everything that queries other applications runs on a background thread, so
                // that a busy application cannot stall the UI thread.
                LoadedWindows loadedWindows = await Task.Run(
                    () => LoadWindows(foregroundWindowHandle, cancellationToken), cancellationToken);

                // Check for cancellation before updating the UI
                cancellationToken.ThrowIfCancellationRequested();

                _unfilteredWindowList = loadedWindows.Windows;
                _foregroundProcessTitle = loadedWindows.ForegroundProcessTitle;
                _filteredWindowList = [.. _unfilteredWindowList];
                lb.DataContext = _filteredWindowList;

                FocusItemInList(focus, loadedWindows.ForegroundWindowMovedToBottom);
                tb.Focus();
                CenterWindow();
                ScrollSelectedItemIntoView();

                LoadMissingIcons(loadedWindows.Windows, cancellationToken);

                if (tb.IsEnabled && tb.Text.Length > 0)
                {
                    // The user started typing before the windows were loaded.
                    ApplyFilterNow();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when a newer load supersedes this one
            }
            catch (Exception ex)
            {
                // This method is async void, so contain unexpected failures.
                DiagnosticLogger.LogException("MainWindow.LoadData", ex);
            }
        }
        
        /// <summary>
        /// Collects the windows to show, together with everything that is needed to display
        /// them. Runs on a background thread.
        /// </summary>
        private static LoadedWindows LoadWindows(IntPtr foregroundWindowHandle, CancellationToken cancellationToken)
        {
            List<AppWindow> appWindows;
            using (PerfRecorder.Measure("EnumerateWindows"))
            {
                appWindows = new WindowFinder().GetWindows();
            }

            XamlHighlighter highlighter = new();
            List<AppWindowViewModel> windows = new(appWindows.Count);
            foreach (AppWindow appWindow in appWindows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // The titles are formatted here, so that the list is complete when it appears.
                AppWindowViewModel window = new(appWindow);
                window.FormattedTitle = highlighter.Highlight(new[] { new StringPart(window.WindowTitle) });
                window.FormattedProcessTitle = highlighter.Highlight(new[] { new StringPart(window.ProcessTitle) });

                // Icons that were loaded before are shown at once. See LoadMissingIcons for the others.
                window.IconImage = WindowIconImageLoader.GetCached(appWindow);

                windows.Add(window);
            }

            bool foregroundWindowMovedToBottom = false;
            string foregroundProcessTitle = null;
            if (foregroundWindowHandle != IntPtr.Zero)
            {
                AppWindow foregroundWindow = new(foregroundWindowHandle);
                foregroundProcessTitle = foregroundWindow.ProcessTitle;

                // Move first window to the bottom of the list if it's related to the foreground window
                if (windows.Count > 0 && AreWindowsRelated(windows[0].AppWindow, foregroundWindow))
                {
                    AppWindowViewModel firstWindow = windows[0];
                    windows.RemoveAt(0);
                    windows.Add(firstWindow);
                    foregroundWindowMovedToBottom = true;
                }
            }

            return new LoadedWindows(windows, foregroundProcessTitle, foregroundWindowMovedToBottom);
        }

        /// <summary>
        /// Loads the icons that were not available from the cache when the list was created.
        /// </summary>
        private static void LoadMissingIcons(List<AppWindowViewModel> windows, CancellationToken cancellationToken)
        {
            AppWindowViewModel[] windowsWithoutIcon = windows.Where(window => window.IconImage == null).ToArray();
            if (windowsWithoutIcon.Length == 0)
            {
                return;
            }

            // Retrieving an icon waits for a reply from the application that owns the window,
            // which takes a while when that application is busy. On the UI thread this would
            // freeze the switcher, and with it the keyboard hook that runs on the same thread.
            Task.Run(() =>
            {
                using var perf = PerfRecorder.Measure("LoadMissingIcons");
                foreach (AppWindowViewModel window in windowsWithoutIcon)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        // Null once the window has been closed from within the switcher
                        AppWindow appWindow = window.AppWindow;
                        if (appWindow != null)
                        {
                            // WPF delivers the change notification of the property to the UI thread.
                            window.IconImage = WindowIconImageLoader.Load(appWindow);
                        }
                    }
                    catch (Exception ex)
                    {
                        // A window without an icon must not affect the remaining ones.
                        DiagnosticLogger.LogException("MainWindow.LoadMissingIcons", ex);
                    }
                }
            });
        }

        /// <summary>
        /// Cancels the operation of the given token source, if any, and starts a new one.
        /// </summary>
        private static CancellationToken RenewCancellation(ref CancellationTokenSource cancellationTokenSource)
        {
            CancellationTokenSource previous = cancellationTokenSource;
            cancellationTokenSource = new CancellationTokenSource();

            if (previous != null)
            {
                previous.Cancel();
                previous.Dispose();
            }

            return cancellationTokenSource.Token;
        }

        private sealed record LoadedWindows(
            List<AppWindowViewModel> Windows,
            string ForegroundProcessTitle,
            bool ForegroundWindowMovedToBottom);

        private static bool AreWindowsRelated(SystemWindow window1, SystemWindow window2)
        {
            return window1.HWnd == window2.HWnd || window1.ProcessId == window2.ProcessId;
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
            // Enter may have been pressed before the list caught up with the search text.
            FlushPendingFilter();

            if (lb.Items.Count > 0)
            {
                AppWindowViewModel win = (AppWindowViewModel) (lb.SelectedItem ?? lb.Items[0]);
                win.AppWindow?.SwitchToLastVisibleActivePopup();
            }

            HideWindow();
        }

        private void HideWindow()
        {
            CancelCurrentCloseOperation();

            _altTabAutoSwitch = false;
            Opacity = 0;
            Dispatcher.BeginInvoke(new Action(Hide), DispatcherPriority.Input);
        }

        /// <summary>
        /// Returns the overlay to its hidden state after an operation failed halfway, so that
        /// it cannot stay behind invisible but still holding the keyboard focus.
        /// </summary>
        internal void HideAfterError()
        {
            if (!_disposed)
            {
                HideWindow();
            }
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
                _optionsWindow.Closed += (sender, args) =>
                {
                    _optionsWindow = null;

                    // The Alt+Tab option may have been changed.
                    UpdateAltTabHook();
                };
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
            try
            {
                Dispose();
            }
            finally
            {
                // Without this the process would stay alive without a tray icon to end it,
                // and it would keep new instances from starting.
                Application.Current.Shutdown();
            }
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

            // Set first, so that a failure below cannot lead to a second attempt.
            _disposed = true;

            if (disposing)
            {
                // Unregister event handlers
                if (_hotkey != null)
                    _hotkey.HotkeyPressed -= hotkey_HotkeyPressed;

                if (_altTabHook != null)
                    _altTabHook.Pressed -= AltTabPressed;

                // Dispose managed resources. Each one is released on its own: when one of them
                // fails, the others still have to be released and the application still has
                // to be able to exit.
                DisposeSafely(_notifyIcon, nameof(_notifyIcon));
                DisposeSafely(_hotkey, nameof(_hotkey));
                DisposeSafely(_altTabHook, nameof(_altTabHook));
                CancelCurrentCloseOperation();
                
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

                // Clean up references
                _notifyIcon = null;
                _hotkey = null;
                _altTabHook = null;
                _unfilteredWindowList = null;
                _filteredWindowList = null;
                _foregroundWindow = null;
            }
        }

        private static void DisposeSafely(IDisposable disposable, string name)
        {
            try
            {
                disposable?.Dispose();
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogException("MainWindow.Dispose." + name, ex);
            }
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

            // This is called from within the low-level keyboard hook, which has to return at
            // once (see AltTabHook.Pressed). Only the decision to take the key press is made
            // here. The dispatcher shows the window as soon as the hook has returned.
            bool shiftDown = e.ShiftDown;
            bool ctrlDown = e.CtrlDown;
            Dispatcher.BeginInvoke(new Action(() => ShowOrCycleWindowList(shiftDown, ctrlDown)));
        }

        private void ShowOrCycleWindowList(bool shiftDown, bool ctrlDown)
        {
            if (_disposed)
            {
                return;
            }

            if (Visibility != Visibility.Visible)
            {
                tb.IsEnabled = true;

                _foregroundWindow = SystemWindow.ForegroundWindow;

                ActivateAndFocusMainWindow();

                Keyboard.Focus(tb);
                if (shiftDown)
                {
                    LoadData(InitialFocus.PreviousItem);
                }
                else
                {
                    LoadData(InitialFocus.NextItem);
                }

                if (Settings.Default.AutoSwitch && !ctrlDown)
                {
                    _altTabAutoSwitch = true;
                    tb.IsEnabled = false;
                    tb.Text = "Press Alt + S to search";
                }

                Opacity = 1;
            }
            else
            {
                if (shiftDown)
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

        // True from the moment the search text changes until the list shows the matching windows
        private bool _filterPending;

        // True while the search box is being changed by the application instead of the user
        private bool _ignoreTextChanges;

        private async void TextChanged(object sender, TextChangedEventArgs args)
        {
            if (!tb.IsEnabled || _ignoreTextChanges)
            {
                return;
            }

            // Cancel any previous filtering operation and create a new token for this one
            CancellationToken cancellationToken = RenewCancellation(ref _filterCancellationTokenSource);
            _filterPending = true;

            try
            {
                await Task.Delay(FilterDebounce, cancellationToken); // debounce rapid typing

                FilterInput filterInput = CreateFilterInput();

                // Perform filtering on a background thread
                List<FilteredWindow> filteredWindows = await Task.Run(
                    () => FilterWindows(filterInput, cancellationToken), cancellationToken);

                // Check for cancellation before updating UI
                cancellationToken.ThrowIfCancellationRequested();

                ShowFilteredWindows(filteredWindows);
            }
            catch (OperationCanceledException)
            {
                // The operation was canceled because a new filter request came in, or because
                // the result was needed at once (see FlushPendingFilter) - this is expected
            }
            catch (Exception ex)
            {
                // This event handler is async void, so contain unexpected failures.
                DiagnosticLogger.LogException("MainWindow.TextChanged", ex);
            }
        }

        /// <summary>
        /// Captures what a filter run needs, so that the run itself does not touch any state
        /// of the window and can execute on a background thread.
        /// </summary>
        private FilterInput CreateFilterInput()
        {
            // A copy, because closing a window removes it from the list on the UI thread.
            AppWindowViewModel[] windows = _unfilteredWindowList?.ToArray() ?? [];
            return new FilterInput(windows, _foregroundProcessTitle, tb.Text);
        }

        private static List<FilteredWindow> FilterWindows(FilterInput filterInput, CancellationToken cancellationToken)
        {
            using var perf = PerfRecorder.Measure("FilterWindows");

            // Check for cancellation before starting work
            cancellationToken.ThrowIfCancellationRequested();

            var context = new WindowFilterContext<AppWindowViewModel>
            {
                Windows = filterInput.Windows,
                ForegroundWindowProcessTitle = filterInput.ForegroundProcessTitle
            };

            XamlHighlighter highlighter = new();
            List<FilteredWindow> filteredWindows = [];
            foreach (FilterResult<AppWindowViewModel> filterResult in new WindowFilterer().Filter(context, filterInput.Query))
            {
                // Check for cancellation during title formatting
                cancellationToken.ThrowIfCancellationRequested();

                filteredWindows.Add(new FilteredWindow(
                    filterResult.AppWindow,
                    GetFormattedTitleFromBestResult(highlighter, filterResult.WindowTitleMatchResults),
                    GetFormattedTitleFromBestResult(highlighter, filterResult.ProcessTitleMatchResults)));
            }

            return filteredWindows;
        }

        /// <summary>
        /// Shows the result of a filter run. Must be called on the UI thread.
        /// </summary>
        private void ShowFilteredWindows(List<FilteredWindow> filteredWindows)
        {
            _filterPending = false;

            // The list is replaced before the titles are updated: the rows of the previous
            // list are released first and therefore do not render the new titles as well.
            _filteredWindowList = new ObservableCollection<AppWindowViewModel>(
                filteredWindows.Select(filteredWindow => filteredWindow.Window));
            lb.DataContext = _filteredWindowList;

            foreach (FilteredWindow filteredWindow in filteredWindows)
            {
                filteredWindow.Window.FormattedTitle = filteredWindow.FormattedTitle;
                filteredWindow.Window.FormattedProcessTitle = filteredWindow.FormattedProcessTitle;
            }

            if (lb.Items.Count > 0)
            {
                lb.SelectedItem = lb.Items[0];
            }
        }

        /// <summary>
        /// Filters the list by the current search text right away, without waiting for the
        /// debounce interval or for a background thread.
        /// </summary>
        private void ApplyFilterNow()
        {
            CancelPendingFilter();

            try
            {
                ShowFilteredWindows(FilterWindows(CreateFilterInput(), CancellationToken.None));
            }
            catch (Exception ex)
            {
                // Callers go on to act on the list, which then simply stays as it was.
                DiagnosticLogger.LogException("MainWindow.ApplyFilterNow", ex);
            }
        }

        /// <summary>
        /// Makes sure the list matches the search text before a command acts on it. Without
        /// this, a command issued right after typing (typically Enter) would apply to whatever
        /// the list showed before, because filtering is debounced and asynchronous.
        /// </summary>
        private void FlushPendingFilter()
        {
            if (_filterPending)
            {
                ApplyFilterNow();
            }
        }

        private void CancelPendingFilter()
        {
            _filterCancellationTokenSource?.Cancel();
            _filterPending = false;
        }

        /// <summary>
        /// Empties the search box without triggering a filter run.
        /// </summary>
        private void ClearSearchText()
        {
            _ignoreTextChanges = true;
            try
            {
                tb.Clear();
            }
            finally
            {
                _ignoreTextChanges = false;
            }
        }

        private static string GetFormattedTitleFromBestResult(XamlHighlighter highlighter, IList<MatchResult> matchResults)
        {
            MatchResult bestResult = matchResults.FirstOrDefault(r => r.Matched) ?? matchResults.First();
            return highlighter.Highlight(bestResult.StringParts);
        }

        private sealed record FilterInput(AppWindowViewModel[] Windows, string ForegroundProcessTitle, string Query);

        private sealed record FilteredWindow(AppWindowViewModel Window, string FormattedTitle, string FormattedProcessTitle);

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
            FlushPendingFilter();

            if (lb.Items.Count > 0)
            {
                AppWindowViewModel win = (AppWindowViewModel) lb.SelectedItem;
                if (win != null)
                {
                    CancelCurrentCloseOperation();
                    CancellationTokenSource closeCancellationTokenSource = new();
                    _closeCancellationTokenSource = closeCancellationTokenSource;
                    win.IsBeingClosed = true;

                    try
                    {
                        bool isClosed = await _windowCloser.TryCloseAsync(
                            win,
                            WindowCloseTimeout,
                            closeCancellationTokenSource.Token);
                        if (isClosed)
                        {
                            RemoveWindow(win);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Closing was canceled because the switcher was hidden or another
                        // close operation superseded this one.
                    }
                    catch (Exception ex)
                    {
                        // This command handler is async void, so contain unexpected failures.
                        DiagnosticLogger.LogException("MainWindow.CloseWindow", ex);
                        win.IsBeingClosed = false;
                    }
                    finally
                    {
                        if (ReferenceEquals(_closeCancellationTokenSource, closeCancellationTokenSource))
                        {
                            _closeCancellationTokenSource = null;
                            win.IsBeingClosed = false;
                        }

                        closeCancellationTokenSource.Dispose();
                    }
                }
            }
            else
            {
                HideWindow();
            }
            e.Handled = true;
        }

        private void CancelCurrentCloseOperation()
        {
            _closeCancellationTokenSource?.Cancel();
        }

        private void RemoveWindow(AppWindowViewModel window)
        {
            // The close operation that leads here may complete after the application was disposed.
            if (_filteredWindowList == null)
                return;

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
            _unfilteredWindowList?.Remove(window);

            // Properly dispose the removed window
            window.Dispose();
        }

        private void ScrollListUp(object sender, ExecutedRoutedEventArgs e)
        {
            FlushPendingFilter();
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
            FlushPendingFilter();
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
