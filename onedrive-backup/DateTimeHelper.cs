using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup
{
    internal static class DateTimeHelper
    {
#if DEBUG
        private static DateTimeKind _dateTimeKind = DateTimeKind.Local;
#else

        private static DateTimeKind _dateTimeKind = DateTimeKind.Utc;
#endif

        public static DateTime Now
        {
            get
            {
                var now = DateTime.Now;
                DateTime.SpecifyKind(now, _dateTimeKind);
                return now.ToLocalTime();
            }
        }
    }
}
