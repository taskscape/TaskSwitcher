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
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using TaskSwitcher.Properties;

namespace TaskSwitcher
{
    internal class Program
    {
        private const string mutex_id = "DBDE24E4-91F6-11DF-B495-C536DFD72085-TaskSwitcher";
        private const int ErrorCancelled = 1223;

        [STAThread]
        private static void Main()
        {
            try
            {
                using (PerfRecorder.Measure("AppStartup"))
                {
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
                            // Log the fact the mutex was abandoned in another process, it will still get aquired
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
