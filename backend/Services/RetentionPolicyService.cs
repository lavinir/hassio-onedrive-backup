using System.Globalization;
using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services;

public class RetentionPolicyService : IRetentionPolicyService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RetentionPolicyService> _logger;

    public RetentionPolicyService(IDateTimeProvider dateTimeProvider, ILogger<RetentionPolicyService> logger)
    {
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public IEnumerable<Backup> GetBackupsToDelete(IEnumerable<Backup> backups, BackupSettings settings, int maxCount)
    {
        var backupList = backups.ToList();
        if (backupList.Count == 0)
            return Enumerable.Empty<Backup>();

        var now = _dateTimeProvider.Now;

        // How many non-pinned backups are over the limit?
        int effectiveMax = maxCount > 0 ? maxCount : int.MaxValue;
        int nonPinnedCount = backupList.Count(b => !b.Retained);
        int excess = Math.Max(0, nonPinnedCount - effectiveMax);

        if (excess == 0)
            return Enumerable.Empty<Backup>();

        // Build the generational retain-set (backups the rules say to keep)
        bool useGenerational = settings.GenerationalDays.HasValue ||
                               settings.GenerationalWeeks.HasValue ||
                               settings.GenerationalMonths.HasValue ||
                               settings.GenerationalYears.HasValue;

        var retainSet = new HashSet<string>();
        if (useGenerational)
        {
            if (settings.GenerationalDays.HasValue)
                AddToRetainSet(GetDailyGenerations(backupList, settings.GenerationalDays.Value, now), "daily");

            if (settings.GenerationalWeeks.HasValue)
                AddToRetainSet(GetWeeklyGenerations(backupList, settings.GenerationalWeeks.Value, now), "weekly");

            if (settings.GenerationalMonths.HasValue)
                AddToRetainSet(GetMonthlyGenerations(backupList, settings.GenerationalMonths.Value, now), "monthly");

            if (settings.GenerationalYears.HasValue)
                AddToRetainSet(GetYearlyGenerations(backupList, settings.GenerationalYears.Value, now), "yearly");
        }

        // Eligible for deletion: non-pinned AND not in the generational retain-set, oldest first
        var toDelete = backupList
            .Where(b => !b.Retained && !retainSet.Contains(b.Slug))
            .OrderBy(b => b.Date)
            .Take(excess)
            .ToList();

        if (toDelete.Count > 0)
            _logger.LogInformation("Retention policy: {Count} backup(s) marked for deletion: {Slugs}",
                toDelete.Count, string.Join(", ", toDelete.Select(b => b.Slug)));

        return toDelete;

        void AddToRetainSet(IEnumerable<Backup> retained, string generation)
        {
            foreach (var b in retained)
            {
                if (retainSet.Add(b.Slug))
                    _logger.LogDebug("Retaining {Slug} ({Date:yyyy-MM-dd}) for {Generation} generational policy",
                        b.Slug, b.Date, generation);
            }
        }
    }

    private static IEnumerable<Backup> GetDailyGenerations(List<Backup> backups, int days, DateTime now)
    {
        if (days <= 0) return Enumerable.Empty<Backup>();
        return backups.Where(b => (now.Date - b.Date.Date).TotalDays < days);
    }

    private static IEnumerable<Backup> GetWeeklyGenerations(List<Backup> backups, int weeks, DateTime now)
    {
        if (weeks <= 0) return Enumerable.Empty<Backup>();

        var firstDayOfWeek = DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek;

        // Pick the newest backup per calendar week
        var newestPerWeek = backups
            .GroupBy(b => (
                Year: b.Date.Year,
                Week: CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(b.Date.Date, CalendarWeekRule.FirstDay, firstDayOfWeek)
            ))
            .Select(g => g.OrderByDescending(b => b.Date).First())
            .ToList();

        int currentWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now.Date, CalendarWeekRule.FirstDay, firstDayOfWeek);
        int currentYear = now.Year;

        var result = new List<Backup>();
        for (int i = 0; i < weeks; i++)
        {
            int year = currentYear;
            int week = currentWeek - i;
            while (week < 1)
            {
                year--;
                week += CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(new DateTime(year, 12, 31), CalendarWeekRule.FirstDay, firstDayOfWeek);
            }

            var match = newestPerWeek.FirstOrDefault(b =>
                b.Date.Year == year &&
                CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(b.Date.Date, CalendarWeekRule.FirstDay, firstDayOfWeek) == week);

            if (match != null)
                result.Add(match);
        }

        return result.Distinct();
    }

    private static IEnumerable<Backup> GetMonthlyGenerations(List<Backup> backups, int months, DateTime now)
    {
        if (months <= 0) return Enumerable.Empty<Backup>();

        var newestPerMonth = backups
            .GroupBy(b => (b.Date.Year, b.Date.Month))
            .Select(g => g.OrderByDescending(b => b.Date).First())
            .ToList();

        var result = new List<Backup>();
        for (int i = 0; i < months; i++)
        {
            int month = now.Month - i;
            int year = now.Year;
            while (month < 1) { month += 12; year--; }

            var match = newestPerMonth.FirstOrDefault(b => b.Date.Year == year && b.Date.Month == month);
            if (match != null)
                result.Add(match);
        }

        return result.Distinct();
    }

    private static IEnumerable<Backup> GetYearlyGenerations(List<Backup> backups, int years, DateTime now)
    {
        if (years <= 0) return Enumerable.Empty<Backup>();

        var newestPerYear = backups
            .GroupBy(b => b.Date.Year)
            .Select(g => g.OrderByDescending(b => b.Date).First())
            .ToList();

        var result = new List<Backup>();
        for (int i = 0; i < years; i++)
        {
            var match = newestPerYear.FirstOrDefault(b => b.Date.Year == now.Year - i);
            if (match != null)
                result.Add(match);
        }

        return result.Distinct();
    }
}
