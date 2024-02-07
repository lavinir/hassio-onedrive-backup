using System.Collections;
using System.Text;

namespace hassio_onedrive_backup
{
    public static class TimeRangeHelper
    {
        public static BitArray GetAllowedHours(string? allowedHoursExpression)
        {
            var allowedHours = new BitArray(24);
            if (string.IsNullOrWhiteSpace(allowedHoursExpression))
            {
                allowedHours.SetAll(true);
                return allowedHours;
            }

            try
            {
                var sections = allowedHoursExpression.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (string section in sections)
                {
                    string fromStr;
                    string toStr;

                    fromStr = section.StartsWith('-') ? "0" : section.Split('-').FirstOrDefault(s => string.IsNullOrWhiteSpace(s) == false, "0");
                    toStr = section.EndsWith('-') ? "23" : section.Split('-').LastOrDefault(s => string.IsNullOrWhiteSpace(s) == false, "23");

                    int from = int.Parse(fromStr);
                    int to = int.Parse(toStr);

                    for (int i = from; i <= to; i++)
                    {
                        allowedHours.Set(i, true);
                    }
                }

            }
            catch (Exception ex)
            {
                allowedHours.SetAll(true);
            }

            return allowedHours;
        }

        public static DateTime? GetClosestAllowedTimeSlot(DateTime target, string? allowedHoursStr)
        {
            var allowedHours = GetAllowedHours(allowedHoursStr);
            if (allowedHours == null || allowedHours[target.Hour])
            {
                return target;
            }

            for (int i = 1; i < 24; i++)
            {
                target = target.AddHours(1);
                if (allowedHours[target.Hour])
                {
                    return target;
                }
            }

            return null;
        }

        public static string ToAllowedHoursText(this BitArray bitArray) 
        {
            var sb = new StringBuilder();
            for (int i=0; i< bitArray.Length; i++)
            {
                if (bitArray[i])
                {
                    sb.Append($"{i},");
                }
            }

            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
    }
}
