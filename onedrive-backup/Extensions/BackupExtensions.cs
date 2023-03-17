using hassio_onedrive_backup.Contracts;
using onedrive_backup.Hass;
using onedrive_backup.Models;
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
                Addons = onedriveBackup.Addons?.Select(slug => new Addon { Slug = slug }) ?? Enumerable.Empty<Addon>(),
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

		private static string GetAddonNameFromSlug(IEnumerable<Addon> addons, string slug)
		{
			string name = addons.FirstOrDefault(addon => addon.Slug.Equals(slug))?.Name;
			return name ?? string.Empty;
		}
	}
}
