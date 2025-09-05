using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FollowTheWay.Utils
{
    public enum LogLevel
    {
        None = 0,     // No logs
        Error = 1,    // Only critical errors (DEFAULT)
        Warning = 2,  // Errors + warnings  
        Info = 3,     // Standard information
        Debug = 4,    // Debug information
        Verbose = 5   // Everything including performance metrics
    }

    public class ModLogger
    {
        private readonly ManualLogSource _logger;
        private readonly string _logFilePath;
        private readonly object _lockObject;
        private readonly Queue<string> _logQueue;
        private readonly Timer _flushTimer;
        private readonly StringBuilder _buffer;
        private readonly bool _enableFileLogging;
        private readonly bool _enablePerformanceLogging;
        private readonly Dictionary<string, DateTime> _performanceMarkers;

        private static LogLevel _currentLevel = LogLevel.Error;

        // Static instance for global access
        public static ModLogger Instance { get; set; }

        public static LogLevel CurrentLevel
        {
            get => _currentLevel;
            set => _currentLevel = value;
        }

        public ModLogger(ManualLogSource logger, bool enableFileLogging = true, bool enablePerformanceLogging = false)
        {
            _logger = logger;
            _enableFileLogging = enableFileLogging;
            _enablePerformanceLogging = enablePerformanceLogging;
            _lockObject = new object();
            _logQueue = new Queue<string>();
            _buffer = new StringBuilder();
            _performanceMarkers = new Dictionary<string, DateTime>();

            if (_enableFileLogging)
            {
                try
                {
                    string logDir = Path.Combine(Application.persistentDataPath, "FollowTheWay", "Logs");
                    Directory.CreateDirectory(logDir);
                    _logFilePath = Path.Combine(logDir, $"followtheway_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                    // Initialize log file with header
                    WriteToFile($"=== FollowTheWay Mod Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    WriteToFile($"Unity Version: {Application.unityVersion}");
                    WriteToFile($"Platform: {Application.platform}");
                    WriteToFile($"Log Level: {_currentLevel}");
                    WriteToFile("=====================================");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to initialize file logging: {ex.Message}");
                    _enableFileLogging = false;
                }
            }

            // Setup flush timer (flush every 5 seconds)
            if (_enableFileLogging)
            {
                _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            }
        }

        public void Error(string message)
        {
            if (_currentLevel >= LogLevel.Error)
            {
                string formattedMessage = FormatMessage("ERROR", message);
                _logger.LogError(formattedMessage);
                QueueForFile(formattedMessage);
            }
        }

        public void Error(string message, Exception exception)
        {
            if (_currentLevel >= LogLevel.Error)
            {
                string formattedMessage = FormatMessage("ERROR", $"{message}\nException: {exception}");
                _logger.LogError(formattedMessage);
                QueueForFile(formattedMessage);
            }
        }

        public void Warning(string message)
        {
            if (_currentLevel >= LogLevel.Warning)
            {
                string formattedMessage = FormatMessage("WARN", message);
                _logger.LogWarning(formattedMessage);
                QueueForFile(formattedMessage);
            }
        }

        public void Info(string message)
        {
            if (_currentLevel >= LogLevel.Info)
            {
                string formattedMessage = FormatMessage("INFO", message);
                _logger.LogInfo(formattedMessage);
                QueueForFile(formattedMessage);
            }
        }

        public void Debug(string message)
        {
            if (_currentLevel >= LogLevel.Debug)
            {
                string formattedMessage = FormatMessage("DEBUG", message);
                _logger.LogInfo(formattedMessage);
                QueueForFile(formattedMessage);
            }
        }

        public void Verbose(string message)
        {
            if (_currentLevel >= LogLevel.Verbose)
            {
                string formattedMessage = FormatMessage("VERBOSE", message);
                _logger.LogInfo(formattedMessage);
                QueueForFile(formattedMessage);
            }
        }

        // Performance logging methods
        public void StartPerformanceMarker(string markerName)
        {
            if (_enablePerformanceLogging && _currentLevel >= LogLevel.Verbose)
            {
                lock (_performanceMarkers)
                {
                    _performanceMarkers[markerName] = DateTime.UtcNow;
                }
                Verbose($"Performance marker started: {markerName}");
            }
        }

        public void EndPerformanceMarker(string markerName)
        {
            if (_enablePerformanceLogging && _currentLevel >= LogLevel.Verbose)
            {
                lock (_performanceMarkers)
                {
                    if (_performanceMarkers.TryGetValue(markerName, out DateTime startTime))
                    {
                        var duration = DateTime.UtcNow - startTime;
                        Verbose($"Performance marker ended: {markerName} - Duration: {duration.TotalMilliseconds:F2}ms");
                        _performanceMarkers.Remove(markerName);
                    }
                    else
                    {
                        Warning($"Performance marker '{markerName}' was not started");
                    }
                }
            }
        }

        public void LogPerformance(string operation, TimeSpan duration)
        {
            if (_enablePerformanceLogging && _currentLevel >= LogLevel.Verbose)
            {
                Verbose($"Performance: {operation} took {duration.TotalMilliseconds:F2}ms");
            }
        }

        // Memory usage logging
        public void LogMemoryUsage(string context = "")
        {
            if (_currentLevel >= LogLevel.Verbose)
            {
                try
                {
                    long totalMemory = GC.GetTotalMemory(false);
                    string memoryInfo = $"Memory Usage{(string.IsNullOrEmpty(context) ? "" : $" ({context})")}: {totalMemory / 1024 / 1024:F2} MB";
                    Verbose(memoryInfo);
                }
                catch (Exception ex)
                {
                    Warning($"Failed to get memory usage: {ex.Message}");
                }
            }
        }

        // System information logging
        public void LogSystemInfo()
        {
            if (_currentLevel >= LogLevel.Info)
            {
                Info("=== System Information ===");
                Info($"Unity Version: {Application.unityVersion}");
                Info($"Platform: {Application.platform}");
                Info($"System Language: {Application.systemLanguage}");
                Info($"Persistent Data Path: {Application.persistentDataPath}");
                Info($"Temporary Cache Path: {Application.temporaryCachePath}");
                Info($"Is Editor: {Application.isEditor}");
                Info($"Target Frame Rate: {Application.targetFrameRate}");
                Info("========================");
            }
        }

        // Conditional logging methods
        public void LogIf(bool condition, LogLevel level, string message)
        {
            if (condition)
            {
                switch (level)
                {
                    case LogLevel.Error:
                        Error(message);
                        break;
                    case LogLevel.Warning:
                        Warning(message);
                        break;
                    case LogLevel.Info:
                        Info(message);
                        break;
                    case LogLevel.Debug:
                        Debug(message);
                        break;
                    case LogLevel.Verbose:
                        Verbose(message);
                        break;
                }
            }
        }

        // Batch logging for collections
        public void LogCollection<T>(IEnumerable<T> collection, string collectionName, LogLevel level = LogLevel.Debug)
        {
            if (_currentLevel >= level && collection != null)
            {
                var items = collection.ToList();
                LogAtLevel(level, $"{collectionName} contains {items.Count} items:");

                int index = 0;
                foreach (var item in items.Take(10)) // Limit to first 10 items
                {
                    LogAtLevel(level, $"  [{index}]: {item}");
                    index++;
                }

                if (items.Count > 10)
                {
                    LogAtLevel(level, $"  ... and {items.Count - 10} more items");
                }
            }
        }

        private void LogAtLevel(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Error:
                    Error(message);
                    break;
                case LogLevel.Warning:
                    Warning(message);
                    break;
                case LogLevel.Info:
                    Info(message);
                    break;
                case LogLevel.Debug:
                    Debug(message);
                    break;
                case LogLevel.Verbose:
                    Verbose(message);
                    break;
            }
        }

        private string FormatMessage(string level, string message)
        {
            return $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
        }

        private void QueueForFile(string message)
        {
            if (_enableFileLogging)
            {
                lock (_lockObject)
                {
                    _logQueue.Enqueue(message);
                }
            }
        }

        private void FlushLogs(object state)
        {
            if (!_enableFileLogging) return;

            List<string> logsToWrite = new List<string>();

            lock (_lockObject)
            {
                while (_logQueue.Count > 0)
                {
                    logsToWrite.Add(_logQueue.Dequeue());
                }
            }

            if (logsToWrite.Count > 0)
            {
                try
                {
                    foreach (string log in logsToWrite)
                    {
                        _buffer.AppendLine(log);
                    }

                    File.AppendAllText(_logFilePath, _buffer.ToString());
                    _buffer.Clear();
                }
                catch (Exception ex)
                {
                    // Use Unity's Debug instead of our logger to avoid recursion
                    UnityEngine.Debug.LogError($"Failed to write to log file: {ex.Message}");
                }
            }
        }

        private void WriteToFile(string message)
        {
            if (_enableFileLogging)
            {
                try
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Failed to write to log file: {ex.Message}");
                }
            }
        }

        // Cleanup method
        public void Dispose()
        {
            try
            {
                _flushTimer?.Dispose();
                FlushLogs(null); // Final flush

                if (_enableFileLogging)
                {
                    WriteToFile($"=== FollowTheWay Mod Log Ended at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error during ModLogger disposal: {ex.Message}");
            }
        }

        // Static convenience methods
        public static void LogError(string message) => Instance?.Error(message);
        public static void LogWarning(string message) => Instance?.Warning(message);
        public static void LogInfo(string message) => Instance?.Info(message);
        public static void LogDebug(string message) => Instance?.Debug(message);
        public static void LogVerbose(string message) => Instance?.Verbose(message);
    }

    // Extension methods for easier logging
    public static class LoggerExtensions
    {
        public static void LogWithContext(this ModLogger logger, LogLevel level, string context, string message)
        {
            string contextualMessage = $"[{context}] {message}";

            switch (level)
            {
                case LogLevel.Error:
                    logger.Error(contextualMessage);
                    break;
                case LogLevel.Warning:
                    logger.Warning(contextualMessage);
                    break;
                case LogLevel.Info:
                    logger.Info(contextualMessage);
                    break;
                case LogLevel.Debug:
                    logger.Debug(contextualMessage);
                    break;
                case LogLevel.Verbose:
                    logger.Verbose(contextualMessage);
                    break;
            }
        }
    }
}