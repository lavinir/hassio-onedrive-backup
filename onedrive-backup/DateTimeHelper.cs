using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup
{
    internal class DateTimeHelper
    {
        private readonly TimeZoneInfo? _timeZoneInfo = null;

#if DEBUG
        private static DateTimeKind _dateTimeKind = DateTimeKind.Local;
#else

        private static DateTimeKind _dateTimeKind = DateTimeKind.Utc;
#endif

        private DateTimeHelper(string timeZoneId)
        {
            try
            {
                _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Error finding timezone: {ex}. Falling back to UTC");                
            }
        }

        public static DateTimeHelper? Instance { get; private set; }

        public static DateTimeHelper Initialize(string timeZoneId)
        {
            Instance = new DateTimeHelper(timeZoneId);
            return Instance;
        }

        public DateTime Now
        {
            get
            {
                var now = DateTime.Now;
                // var ret = DateTime.SpecifyKind(now, _dateTimeKind);
                var ret = _timeZoneInfo != null ? TimeZoneInfo.ConvertTime(now, _timeZoneInfo) : now;
                return ret;
            }
        }
    }
}
