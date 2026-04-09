using System.Collections;
using System.Text;
using Microsoft.Extensions.Logging;

namespace HassioOneDriveBackup.Utils
{
    public static class TimeRangeHelper
    {
        private static readonly ILogger _logger =
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger(nameof(TimeRangeHelper));

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

                    if (!int.TryParse(fromStr, out int from) || !int.TryParse(toStr, out int to)
                        || from < 0 || from > 23 || to < 0 || to > 23 || from > to)
                    {
                        _logger.LogWarning(
                            "Invalid allowed hours expression '{Expression}' (section '{Section}') — falling back to all hours allowed",
                            allowedHoursExpression, section);
                        allowedHours.SetAll(true);
                        return allowedHours;
                    }

                    for (int i = from; i <= to; i++)
                        allowedHours.Set(i, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse allowed hours expression '{Expression}' — falling back to all hours allowed",
                    allowedHoursExpression);
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
            for (int i = 0; i < bitArray.Length; i++)
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