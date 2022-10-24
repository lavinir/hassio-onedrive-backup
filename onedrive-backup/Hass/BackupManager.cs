using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class BackupManager
    {
        private const string BackupFolder = "/backup";
        private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private IHassioClient _hassIoClient;

        public BackupManager(AddonOptions addonOptions, IGraphHelper graphHelper, IHassioClient hassIoClient)
        {
            _addonOptions = addonOptions;
            _graphHelper = graphHelper;
            _hassIoClient = hassIoClient;
        }

        public async Task PerformBackupsAsync()
        {
            var now = DateTime.Now;

            // Get existing local backups
            var localBackups = await _hassIoClient.GetBackupsAsync(backup => backup.Name.Equals(_addonOptions.BackupName, StringComparison.OrdinalIgnoreCase));

            // Create local backups if needed
            DateTime lastBackupTime = localBackups.Any() ? localBackups.Max(backup => backup.Date) : DateTime.MinValue;
            if ((now - lastBackupTime).TotalHours >= _addonOptions.BackupIntervalHours)
            {
                ConsoleLogger.LogInfo($"Creating new Backup");
                bool backupCreated = await _hassIoClient.CreateBackupAsync(
                    string.IsNullOrWhiteSpace(_addonOptions.BackupName) ? "hass_backup" : _addonOptions.BackupName, 
                    true, 
                    String.IsNullOrEmpty(_addonOptions.BackupPassword) ? null : _addonOptions.BackupPassword);

                if (backupCreated == false && _addonOptions.NotifyOnError)
                {
                    await _hassIoClient.SendPersistentNotificationAsync("Failed creating local backup. Check Addon logs for more details");
                }
            }

            // Get existing online backups
            var onlineBackups = await GetOnlineBackupsAsync();

            // Get Online backup candidates
            var onlineBackupCandiates = await GetOnlineBackupCandidatesAsync(localBackups, onlineBackups);

            // Get Online Backups Candidates that have not yet been uploaded
            var backupsToUpload = onlineBackupCandiates
                .Where(backup => onlineBackups.Any(onlineBackup => onlineBackup.Slug.Equals(backup.Slug, StringComparison.OrdinalIgnoreCase) == false))
                .ToList();

            // Upload backups
            if (backupsToUpload.Any())
            {
                ConsoleLogger.LogInfo($"Found {backupsToUpload.Count()} backups to upload.");
                foreach (var backup in backupsToUpload)
                {
                    ConsoleLogger.LogInfo($"Uploading {backup.Name}");
                    string destinationFileName = $"{_addonOptions.BackupName}_{backup.Date.ToString("yyyy-MM-dd-HH-mm")}.tar";                    
                    var uploadSuccessful = await _graphHelper.UploadFileAsync(GetBackupFilePath(backup), destinationFileName);
                    if (uploadSuccessful == false && _addonOptions.NotifyOnError)
                    {
                        await _hassIoClient.SendPersistentNotificationAsync("Failed uploading backup to onedrive. Check Addon logs for more details");
                    }
                }
            }

            // Delete Old Online Backups
            var backupsToDelete = onlineBackups
                .Where(onlineBackup => onlineBackupCandiates.Any(backupCandidate => backupCandidate.Slug.Equals(onlineBackup.Slug, StringComparison.OrdinalIgnoreCase)) == false)
                .ToList();

            if (backupsToDelete.Any())
            {
                ConsoleLogger.LogInfo($"Found {backupsToDelete.Count()} backups to delete.");
                foreach (var backupToDelete in backupsToDelete)
                {
                    bool deleteSuccessfull = await _graphHelper.DeleteFileFromAppFolderAsync(backupToDelete.FileName);
                    if (deleteSuccessfull == false && _addonOptions.NotifyOnError)
                    {
                        await _hassIoClient.SendPersistentNotificationAsync("Failed deleting old backup from Onedrive. Check Addon logs for more details");
                    }
                }
            }

            // Delete Old Local Backups
            if (localBackups.Count <= _addonOptions.MaxLocalBackups)
            {
                return;
            }

            int numOfLocalBackupsToRemove = localBackups.Count - _addonOptions.MaxLocalBackups;
            var localBackupsToRemove = localBackups
                .OrderBy(backup => backup.Date)
                .Take(numOfLocalBackupsToRemove)
                .ToList();

            ConsoleLogger.LogInfo($"Removing {numOfLocalBackupsToRemove} local backups");
            foreach (var localBackup in localBackupsToRemove)
            {
                bool deleteSuccess = await _hassIoClient.DeleteBackupAsync(localBackup);
                if (deleteSuccess == false && _addonOptions.NotifyOnError)
                {
                    await _hassIoClient.SendPersistentNotificationAsync("Error Deleting Local Backup. Check Addon logs for more details");
                }
            }
        }

        private static string GetBackupFilePath(Backup backup)
        {
            return $"{BackupFolder}/{backup.Slug}.tar";
        }

        private async Task<List<OnedriveBackup>> GetOnlineBackupsAsync()
        {
            var onlineBackups = (await _graphHelper.GetItemsInAppFolderAsync()).Select(item => new OnedriveBackup(item.Description, item.Name)).ToList();
            return onlineBackups;
        }

        private Task<List<Backup>> GetOnlineBackupCandidatesAsync(IEnumerable<Backup> localBackups, IEnumerable<OnedriveBackup> onlineBackups)
        {
            var filteredLocalBackups = localBackups
                .Where(backup => backup.Compressed)
                .OrderByDescending(backup => backup.Date);

            return Task.FromResult(filteredLocalBackups.Take(_addonOptions.MaxOnedriveBackups).ToList());
        }
    }
}
