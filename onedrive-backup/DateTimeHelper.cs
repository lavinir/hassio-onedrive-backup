namespace hassio_onedrive_backup
{
    public class DateTimeHelper
    {
        public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private readonly TimeZoneInfo? _timeZoneInfo = null;

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
                var ret = _timeZoneInfo != null ? TimeZoneInfo.ConvertTime(now, _timeZoneInfo) : now;
                return ret;
            }
        }

        public static DateTime GetStartDateByWeekAndYear(int year, int week, DayOfWeek firstDayOfWeek)
        {
			DateTime jan1 = new DateTime(year, 1, 1);
			DateTime weekStartDate = jan1.AddDays((week - 1) * 7 - (int)jan1.DayOfWeek + (int)firstDayOfWeek);
            return weekStartDate;
		}
    }
}
