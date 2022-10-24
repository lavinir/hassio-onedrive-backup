using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup
{
    internal static class ConsoleLogger
    {
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

        private static void WriteLog(LogLevel level, string msg)
        {
            var timestamp = DateTime.Now;
            string logMsg = $"{timestamp} {level}: {msg}";

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
