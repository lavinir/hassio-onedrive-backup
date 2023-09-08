using hassio_onedrive_backup;
using hassio_onedrive_backup.Contracts;
using Microsoft.VisualBasic;
using onedrive_backup.Contracts;
using onedrive_backup.Hass;
using onedrive_backup.Models;
using System.Globalization;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;
using Addon = hassio_onedrive_backup.Contracts.HassAddonsResponse.Addon;

namespace onedrive_backup.Extensions
{
    public static class BackupExtensions
    {
        public static BackupModel ToBackupModel(this Backup backup, HassContext hassContext)
        {
            return new BackupModel
            {
                Slug = backup.Slug,
                Date = backup.Date,
                Name = backup.Name,
                Type = backup.Type,
                Size = backup.Size,
                IsProtected = backup.Protected,
                Location = BackupModel.BackupLocation.Local,
                Addons = backup.Content?.Addons?.Select(slug => new Addon { Slug = slug, Name = GetAddonNameFromSlug(hassContext.Addons, slug) }).ToList() ?? Enumerable.Empty<Addon>(),
                Folders = backup.Content?.Folders ?? Enumerable.Empty<string>()
            };
        }

		public static BackupModel ToBackupModel(this OnedriveBackup onedriveBackup, HassContext hassContext)
        {
            return new BackupModel
            {
                Slug = onedriveBackup.Slug,
                Date = onedriveBackup.BackupDate,
                OneDriveFileName = onedriveBackup.FileName,
                Type = onedriveBackup.Type,
                Size = onedriveBackup.Size,
                IsProtected = onedriveBackup.IsProtected,
                Location = BackupModel.BackupLocation.OneDrive,
                Addons = onedriveBackup.Addons?.Select(slug => new Addon { Slug = slug, Name = GetAddonNameFromSlug(hassContext.Addons, slug) }) ?? Enumerable.Empty<Addon>(),
                Folders = onedriveBackup.Folders ?? Enumerable.Empty<string>()
			};
        } 

        public static OnedriveBackup ToOneDriveBackup(this BackupModel backupModel)
        {
            return new OnedriveBackup
            {
                Addons = backupModel.Addons.Select(addon => addon.Slug),
                BackupDate = backupModel.Date,
                FileName = backupModel.OneDriveFileName,
                Folders = backupModel.Folders,
                IsProtected = backupModel.IsProtected,
                Slug = backupModel.Slug,
                Type = backupModel.Type,
                Size = backupModel.Size
            };
        }

        public static Backup ToBackup(this BackupModel backupModel)
        {
            return new Backup
            {
                Date = backupModel.Date,
                Protected = backupModel.IsProtected,
                Slug = backupModel.Slug,
                Type = backupModel.Type,
                Size = backupModel.Size,
                Name = backupModel.DisplayName,
                Content = new Content
                {
                    Addons = backupModel.Addons.Select(addon => addon.Slug).ToArray() ?? Enumerable.Empty<string>().ToArray(),
                    Folders = backupModel.Folders?.ToArray() ?? Enumerable.Empty<string>().ToArray()
                }
            };
        }

		public static IEnumerable<IBackup> GetDailyGenerations(this IEnumerable<IBackup> backups, int dailyBackupNum)
		{
            if (dailyBackupNum < 0)
            {
                ConsoleLogger.LogWarning($"Daily Backup Num configured to {dailyBackupNum}");
                return Enumerable.Empty<IBackup>();
            }

			var now = DateTimeHelper.Instance.Now.Date;
			return backups.Where(backup => now - backup.BackupDate.Date < TimeSpan.FromDays(dailyBackupNum));
		}

		public static IEnumerable<IBackup> GetWeeklyGenerations(this IEnumerable<IBackup> backups, int weeklyBackupNum, DayOfWeek firstDayOfWeek)
		{
			if (weeklyBackupNum < 0)
			{
				ConsoleLogger.LogWarning($"Weekly Backup Num configured to {weeklyBackupNum}");
                yield break;
			}

			var now = DateTimeHelper.Instance.Now.Date;
            var currentWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now, CalendarWeekRule.FirstDay, firstDayOfWeek);
            var currentYear = now.Year;
            var groupedBackups = backups.GroupBy(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate.Date, CalendarWeekRule.FirstDay, firstDayOfWeek))
                   .Select(weekGroup => weekGroup.Max())
                   .OrderByDescending(backup => backup.BackupDate)
                   .Take(weeklyBackupNum)
                   .ToList();

