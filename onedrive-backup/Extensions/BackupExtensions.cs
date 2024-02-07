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
        public static BackupModel ToBackupModel(this Backup backup, HassContext hassContext, BackupAdditionalData backupAdditionalData)
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
                Folders = backup.Content?.Folders ?? Enumerable.Empty<string>(),
                RetainLocal = backupAdditionalData.IsRetainedLocally(backup.Slug),
                RetainOneDrive = backupAdditionalData.IsRetainedOneDrive(backup.Slug)
            };
        }

		public static BackupModel ToBackupModel(this OnedriveBackup onedriveBackup, HassContext hassContext, BackupAdditionalData backupAdditionalData)
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
                Folders = onedriveBackup.Folders ?? Enumerable.Empty<string>(),
                RetainLocal = backupAdditionalData.IsRetainedLocally(onedriveBackup.Slug),
                RetainOneDrive = backupAdditionalData.IsRetainedOneDrive(onedriveBackup.Slug)
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

		public static IList<IBackup> GetDailyGenerations(this IEnumerable<IBackup> backups, int dailyBackupNum, DateTime now)
		{
            if (dailyBackupNum < 0)
            {
                return Enumerable.Empty<IBackup>().ToList();
            }

			return backups.Where(backup => now - backup.BackupDate.Date < TimeSpan.FromDays(dailyBackupNum)).ToList();
		}

		public static IList<IBackup> GetWeeklyGenerations(this IEnumerable<IBackup> backups, int weeklyBackupNum, DayOfWeek firstDayOfWeek, DateTime now)
		{
			if (weeklyBackupNum < 0)
			{
                return Enumerable.Empty<IBackup>().ToList();
			}

            List<IBackup> ret = new();
            var currentWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now, CalendarWeekRule.FirstDay, firstDayOfWeek);
            var currentYear = now.Year;
            var groupedBackups = backups.GroupBy(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate.Date, CalendarWeekRule.FirstDay, firstDayOfWeek))
                   .Select(weekGroup => weekGroup.OrderByDescending(backup => backup.BackupDate).First())
                   .OrderByDescending(backup => backup.BackupDate)
                   .Take(weeklyBackupNum)
                   .ToList();

            for (int i=0; i < weeklyBackupNum; i++)
            {
				int backupYear = currentYear;
				var week = currentWeek - i;
                while (week < 1)
                {
                    backupYear--;
                    week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(new DateTime(backupYear, 12, 31), CalendarWeekRule.FirstDay, firstDayOfWeek);
				}

				var weeklyBackup = groupedBackups.FirstOrDefault(backup => backup.BackupDate.Year == backupYear && CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate.Date, CalendarWeekRule.FirstDay, firstDayOfWeek) == week);
				if (weeklyBackup != null)
                {
                    ret.Add(weeklyBackup);
				}
			}

            return ret.Distinct().ToList();
		}

        public static IList<IBackup> GetMonthlyGenerations(this IEnumerable<IBackup> backups, int monthlyBackupNum, DateTime now)
        {
			if (monthlyBackupNum < 0)
			{
                return Enumerable.Empty<IBackup>().ToList();
			}

			List<IBackup> ret = new();
			var currentMonth = now.Month;
            var currentYear = now.Year;
			var groupedBackups = backups.GroupBy(backup => backup.BackupDate.Month)
				  .Select(monthGrp => monthGrp.OrderByDescending(backup => backup.BackupDate).First())
				  .OrderByDescending(backup => backup.BackupDate)
				  .Take(monthlyBackupNum)
				  .ToList();

			for (int i = 0; i < monthlyBackupNum; i++)
			{
				var year = currentYear;
				var month = currentMonth - i;
                while (month < 1)
                {
					month = 12 - Math.Abs(month);
					year--;
				}

				var monthlyBackup = groupedBackups.FirstOrDefault(backup => backup.BackupDate.Month == month && backup.BackupDate.Year == year);
				if (monthlyBackup != null)
				{
                    ret.Add(monthlyBackup);
				}
			}

            return ret.Distinct().ToList();
		}

        public static IList<IBackup> GetYearlyGenerations(this IEnumerable<IBackup> backups, int yearlyBackups , DateTime now)
        {
			if (yearlyBackups < 0)
			{
                return Enumerable.Empty<IBackup>().ToList();
			}

			List<IBackup> ret = new();
			var currentYear = now.Year;

			var groupedBackups = backups.GroupBy(backup => backup.BackupDate.Year)
			  .Select(yearGrp => yearGrp.OrderByDescending(backup => backup.BackupDate).First())
			  .OrderByDescending(backup => backup.BackupDate)
			  .Take(yearlyBackups)
			  .ToList();

			for (int i = 0; i < yearlyBackups; i++)
            {
                var yearlyBackup = groupedBackups.FirstOrDefault(b => b.BackupDate.Year == currentYear - i);
				if (yearlyBackup != null)
				{
                    ret.Add(yearlyBackup);
				}
			}

            return ret.Distinct().ToList();
		}

        public static bool IsRetainedLocally(this IBackup backup, BackupAdditionalData additionalData) 
        {
            return additionalData.IsRetainedLocally(backup.Slug);
        }

        public static bool IsRetainedOneDrive(this IBackup backup, BackupAdditionalData additionalData)
        {
            return additionalData.IsRetainedOneDrive(backup.Slug);
        }

        private static string GetAddonNameFromSlug(IEnumerable<Addon> addons, string slug)
		{
			string name = addons.FirstOrDefault(addon => addon.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))?.Name;
			return name ?? string.Empty;
		}        
	}
}
