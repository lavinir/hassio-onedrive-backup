using onedrive_backup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup
{
    public class ConsoleLogger
    {
		private IDateTimeProvider _dateTimeProvider = null;
		private LogLevel _logLevel;

		public void LogError(string msg)
        {
            WriteLog(LogLevel.Error, msg);
        }

        public void LogWarning(string msg)
        {
            WriteLog(LogLevel.Warning, msg);
        }

        public void LogInfo(string msg)
        {
            WriteLog(LogLevel.Info, msg);
        }

        public void LogVerbose(string msg)
        {
            WriteLog(LogLevel.Verbose, msg);
        }

        public void SetLogLevel(LogLevel level)
        {
			_logLevel = level;
		}

        public void SetDateTimeProvider(IDateTimeProvider dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;
        }

        private void WriteLog(LogLevel level, string msg)
        {
            if (level < _logLevel)
            {
				return;
			}

            var timestamp = _dateTimeProvider?.Now;
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
