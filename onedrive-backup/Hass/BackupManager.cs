using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using Microsoft.Graph;
using Newtonsoft.Json;
using System.Collections;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class BackupManager
    {
        private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private IHassioClient _hassIoClient;
        private readonly HassOnedriveEntityState _hassEntityState;
        private BitArray _allowedHours;

        public BackupManager(AddonOptions addonOptions, IGraphHelper graphHelper, IHassioClient hassIoClient, BitArray allowedHours)
        {
            _addonOptions = addonOptions;
            _graphHelper = graphHelper;
            _hassIoClient = hassIoClient;
            _hassEntityState = HassOnedriveEntityState.Initialize(hassIoClient);
            _allowedHours = allowedHours;
        }

        public async Task PerformBackupsAsync()
        {
            const int InstanceNameMaxLength = 20;
            await UpdateHassEntity();
            var now = DateTimeHelper.Instance!.Now;

            // Get existing local backups
            ConsoleLogger.LogInfo("Retrieving existing local backups...");
            var localBackups = await _hassIoClient.GetBackupsAsync(IsOwnedBackup);

            // Get existing online backups
            ConsoleLogger.LogInfo("Retrieving existing online backups...");
            var onlineBackups = await GetOnlineBackupsAsync(_addonOptions.InstanceName);

            DateTime lastLocalBackupTime = localBackups.Any() ? localBackups.Max(backup => backup.Date) : DateTime.MinValue;
            ConsoleLogger.LogInfo($"Last local backup Date: {(lastLocalBackupTime == DateTime.MinValue ? "None" : lastLocalBackupTime)}");

            DateTime lastOnlineBackupTime = onlineBackups.Any() ? onlineBackups.Max(backup => backup.BackupDate) : DateTime.MinValue;
            ConsoleLogger.LogInfo($"Last online backup Date: {(lastOnlineBackupTime== DateTime.MinValue ? "None" : lastOnlineBackupTime)}");

            // Create local backups if needed
            if ((now - lastLocalBackupTime).TotalHours >= _addonOptions.BackupIntervalHours && (now - lastOnlineBackupTime).TotalHours >= _addonOptions.BackupIntervalHours)
            {
                if (_allowedHours[now.Hour] == false)
                {
                    ConsoleLogger.LogWarning("Not performing backup outside allowed times");
                }
                else
                {
                    List<string>? addons = null;
                    List<string>? folders = null;

                    ConsoleLogger.LogInfo($"Creating new backup");
                    if (_addonOptions.IsPartialBackup)
                    {
                        addons = await _hassIoClient.GetAddonsAsync();
                        folders = _addonOptions.IncludedFolderList;
                    }

                    bool backupCreated = await _hassIoClient.CreateBackupAsync(
                        _addonOptions.BackupNameSafe,
                        compressed: true,
                        password: String.IsNullOrEmpty(_addonOptions.BackupPassword) ? null : _addonOptions.BackupPassword,
                        addons: addons,
                        folders: folders);

                    if (backupCreated == false)
                    {
                        await _hassIoClient.PublishEventAsync(Events.OneDriveEvents.BackupCreateFailed);
                        if (_addonOptions.NotifyOnError)
                        {
                            await _hassIoClient.SendPersistentNotificationAsync("Failed creating local backup. Check Addon logs for more details");
                        }
                    }

                    //Refresh local backup list
                    if (backupCreated)
                    {
                        localBackups = await _hassIoClient.GetBackupsAsync(IsOwnedBackup);
                    }
                }
            }

            // Get Online backup candidates
            var onlineBackupCandiates = await GetOnlineBackupCandidatesAsync(localBackups);

            // Get Online Backups Candidates that have not yet been uploaded
            var backupsToUpload = onlineBackupCandiates
                .Where(backup => onlineBackups.Any(onlineBackup => onlineBackup.Slug.Equals(backup.Slug, StringComparison.OrdinalIgnoreCase)) == false)
                .Take(_addonOptions.MaxOnedriveBackups)                
                .ToList();

            // Upload backups
            if (backupsToUpload.Any())
            {
                ConsoleLogger.LogInfo($"Found {backupsToUpload.Count()} backups to upload.");
                
                // Set Home Assistant Entity state to Syncing
                _hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
                await _hassEntityState.UpdateBackupEntityInHass();
                
                foreach (var backup in backupsToUpload)
                {
                    ConsoleLogger.LogInfo($"Uploading {backup.Name} ({backup.Date})");                    
                    string? instanceSuffix = _addonOptions.InstanceName == null ? null : $".{_addonOptions.InstanceName.Substring(0, Math.Min(InstanceNameMaxLength, _addonOptions.InstanceName.Length))}";
                    string destinationFileName = $"{backup.Name}{instanceSuffix}.tar";
                    string tempBackupFilePath = await _hassIoClient.DownloadBackupAsync(backup.Slug);
                    var uploadSuccessful = await _graphHelper.UploadFileAsync(tempBackupFilePath, backup.Date, _addonOptions.InstanceName, destinationFileName,
                        async (prog) =>
                        {
                            _hassEntityState.UploadPercentage = prog;
                            await _hassEntityState.UpdateBackupEntityInHass();

                        });
                    if (uploadSuccessful == false)
                    {
                        await _hassIoClient.PublishEventAsync(Events.OneDriveEvents.BackupUploadFailed);
                        if (_addonOptions.NotifyOnError)
                        {
                            await _hassIoClient.SendPersistentNotificationAsync("Failed uploading backup to onedrive. Check Addon logs for more details");
                        }
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
            onlineBackups = await GetOnlineBackupsAsync(_addonOptions.InstanceName);
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
                    if (deleteSuccessfull == false)
                    {
                        await _hassIoClient.PublishEventAsync(Events.OneDriveEvents.OneDriveBackupDeleteFailed);
                        if (_addonOptions.NotifyOnError)
                        {
                            await _hassIoClient.SendPersistentNotificationAsync("Failed deleting old backup from OneDrive. Check Addon logs for more details");
                        }
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
                    if (deleteSuccess == false)
                    {
                        await _hassIoClient.PublishEventAsync(Events.OneDriveEvents.LocalBackupDeleteFailed);
                        if (_addonOptions.NotifyOnError)
                        {
                            await _hassIoClient.SendPersistentNotificationAsync("Error Deleting Local Backup. Check Addon logs for more details");
                        }
                    }
                }
            }

            await UpdateHassEntity();
        }

        public async Task DownloadCloudBackupsAsync()
        {
            _hassEntityState.State = HassOnedriveEntityState.BackupState.RecoveryMode;
            await _hassEntityState.UpdateBackupEntityInHass();
            var onlineBackups = await GetOnlineBackupsAsync("*");
            var onlineInstanceBackups = onlineBackups.Where(backup => string.Equals(backup.InstanceName, _addonOptions.InstanceName, StringComparison.OrdinalIgnoreCase)).ToList();
            var localBackups = await _hassIoClient.GetBackupsAsync(IsOwnedBackup);

            if (onlineInstanceBackups.Count > 0)
            {
                ConsoleLogger.LogInfo($"Found {onlineInstanceBackups.Count} matching backups in OneDrive");
            }
            else if (onlineBackups.Count > 0)
            {
                var instanceNames = onlineBackups.Select(backup => backup.InstanceName ?? "*NoInstance*").Distinct();                    
                ConsoleLogger.LogInfo($"Found backups belonging to other instances: {string.Join(',', instanceNames)}. If you would like to use another instance backup please update the addon configuration and set the appropriate instance name");
                return;
            }
            else
            {
                ConsoleLogger.LogWarning($"No backups found in OneDrive");
                return;
            }

            var localBackupNum = localBackups.Count;
            int numberOfBackupsToDownload = Math.Max(0, _addonOptions.MaxLocalBackups - localBackupNum);
            if (numberOfBackupsToDownload == 0)
            {
                ConsoleLogger.LogWarning(
                    $"Local backups at maximum configured number ({_addonOptions.MaxLocalBackups}). To sync additional backups from OneDrive either delete some local backups or increase the configured maximum");
                return;
            }

            var backupsToDownload = onlineInstanceBackups
                .OrderByDescending(backup => backup.BackupDate)
                .Where(backup => localBackups.Any(local => local.Slug.Equals(backup.Slug, StringComparison.OrdinalIgnoreCase)) == false)
                .Take(numberOfBackupsToDownload)
                .ToList();

            if (backupsToDownload.Count == 0)
            {
                ConsoleLogger.LogInfo($"All {Math.Min(numberOfBackupsToDownload, onlineInstanceBackups.Count)} latest backups already exist locally");
            }

            foreach (var onlineBackup in backupsToDownload)
            {
                try
                {
                    ConsoleLogger.LogInfo($"Downloading backup {onlineBackup.FileName}");
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
                    ConsoleLogger.LogInfo($"Loading backup {onlineBackup.FileName} to Home Assisant");
                    await _hassIoClient.UploadBackupAsync(backupFile);
                    System.IO.File.Delete(backupFile);
                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Error fetching backup {onlineBackup.FileName} from Onedrive to Home Assistant. {ex}");
                }
            }
        }

        private async Task UpdateHassEntity()
        {
            var now = DateTimeHelper.Instance!.Now;
            var localBackups = await _hassIoClient.GetBackupsAsync(IsOwnedBackup);
            var onlineBackups = await GetOnlineBackupsAsync(_addonOptions.InstanceName);
            _hassEntityState.BackupsInHomeAssistant = localBackups.Count;
            _hassEntityState.BackupsInOnedrive = onlineBackups.Count;
            _hassEntityState.LastLocalBackupDate = localBackups.Any() ? localBackups.Max(backup => backup.Date) : null;
            _hassEntityState.LastOnedriveBackupDate = onlineBackups.Any() ? onlineBackups.Max(backup => backup.BackupDate) : null;

            bool onedriveSynced = false;
            bool localSynced = false;

            if (_hassEntityState.LastOnedriveBackupDate != null && (now - _hassEntityState.LastOnedriveBackupDate.Value).TotalHours <= _addonOptions.BackupIntervalHours)
            {
                onedriveSynced = true;
            }

            if (_hassEntityState.LastLocalBackupDate != null && (now - _hassEntityState.LastLocalBackupDate.Value).TotalHours <= _addonOptions.BackupIntervalHours)
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

        private async Task<List<OnedriveBackup>> GetOnlineBackupsAsync(string? instanceName)
        {
            var onlineBackups = (await _graphHelper.GetItemsInAppFolderAsync()).Select(CheckIfFileIsBackup).ToList();
            onlineBackups.RemoveAll(item => item == null);
            if (instanceName != "*")
            {
                onlineBackups = onlineBackups.Where(item => string.Equals(instanceName, item.InstanceName, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            return onlineBackups;
        }

        private OnedriveBackup? CheckIfFileIsBackup(DriveItem item)
        {
            OnedriveBackup ret = null;
            if (item.Folder != null && item.Name.Equals("FileSync"))
            {
                return ret;
            }

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

        private Task<List<Backup>> GetOnlineBackupCandidatesAsync(IEnumerable<Backup> localBackups)
        {
            var filteredLocalBackups = localBackups
                .Where(backup => backup.Compressed)
                .OrderByDescending(backup => backup.Date);

            return Task.FromResult(filteredLocalBackups.ToList());
        }

        private bool IsOwnedBackup(Backup backup)
        {
            return backup.Name.StartsWith(_addonOptions.BackupNameSafe, StringComparison.OrdinalIgnoreCase);
        }
    }
}
