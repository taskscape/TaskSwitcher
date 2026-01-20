using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TaskSwitcher
{
    /// <summary>
    /// Lightweight diagnostic logger for capturing exceptions and debug information.
    /// Uses System.Threading.Channels for non-blocking async logging.
    /// Writes to %LOCALAPPDATA%\TaskSwitcher\logs\diagnostic.log when enabled.
    /// </summary>
    internal sealed class DiagnosticLogger : IAsyncDisposable
    {
        private static readonly Lazy<DiagnosticLogger> LazyInstance = new(() => new DiagnosticLogger());
        
        /// <summary>
        /// Singleton instance of the diagnostic logger.
        /// </summary>
        internal static DiagnosticLogger Instance => LazyInstance.Value;

        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly Channel<LogEntry> _logChannel;
        private readonly Task _writerTask;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Logging is enabled when a debugger is attached or the environment variable TASKSWITCHER_LOG=1 is set.
        /// </summary>
        internal static bool Enabled { get; set; } =
            Debugger.IsAttached ||
            string.Equals(Environment.GetEnvironmentVariable("TASKSWITCHER_LOG"), "1", StringComparison.OrdinalIgnoreCase);

        private DiagnosticLogger() : this(TimeProvider.System)
        {
        }

        /// <summary>
        /// Constructor for testing with a custom TimeProvider.
        /// </summary>
        internal DiagnosticLogger(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TaskSwitcher",
                "logs");
            _logFilePath = Path.Combine(_logDirectory, "diagnostic.log");

            // Bounded channel to prevent unbounded memory growth; oldest entries are dropped if full
            _logChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

            _shutdownCts = new CancellationTokenSource();
            _writerTask = Task.Run(() => ProcessLogEntriesAsync(_shutdownCts.Token));
        }

        /// <summary>
        /// Logs an exception with context information.
        /// </summary>
        internal static void LogException(string context, Exception exception)
        {
            if (!Enabled || exception == null) return;

            string message = $"[ERROR] {context}: {exception.GetType().Name} - {exception.Message}";
            Instance.EnqueueLog(message, exception.StackTrace);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        internal static void LogWarning(string context, string message)
        {
            if (!Enabled) return;

            Instance.EnqueueLog($"[WARN] {context}: {message}", stackTrace: null);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        internal static void LogInfo(string context, string message)
        {
            if (!Enabled) return;

            Instance.EnqueueLog($"[INFO] {context}: {message}", stackTrace: null);
        }

        private void EnqueueLog(string message, string stackTrace)
        {
            var entry = new LogEntry
            {
                Timestamp = _timeProvider.GetUtcNow(),
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId,
                Message = message,
                StackTrace = stackTrace
            };

            // TryWrite is non-blocking; if channel is full, oldest entry is dropped
            _logChannel.Writer.TryWrite(entry);
        }

        private async Task ProcessLogEntriesAsync(CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);

                await foreach (LogEntry entry in _logChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        string logLine = FormatLogEntry(entry);
                        await File.AppendAllTextAsync(_logFilePath, logLine + Environment.NewLine, cancellationToken);
                    }
                    catch
                    {
                        // Never let logging break the app
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch
            {
                // Never let logging break the app
            }
        }

        private static string FormatLogEntry(LogEntry entry)
        {
            string logLine = $"{entry.Timestamp:O} | PID:{entry.ProcessId} | TID:{entry.ThreadId} | {entry.Message}";

            if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                logLine += Environment.NewLine + "  StackTrace: " + 
                    entry.StackTrace.Replace(Environment.NewLine, Environment.NewLine + "    ");
            }

            return logLine;
        }

        /// <summary>
        /// Gracefully shuts down the logger, flushing any pending log entries.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _logChannel.Writer.TryComplete();

            try
            {
                // Wait for remaining entries to be written (with timeout)
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _writerTask.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout expired, proceed with shutdown
            }
            finally
            {
                await _shutdownCts.CancelAsync();
                _shutdownCts.Dispose();
            }
        }

        private readonly struct LogEntry
        {
            public DateTimeOffset Timestamp { get; init; }
            public int ProcessId { get; init; }
            public int ThreadId { get; init; }
            public string Message { get; init; }
            public string StackTrace { get; init; }
        }
    }
}
