using hassio_onedrive_backup;
using hassio_onedrive_backup.Contracts;
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

		public static List<IBackup> GetDailyGenerations(this IEnumerable<IBackup> backups, int dailyBackupNum)
		{
			var now = DateTimeHelper.Instance.Now.Date;
			return backups.Where(backup => now - backup.BackupDate.Date < TimeSpan.FromDays(dailyBackupNum)).ToList();
		}

		public static List<IBackup> GetWeeklyGenerations(this IEnumerable<IBackup> backups, int weeklyBackupNum, DayOfWeek firstDayOfWeek)
		{
            var now = DateTimeHelper.Instance.Now.Date;
            var currentWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now, CalendarWeekRule.FirstDay, firstDayOfWeek);
             backups.GroupBy(backup => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(backup.BackupDate.Date, CalendarWeekRule.FirstDay, firstDayOfWeek))
                   .Select(weekGroup => weekGroup.Max())
                   .OrderByDescending(backup => backup.BackupDate)
                   .Take(weeklyBackupNum)
                   .ToList();
                   
		}

		private static string GetAddonNameFromSlug(IEnumerable<Addon> addons, string slug)
		{
            ConsoleLogger.LogVerbose($"Looking for Addon name matching slug: {slug}. Checking agaisnt {addons.Count()} Addons in cache ");
			string name = addons.FirstOrDefault(addon => addon.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))?.Name;
			return name ?? string.Empty;
		}        
	}
}
