using hassio_onedrive_backup.Contracts;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class BackupManager
    {
        private const string BackupFolder = "/backup";
        private AddonOptions _addonOptions;

        public BackupManager(AddonOptions addonOptions)
        {
            _addonOptions = addonOptions;
        }

        public static string GetBackupFilePath(Backup backup)
        {
            return $"{BackupFolder}/{backup.Slug}.tar";
        }

        public 
    }
}
