using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace TaskSwitcher
{
    /// <summary>
    /// Lightweight performance recorder intended for debug builds. It writes simple CSV lines
    /// to %LOCALAPPDATA%\TaskSwitcher\perf\perf.log for post-run analysis.
    /// </summary>
    internal static class PerfRecorder
    {
        private static readonly object Sync = new();
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskSwitcher",
            "perf");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "perf.log");

        /// <summary>
        /// Recording is enabled when a debugger is attached or the environment variable TASKSWITCHER_PERF=1 is set.
        /// </summary>
        internal static bool Enabled { get; set; } =
            Debugger.IsAttached ||
            string.Equals(Environment.GetEnvironmentVariable("TASKSWITCHER_PERF"), "1", StringComparison.OrdinalIgnoreCase);

        internal static IDisposable Measure(string name)
        {
            return Enabled ? new Scope(name) : NullScope.Instance;
        }

        private static void Write(string name, TimeSpan elapsed)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(LogDirectory);
                    string line = $"{DateTimeOffset.Now:O},{name},{elapsed.TotalMilliseconds:F3},{Environment.ProcessId},{Thread.CurrentThread.ManagedThreadId}";
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never let diagnostics break the app.
            }
        }

        private sealed class Scope : IDisposable
        {
            private readonly string _name;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;

            internal Scope(string name)
            {
                _name = name;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _stopwatch.Stop();
                Write(_name, _stopwatch.Elapsed);
            }
        }

        private sealed class NullScope : IDisposable
        {
            internal static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
