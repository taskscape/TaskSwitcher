using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TaskSwitcher.Core.Utilities;

namespace TaskSwitcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize the logger
            Logger.Initialize();
            
            // Log application start
            Logger.Info("===== TaskSwitcher Application Starting =====");
            Logger.Info($"Command line: {Environment.CommandLine}");
            
            // Set up global exception handling
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }
        
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            Logger.Error($"Unhandled AppDomain exception (Terminating: {e.IsTerminating})", ex);
            
            if (e.IsTerminating)
            {
                MessageBox.Show(
                    $"A fatal error has occurred and the application needs to close.\n\nError: {(ex?.Message ?? "Unknown error")}\n\nPlease check the log file for details.",
                    "TaskSwitcher - Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error("Unhandled UI thread exception", e.Exception);
            
            // Mark as handled to prevent application crash
            e.Handled = true;
            
            // Show a user-friendly message
            MessageBox.Show(
                $"An error has occurred:\n\n{e.Exception.Message}\n\nThe application will continue running, but you may want to restart it if you encounter further issues.",
                "TaskSwitcher - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        
        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Error("Unobserved Task exception", e.Exception);
            
            // Mark as observed to prevent application crash
            e.SetObserved();
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info($"Application exiting with code: {e.ApplicationExitCode}");
            Logger.Info("===== TaskSwitcher Application Exiting =====");
            
            base.OnExit(e);
        }
    }
}