using hassio_onedrive_backup.Contracts;
using onedrive_backup.Models;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace onedrive_backup.Extensions
{
    public static class BackupExtensions
    {
        public static BackupModel ToBackupModel(this Backup backup)
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
                Addons = backup.Content.Addons,
                Folders = backup.Content.Folders
            };
        }

        public static BackupModel ToBackupModel(this OnedriveBackup onedriveBackup)
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
                Addons = onedriveBackup.Addons,
                Folders = onedriveBackup.Folders
            };
        } 
    }
}
