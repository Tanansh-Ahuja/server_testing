using System;
using System.IO;

namespace ADL_Automation
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath;

        public static void Init(string logDirectory)
        {
            if (string.IsNullOrEmpty(logDirectory))
                throw new ArgumentNullException(nameof(logDirectory));

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Generate timestamped log file name
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string logFileName = $"tt_order_{timestamp}.log";
            _logFilePath = Path.Combine(logDirectory, logFileName);

            Log("Logger initialized.");
            Log($"Log file: {_logFilePath}");
            LogBlankLine();
        }

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(_logFilePath))
                throw new InvalidOperationException("Logger not initialized. Call Logger.Init(folderPath) first.");

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
            string fullMessage = $"[{timestamp}] {message}";

            lock (_lock)
            {
                File.AppendAllText(_logFilePath, fullMessage + Environment.NewLine);
            }

            Console.WriteLine(fullMessage);
        }

        public static void LogBlankLine()
        {
            if (string.IsNullOrEmpty(_logFilePath))
                throw new InvalidOperationException("Logger not initialized. Call Logger.Init(folderPath) first.");

            lock (_lock)
            {
                File.AppendAllText(_logFilePath, Environment.NewLine);
            }

            Console.WriteLine();
        }

        public static void LogError(Exception ex)
        {
            Log("ERROR: " + ex.Message);
            Log("STACK TRACE: " + ex.StackTrace);
        }
    }
}

