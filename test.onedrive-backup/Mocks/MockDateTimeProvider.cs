using onedrive_backup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test.onedrive_backup.Mocks
{
    internal class MockDateTimeProvider : IDateTimeProvider
    {
        private DateTime _now = DateTime.Now;

        public DateTime Now => _now;

        public void SetNow(DateTime now)
        {
            _now = now;
        }

        public void TimeSeek(int days = 0, int months = 0, int years = 0)
        {
            _now = _now.AddDays(days).AddMonths(months).AddYears(years);
        }
    }
}