            for (int i=0; i < weeklyBackupNum; i++)
            {
				var week = currentWeek - i;
                if (week < 1)
                {
                    currentYear -= 1;
                    week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now.AddYears(-1), CalendarWeekRule.FirstDay, firstDayOfWeek) - week;
                }

				var weeklyBackup = groupedBackups.FirstOrDefault(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate.Date, CalendarWeekRule.FirstDay, firstDayOfWeek) == week);
				if (weeklyBackup != null)
                {
					yield return weeklyBackup;
				}
                else
                {
                    var weekCutoffDate = DateTimeHelper.GetStartDateByWeekAndYear(currentYear, currentWeek, firstDayOfWeek);
                    var nextCandidateBackup = backups.OrderByDescending(b => b.BackupDate).FirstOrDefault(b => b.BackupDate <= weekCutoffDate);
                    if (nextCandidateBackup != null)
                    {
                        yield return nextCandidateBackup;
                    }

                    yield break;
                }
			}                   
		}

        public static IEnumerable<IBackup> GetMonthlyGenerations(this IEnumerable<IBackup> backups, int monthlyBackupNum)
        {
			if (monthlyBackupNum < 0)
			{
				ConsoleLogger.LogWarning($"Monthly Backup Num configured to {monthlyBackupNum}");
                yield break;
			}

			var now = DateTimeHelper.Instance.Now.Date;
            var currentMonth = now.Month;
            var currentYear = now.Year;
			var groupedBackups = backups.GroupBy(backup => backup.BackupDate.Month)
				  .Select(monthGrp => monthGrp.Max())
				  .OrderByDescending(backup => backup.BackupDate)
				  .Take(monthlyBackupNum)
				  .ToList();

            var year = currentYear;
			for (int i = 0; i < monthlyBackupNum; i++)
			{
				var month = currentMonth - i;
				if (month < 1)
				{
                    month = 12 - month;
                    year--;
				}

				var monthlyBackup = groupedBackups.FirstOrDefault(backup => backup.BackupDate.Month == month && backup.BackupDate.Year == year);
				if (monthlyBackup != null)
				{
					yield return monthlyBackup;
				}
                else
                {
                    var cutoffDate = new DateTime(currentYear, currentMonth, 1);
					var nextCandidateBackup = backups.OrderByDescending(b => b.BackupDate).FirstOrDefault(b => b.BackupDate <= cutoffDate);
					if (nextCandidateBackup != null)
					{
						yield return nextCandidateBackup;
					}

					yield break;
				}
			}
		}

        public static IEnumerable<IBackup> GetYearlyGenerations(this IEnumerable<IBackup> backups, int yearlyBackups)
        {
			if (yearlyBackups < 0)
			{
				ConsoleLogger.LogWarning($"Yearly Backup Num configured to {yearlyBackups}");
                yield break;
			}

			var currentYear = DateTimeHelper.Instance.Now.Year;

			var groupedBackups = backups.GroupBy(backup => backup.BackupDate.Year)
			  .Select(yearGrp => yearGrp.Max())
			  .OrderByDescending(backup => backup.BackupDate)
			  .Take(yearlyBackups)
			  .ToList();

			for (int i = 0; i < yearlyBackups; i++)
            {
                var yearlyBackup = groupedBackups.FirstOrDefault(b => b.BackupDate.Year == currentYear - i);
				if (yearlyBackup != null)
				{
					yield return yearlyBackup;
				}
				else
				{
					var cutoffDate = new DateTime(currentYear, 1, 1);
					var nextCandidateBackup = backups.OrderByDescending(b => b.BackupDate).FirstOrDefault(b => b.BackupDate <= cutoffDate);
					if (nextCandidateBackup != null)
					{
						yield return nextCandidateBackup;
					}

					yield break;
				}
			}
		}

		private static string GetAddonNameFromSlug(IEnumerable<Addon> addons, string slug)
		{
            ConsoleLogger.LogVerbose($"Looking for Addon name matching slug: {slug}. Checking agaisnt {addons.Count()} Addons in cache ");
			string name = addons.FirstOrDefault(addon => addon.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))?.Name;
			return name ?? string.Empty;
		}        
	}
}
