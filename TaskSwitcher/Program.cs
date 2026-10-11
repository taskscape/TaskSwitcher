/*
 * TaskSwitcher - The incremental-search task switcher for Windows.
 * Copyright 2009-2026 James Sulak, Regin Larsen, Taskscape Ltd
 * 
 * TaskSwitcher is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * TaskSwitcher is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with TaskSwitcher.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using TaskSwitcher.Properties;

namespace TaskSwitcher
{
    internal class Program
    {
        private const string mutex_id = "DBDE24E4-91F6-11DF-B495-C536DFD72085-TaskSwitcher";
        private const int ErrorCancelled = 1223;
        private const string SettingsResetArgument = "--settings-reset";

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                RegisterUnhandledExceptionHandlers();

                using (PerfRecorder.Measure("AppStartup"))
                {
                    if (!EnsureSettingsCanBeRead(args))
                    {
                        return;
                    }

                    if (!RunAsAdministratorIfConfigured())
                    {
                        return;
                    }

                    using Mutex mutex = new(false, mutex_id);
                    bool hasHandle = false;
                    try
                    {
                        try
                        {
                            hasHandle = mutex.WaitOne(5000, false);
                            if (hasHandle == false) return; //another instance exist
                        }
                        catch (AbandonedMutexException)
                        {
                            // The previous instance ended without releasing the mutex. This
                            // instance owns it now and has to release it like any other.
                            hasHandle = true;
                        }

#if PORTABLE
                        MakePortable(Settings.Default);
#endif

                        MigrateUserSettings();

                        App app = new()
                        {
                            MainWindow = new MainWindow()
                        };
                        app.Run();
                    }
                    finally
                    {
                        if (hasHandle)
                            mutex.ReleaseMutex();
                    }
                }
            }
            finally
            {
                DiagnosticLogger.ShutdownAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Makes failures outside the WPF dispatcher (see <see cref="App"/>) visible in the
        /// diagnostic log instead of letting them pass unnoticed or end in a framework dialog.
        /// </summary>
        private static void RegisterUnhandledExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                DiagnosticLogger.LogException("Program.UnhandledException", e.ExceptionObject as Exception);
                if (e.IsTerminating)
                {
                    // Nothing runs after this handler, so write the pending entries now.
                    DiagnosticLogger.ShutdownAsync().AsTask().GetAwaiter().GetResult();
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                DiagnosticLogger.LogException("Program.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

            // The global hotkey and the tray menu are dispatched by Windows Forms, which shows
            // its own error dialog for exceptions thrown by their handlers unless this is set.
            System.Windows.Forms.Application.ThreadException += (sender, e) =>
            {
                if (System.Windows.Application.Current is App app)
                {
                    app.HandleUnhandledException("Program.ThreadException", e.Exception);
                }
                else
                {
                    DiagnosticLogger.LogException("Program.ThreadException", e.Exception);
                }
            };
        }

        /// <summary>
        /// Recovers from a corrupt user settings file, which otherwise makes every settings
        /// access throw and so prevents TaskSwitcher from starting until the file is deleted
        /// by hand.
        /// </summary>
        /// <returns>False when this process must exit.</returns>
        private static bool EnsureSettingsCanBeRead(string[] args)
        {
            try
            {
                // The first access loads the settings file.
                _ = Settings.Default.FirstRun;
                return true;
            }
            catch (ConfigurationException ex)
            {
                DiagnosticLogger.LogException("Program.EnsureSettingsCanBeRead", ex);

                // The configuration system keeps reporting the failure for the lifetime of the
                // process, so the defaults can only be loaded by a new one. The argument stops
                // the new process from trying again when setting the file aside did not help.
                bool alreadyReset = args.Contains(SettingsResetArgument, StringComparer.OrdinalIgnoreCase);
                if (!alreadyReset && TrySetAsideUserSettingsFile(ex) && TryRestart(SettingsResetArgument))
                {
                    return false;
                }

                System.Windows.MessageBox.Show(
                    "TaskSwitcher could not read its settings and was unable to reset them." +
                    Environment.NewLine + Environment.NewLine + ex.Message,
                    "Unable to Load Settings",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        private static bool TrySetAsideUserSettingsFile(Exception exception)
        {
            string settingsFile = null;
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is ConfigurationErrorsException { Filename.Length: > 0 } configurationError)
                {
                    settingsFile = configurationError.Filename;
                    break;
                }
            }

            // Only the per-user file is replaced. A problem in the configuration file that is
            // installed next to the executable cannot be repaired from here.
            if (settingsFile == null ||
                !string.Equals(Path.GetFileName(settingsFile), "user.config", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                File.Move(settingsFile, settingsFile + ".corrupt", overwrite: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DiagnosticLogger.LogException("Program.TrySetAsideUserSettingsFile", ex);
                return false;
            }
        }

        private static bool TryRestart(string argument)
        {
            string executablePath = GetExecutablePath();
            if (string.IsNullOrEmpty(executablePath))
            {
                return false;
            }

            try
            {
                using Process restartedProcess = Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = executablePath,
                    Arguments = argument
                });
                return true;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                DiagnosticLogger.LogException("Program.TryRestart", ex);
                return false;
            }
        }

        private static bool RunAsAdministratorIfConfigured()
        {
            using var perf = PerfRecorder.Measure("RunAsAdministratorIfConfigured");
            if (!RunAsAdminRequested() || IsRunAsAdmin()) return true;
            ProcessStartInfo proc = new()
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = GetExecutablePath(),
                Verb = "runas"
            };

            if (string.IsNullOrEmpty(proc.FileName))
            {
                ReportElevationFailure(new InvalidOperationException(
                    "The TaskSwitcher executable path could not be determined."));
                return false;
            }

            try
            {
                using Process elevatedProcess = Process.Start(proc);
                return false;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
            {
                // The user declined the UAC prompt. Exit the unelevated instance quietly.
                DiagnosticLogger.LogInfo("Program.RunAsAdministratorIfConfigured", "Elevation was canceled by the user.");
                return false;
            }
            catch (Exception ex)
            {
                ReportElevationFailure(ex);
                return false;
            }
        }

        private static void ReportElevationFailure(Exception exception)
        {
            DiagnosticLogger.LogException("Program.RunAsAdministratorIfConfigured", exception);
            System.Windows.MessageBox.Show(
                "TaskSwitcher could not restart with administrator privileges. " +
                "Check your Windows security settings or start TaskSwitcher manually as administrator.",
                "Unable to Start as Administrator",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }

        private static bool RunAsAdminRequested()
        {
            return Settings.Default.RunAsAdmin;
        }

        private static void MakePortable(ApplicationSettingsBase settings)
        {
            PortableSettingsProvider portableSettingsProvider = new();
            settings.Providers.Add(portableSettingsProvider);
            foreach (SettingsProperty prop in settings.Properties)
            {
                prop.Provider = portableSettingsProvider;
            }
            settings.Reload();
        }

        private static void MigrateUserSettings()
        {
            using var perf = PerfRecorder.Measure("MigrateUserSettings");
            if (!Settings.Default.FirstRun) return;

            Settings.Default.Upgrade();
            Settings.Default.FirstRun = false;
            Settings.Default.Save();
        }

        private static bool IsRunAsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(id);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static string GetExecutablePath()
        {
            // Environment.ProcessPath is the most reliable way to locate the current executable
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                return Environment.ProcessPath;
            }

            // Fallback to MainModule (can be null in some hosting scenarios)
            using Process currentProcess = Process.GetCurrentProcess();
            string modulePath = currentProcess.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(modulePath))
            {
                return modulePath;
            }

            // As a last resort, use entry assembly location
            return Assembly.GetEntryAssembly()?.Location ?? string.Empty;
        }
    }
}
