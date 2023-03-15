using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup
{
    public static class ConsoleLogger
    {
		private static LogLevel _logLevel;

		public static void LogError(string msg)
        {
            WriteLog(LogLevel.Error, msg);
        }

        public static void LogWarning(string msg)
        {
            WriteLog(LogLevel.Warning, msg);
        }

        public static void LogInfo(string msg)
        {
            WriteLog(LogLevel.Info, msg);
        }

        public static void LogVerbose(string msg)
        {
            WriteLog(LogLevel.Verbose, msg);
        }

        public static void SetLogLevel(LogLevel level)
        {
			_logLevel = level;
		}

        private static void WriteLog(LogLevel level, string msg)
        {
            if (level < _logLevel)
            {
				return;
			}

            var timestamp = DateTimeHelper.Instance?.Now;
            string logMsg = $"{timestamp} [{Thread.CurrentThread.ManagedThreadId}] {level}: {msg}";

            if (level == LogLevel.Error)
            {
                Console.Error.WriteLine(logMsg);
            }
            else
            {
                Console.WriteLine(logMsg);
            }
        }

        public enum LogLevel
        {
            Verbose,
            Info,
            Warning,
            Error,
        }
    }
}
