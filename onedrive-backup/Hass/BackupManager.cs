﻿using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using Microsoft.Graph;
using Newtonsoft.Json;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class BackupManager
    {
        private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private IHassioClient _hassIoClient;
        private readonly HassOnedriveEntityState _hassEntityState;

        public BackupManager(AddonOptions addonOptions, IGraphHelper graphHelper, IHassioClient hassIoClient)
        {
            _addonOptions = addonOptions;
            _graphHelper = graphHelper;
            _hassIoClient = hassIoClient;
            _hassEntityState = HassOnedriveEntityState.Initialize(hassIoClient);
        }

        public async Task PerformBackupsAsync()
        {
            await UpdateHassEntity();
            var now = DateTime.Now;

            // Set Home Assistant Entity state to Syncing
            _hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
            await _hassEntityState.UpdateBackupEntityInHass();

            // Get existing local backups
            var localBackups = await _hassIoClient.GetBackupsAsync(backup => backup.Name.Equals(_addonOptions.BackupNameSafe, StringComparison.OrdinalIgnoreCase));

            // Get existing online backups
            ConsoleLogger.LogInfo("Retrieving existing online backups...");
            var onlineBackups = await GetOnlineBackupsAsync();

            // Create local backups if needed
            DateTime lastLocalBackupTime = localBackups.Any() ? localBackups.Max(backup => backup.Date) : DateTime.MinValue;
            if ((now - lastLocalBackupTime).TotalHours >= _addonOptions.BackupIntervalHours)
            {
                ConsoleLogger.LogInfo($"Creating new backup");
                bool backupCreated = await _hassIoClient.CreateBackupAsync(
                    _addonOptions.BackupNameSafe,
                    true,
                    String.IsNullOrEmpty(_addonOptions.BackupPassword) ? null : _addonOptions.BackupPassword);

                if (backupCreated == false && _addonOptions.NotifyOnError)
                {
                    await _hassIoClient.SendPersistentNotificationAsync("Failed creating local backup. Check Addon logs for more details");
                }

                //Refresh local backup list
                if (backupCreated)
                {
                    localBackups = await _hassIoClient.GetBackupsAsync(backup => backup.Name.Equals(_addonOptions.BackupNameSafe, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Get Online backup candidates
            var onlineBackupCandiates = await GetOnlineBackupCandidatesAsync(localBackups, onlineBackups);

            // Get Online Backups Candidates that have not yet been uploaded
            var backupsToUpload = onlineBackupCandiates
                .Where(backup => onlineBackups.Any(onlineBackup => onlineBackup.Slug.Equals(backup.Slug, StringComparison.OrdinalIgnoreCase)) == false)
                .ToList();

            // Upload backups
            if (backupsToUpload.Any())
            {
                ConsoleLogger.LogInfo($"Found {backupsToUpload.Count()} backups to upload.");
                foreach (var backup in backupsToUpload)
                {
                    ConsoleLogger.LogInfo($"Uploading {backup.Name} ({backup.Date})");
                    string destinationFileName = $"{_addonOptions.BackupNameSafe}_{backup.Date.ToString("yyyy-MM-dd-HH-mm")}.tar";
                    string tempBackupFilePath = await _hassIoClient.DownloadBackup(backup.Slug);
                    var uploadSuccessful = await _graphHelper.UploadFileAsync(tempBackupFilePath, backup.Date, destinationFileName,
                        async (prog) =>
                        {
                            _hassEntityState.UploadPercentage = prog;
                            await _hassEntityState.UpdateBackupEntityInHass();

                        });
                    if (uploadSuccessful == false && _addonOptions.NotifyOnError)
                    {
                        await _hassIoClient.SendPersistentNotificationAsync("Failed uploading backup to onedrive. Check Addon logs for more details");
                    }

                    // Delete temporary backup file
                    System.IO.File.Delete(tempBackupFilePath);
                }
            }
            else
            {
                ConsoleLogger.LogInfo("Online backups synced. No upload required");
            }

            // Refresh Online Backups
            onlineBackups = await GetOnlineBackupsAsync();
            int numOfOnlineBackups = onlineBackups.Count;
            int numOfOnlineBackupsToDelete = Math.Max(0, onlineBackups.Count - _addonOptions.MaxOnedriveBackups);

            // Delete Old Online Backups
            var backupsToDelete = onlineBackups
                .OrderBy(onlineBackup => onlineBackup.BackupDate)
                .Take(numOfOnlineBackupsToDelete)
                .ToList();

            if (backupsToDelete.Any())
            {
                ConsoleLogger.LogInfo($"Found {backupsToDelete.Count()} backups to delete from OneDrive.");
                foreach (var backupToDelete in backupsToDelete)
                {
                    bool deleteSuccessfull = await _graphHelper.DeleteFileFromAppFolderAsync(backupToDelete.FileName);
                    if (deleteSuccessfull == false && _addonOptions.NotifyOnError)
                    {
                        await _hassIoClient.SendPersistentNotificationAsync("Failed deleting old backup from OneDrive. Check Addon logs for more details");
                    }
                }
            }

            // Delete Old Local Backups
            if (localBackups.Count > _addonOptions.MaxLocalBackups)
            {
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

            await UpdateHassEntity();
        }

        public async Task DownloadCloudBackupsAsync()
        {
            _hassEntityState.State = HassOnedriveEntityState.BackupState.RecoveryMode;
            await _hassEntityState.UpdateBackupEntityInHass();
            var onlineBackups = await GetOnlineBackupsAsync();
            var localBackupNum = (await _hassIoClient.GetBackupsAsync(backup => backup.Name.Equals(_addonOptions.BackupNameSafe, StringComparison.OrdinalIgnoreCase))).Count();
            int numberOfBackupsToDownload = Math.Max(0, _addonOptions.MaxLocalBackups - localBackupNum);
            if (numberOfBackupsToDownload == 0)
            {
                ConsoleLogger.LogWarning(
                    $"Local backups at maximum configured number ({_addonOptions.MaxLocalBackups}). To sync additional backups from OneDrive either delete some local backups or increase the configured maximum");
            }

            var backupsToDownload = onlineBackups
                .OrderByDescending(backup => backup.BackupDate)
                .Take(numberOfBackupsToDownload)
                .ToList();

            foreach (var onlineBackup in backupsToDownload)
            {
                string? backupFile = await _graphHelper.DownloadFileAsync(onlineBackup.FileName, async (prog) =>
                {
                    _hassEntityState.DownloadPercentage = prog;
                    await _hassEntityState.UpdateBackupEntityInHass();
                });

                if (backupFile == null)
                {
                    ConsoleLogger.LogError($"Error downloading backup {onlineBackup.FileName}");
                    continue;
                }

                // Upload backup to Home Assistant

            }
        }

        private async Task UpdateHassEntity()
        {
            var localBackups = await _hassIoClient.GetBackupsAsync(backup => backup.Name.Equals(_addonOptions.BackupNameSafe, StringComparison.OrdinalIgnoreCase));
            var onlineBackups = await GetOnlineBackupsAsync();
            _hassEntityState.BackupsInHomeAssistant = localBackups.Count;
            _hassEntityState.BackupsInOnedrive = onlineBackups.Count;
            _hassEntityState.LastLocalBackupDate = localBackups.Any() ? localBackups.Max(backup => backup.Date) : null;
            _hassEntityState.LastOnedriveBackupDate = onlineBackups.Any() ? onlineBackups.Max(backup => backup.BackupDate) : null;

            bool onedriveSynced = false;
            bool localSynced = false;

            if (_hassEntityState.LastOnedriveBackupDate != null && (DateTime.Now - _hassEntityState.LastOnedriveBackupDate.Value).TotalHours <= _addonOptions.BackupIntervalHours)
            {
                onedriveSynced = true;
            }

            if (_hassEntityState.LastLocalBackupDate != null && (DateTime.Now - _hassEntityState.LastLocalBackupDate.Value).TotalHours <= _addonOptions.BackupIntervalHours)
            {
                localSynced = true;
            }

            HassOnedriveEntityState.BackupState state = HassOnedriveEntityState.BackupState.Unknown;
            if (_addonOptions.RecoveryMode)
            {
                state = HassOnedriveEntityState.BackupState.RecoveryMode;
            }
            else if (onedriveSynced && localSynced)
            {
                state = HassOnedriveEntityState.BackupState.Backed_Up;
            }
            else if (onedriveSynced)
            {
                state = HassOnedriveEntityState.BackupState.Backed_Up_Onedrive;
            }
            else if (localSynced)
            {
                state = HassOnedriveEntityState.BackupState.Backed_Up_Local;
            }
            else
            {
                state = HassOnedriveEntityState.BackupState.Stale;
            }

            _hassEntityState.State = state;
            await _hassEntityState.UpdateBackupEntityInHass();
        }

        private async Task<List<OnedriveBackup>> GetOnlineBackupsAsync()
        {
            var onlineBackups = (await _graphHelper.GetItemsInAppFolderAsync()).Select(CheckIfFileIsBackup).ToList();
            onlineBackups.RemoveAll(item => item == null);
            return onlineBackups;
        }

        private OnedriveBackup? CheckIfFileIsBackup(DriveItem item)
        {
            OnedriveBackup ret = null;
            try
            {
                ret = new OnedriveBackup(item.Name, JsonConvert.DeserializeObject<OnedriveItemDescription>(item.Description)!);
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogWarning($"Unrecognized file found in backup folder : {item.Name}");
            }

            return ret;
        }

        private Task<List<Backup>> GetOnlineBackupCandidatesAsync(IEnumerable<Backup> localBackups, IEnumerable<OnedriveBackup> onlineBackups)
        {
            var filteredLocalBackups = localBackups
                .Where(backup => backup.Compressed)
                .OrderByDescending(backup => backup.Date);

            return Task.FromResult(filteredLocalBackups.ToList());
        }
    }
}
