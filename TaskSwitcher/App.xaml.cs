using System;
using System.Windows;
using System.Windows.Threading;

namespace TaskSwitcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool _recovering;

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            HandleUnhandledException("App.DispatcherUnhandledException", e.Exception);
        }

        /// <summary>
        /// TaskSwitcher runs unattended in the background for the whole session, so a failure
        /// while handling one key press must not take it down. The exception is logged, the
        /// overlay is hidden so that it cannot linger in a half-initialized state, and the
        /// application keeps running. Must be called on the UI thread.
        /// </summary>
        internal void HandleUnhandledException(string context, Exception exception)
        {
            DiagnosticLogger.LogException(context, exception);

            // Hiding the overlay is dispatched as well. Should that fail too, it is not
            // attempted again, as that would repeat without end.
            if (_recovering)
            {
                return;
            }

            _recovering = true;
            try
            {
                (MainWindow as MainWindow)?.HideAfterError();
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogException(context + ".Recovery", ex);
            }

            Dispatcher.BeginInvoke(new Action(() => _recovering = false), DispatcherPriority.ContextIdle);
        }
    }
}