using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace TaskSwitcher
{
    /// <summary>
    /// Lightweight diagnostic logger for capturing exceptions and debug information.
    /// Writes to %LOCALAPPDATA%\TaskSwitcher\logs\diagnostic.log when enabled.
    /// </summary>
    internal static class DiagnosticLogger
    {
        private static readonly object Sync = new();
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskSwitcher",
            "logs");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "diagnostic.log");

        /// <summary>
        /// Logging is enabled when a debugger is attached or the environment variable TASKSWITCHER_LOG=1 is set.
        /// </summary>
        internal static bool Enabled { get; set; } =
            Debugger.IsAttached ||
            string.Equals(Environment.GetEnvironmentVariable("TASKSWITCHER_LOG"), "1", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Logs an exception with context information.
        /// </summary>
        /// <param name="context">A description of where the exception occurred.</param>
        /// <param name="exception">The exception to log.</param>
        internal static void LogException(string context, Exception exception)
        {
            if (!Enabled || exception == null) return;

            string message = $"[ERROR] {context}: {exception.GetType().Name} - {exception.Message}";
            Write(message, exception.StackTrace);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="context">A description of where the warning occurred.</param>
        /// <param name="message">The warning message.</param>
        internal static void LogWarning(string context, string message)
        {
            if (!Enabled) return;

            Write($"[WARN] {context}: {message}", stackTrace: null);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="context">A description of the context.</param>
        /// <param name="message">The informational message.</param>
        internal static void LogInfo(string context, string message)
        {
            if (!Enabled) return;

            Write($"[INFO] {context}: {message}", stackTrace: null);
        }

        private static void Write(string message, string stackTrace)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(LogDirectory);

                    string timestamp = DateTimeOffset.Now.ToString("O");
                    int processId = Environment.ProcessId;
                    int threadId = Thread.CurrentThread.ManagedThreadId;

                    string logEntry = $"{timestamp} | PID:{processId} | TID:{threadId} | {message}";

                    if (!string.IsNullOrEmpty(stackTrace))
                    {
                        logEntry += Environment.NewLine + "  StackTrace: " + stackTrace.Replace(Environment.NewLine, Environment.NewLine + "    ");
                    }

                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Never let logging break the app - this is intentionally empty
                // as we cannot log a logging failure without risking infinite recursion.
            }
        }
    }
}
