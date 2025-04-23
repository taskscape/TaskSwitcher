using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace TaskSwitcher.Core.Utilities
{
    /// <summary>
    /// Thread-safe logging utility that writes to both Debug output and a log file
    /// </summary>
    public static class Logger
    {
        private static readonly object LogLock = new object();
        private static string _logFilePath;
        private static bool _isInitialized = false;
        private static int _logFileMaxSizeMB = 5;
        
        /// <summary>
        /// Initialize the logger with a specific log file path
        /// </summary>
        public static void Initialize(string logFilePath = null, int maxSizeMB = 5)
        {
            lock (LogLock)
            {
                if (string.IsNullOrEmpty(logFilePath))
                {
                    string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string logFolder = Path.Combine(appDataFolder, "TaskSwitcher", "Logs");
                    
                    // Create the log directory if it doesn't exist
                    if (!Directory.Exists(logFolder))
                    {
                        Directory.CreateDirectory(logFolder);
                    }
                    
                    // Use date-based log filename
                    string logFileName = $"TaskSwitcher_{DateTime.Now:yyyy-MM-dd}.log";
                    logFilePath = Path.Combine(logFolder, logFileName);
                }
                
                _logFilePath = logFilePath;
                _logFileMaxSizeMB = maxSizeMB;
                _isInitialized = true;
                
                // Log startup information
                Info($"Logger initialized. Log file: {_logFilePath}");
                Info($"Application started at {DateTime.Now}");
                Info($"OS Version: {Environment.OSVersion.VersionString}");
                Info($"Process ID: {Process.GetCurrentProcess().Id}");
                Info($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }
        }
        
        /// <summary>
        /// Write an information message to the log
        /// </summary>
        public static void Info(string message)
        {
            Log("INFO", message);
        }
        
        /// <summary>
        /// Write a debug message to the log
        /// </summary>
        public static void Debug(string message)
        {
            Log("DEBUG", message);
        }
        
        /// <summary>
        /// Write a warning message to the log
        /// </summary>
        public static void Warning(string message)
        {
            Log("WARNING", message);
        }
        
        /// <summary>
        /// Write an error message to the log
        /// </summary>
        public static void Error(string message)
        {
            Log("ERROR", message);
        }
        
        /// <summary>
        /// Write an error message with exception details to the log
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine($"Exception: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"StackTrace: {ex.StackTrace}");
            
            // Include inner exception if present
            if (ex.InnerException != null)
            {
                sb.AppendLine($"Inner Exception: {ex.InnerException.GetType().Name}");
                sb.AppendLine($"Inner Message: {ex.InnerException.Message}");
                sb.AppendLine($"Inner StackTrace: {ex.InnerException.StackTrace}");
            }
            
            Log("ERROR", sb.ToString());
        }
        
        /// <summary>
        /// Internal method to write to both debug output and log file
        /// </summary>
        private static void Log(string level, string message)
        {
            // Initialize with default settings if not already initialized
            if (!_isInitialized)
            {
                Initialize();
            }
            
            // Format the log entry
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(4, '0');
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] [{threadId}] [{level}] {message}";
            
            // Always write to debug output
            System.Diagnostics.Debug.WriteLine(logEntry);
            
            // Write to log file with thread safety
            try
            {
                lock (LogLock)
                {
                    // Check if log file is too large and rotate if needed
                    RotateLogFileIfNeeded();
                    
                    // Append to log file
                    using (StreamWriter writer = new StreamWriter(_logFilePath, true, Encoding.UTF8))
                    {
                        writer.WriteLine(logEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                // Just write to debug output if file logging fails
                System.Diagnostics.Debug.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Rotate log file if it exceeds the maximum size
        /// </summary>
        private static void RotateLogFileIfNeeded()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    FileInfo fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length > _logFileMaxSizeMB * 1024 * 1024)
                    {
                        string directory = Path.GetDirectoryName(_logFilePath);
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(_logFilePath);
                        string extension = Path.GetExtension(_logFilePath);
                        string newFileName = $"{fileNameWithoutExt}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
                        string backupPath = Path.Combine(directory, newFileName);
                        
                        // Move the current log file to the backup location
                        File.Move(_logFilePath, backupPath);
                        
                        // Clean up old log files (keep last 5)
                        CleanupOldLogFiles(directory, 5);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rotating log file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Clean up old log files, keeping only the specified number of newest files
        /// </summary>
        private static void CleanupOldLogFiles(string directory, int filesToKeep)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(directory);
                FileInfo[] logFiles = di.GetFiles("TaskSwitcher_*.log");
                
                // Sort by last write time, oldest first
                Array.Sort(logFiles, (f1, f2) => f1.LastWriteTime.CompareTo(f2.LastWriteTime));
                
                // Delete oldest files beyond the keep count
                int filesToDelete = logFiles.Length - filesToKeep;
                for (int i = 0; i < filesToDelete; i++)
                {
                    if (i < logFiles.Length)
                    {
                        try
                        {
                            logFiles[i].Delete();
                        }
                        catch
                        {
                            // Ignore errors when trying to delete log files
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors in cleanup
            }
        }
    }
}