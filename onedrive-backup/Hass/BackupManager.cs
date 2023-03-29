using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using Microsoft.Graph;
using Newtonsoft.Json;
using onedrive_backup.Graph;
using onedrive_backup.Hass;
using System.Collections;
using static hassio_onedrive_backup.Contracts.HassAddonsResponse;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    public class BackupManager 
    {
		private const int InstanceNameMaxLength = 20;
		private readonly HassOnedriveEntityState _hassEntityState;
        private readonly TransferSpeedHelper? _transferSpeedHelper;
        private readonly HassContext _hassContext;
		private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private IHassioClient _hassIoClient;
		private BitArray _allowedHours;
        private bool _isExecuting = false;

        public List<Backup> LocalBackups { get; private set; }
        public List<OnedriveBackup> OnlineBackups { get; private set; }

        public BackupManager(IServiceProvider serviceProvider, BitArray allowedHours, TransferSpeedHelper? transferSpeedHelper)
        {
            _addonOptions = serviceProvider.GetService<AddonOptions>();
            _graphHelper = serviceProvider.GetService<IGraphHelper>();
            _hassIoClient = serviceProvider.GetService<IHassioClient>();
            _hassEntityState = serviceProvider.GetService<HassOnedriveEntityState>();
            _transferSpeedHelper = transferSpeedHelper;
            _hassContext = serviceProvider.GetService<HassContext>();
            _allowedHours = allowedHours;
        }

        public event Action? LocalBackupsUpdated;

        public event Action? OneDriveBackupsUpdated;

        public async Task PerformBackupsAsync()
        {
            if (_isExecuting)
            {
                ConsoleLogger.LogWarning("Previous backup iteration still executing. Skipping...");
                return;
            }
            
            await UpdateHassEntity();
            var now = DateTimeHelper.Instance!.Now;

            // Get existing local backups
            ConsoleLogger.LogVerbose("Retrieving existing local backups...");
            var localBackups = await GetLocalBackups();

            // Get existing online backups
            ConsoleLogger.LogVerbose("Retrieving existing online backups...");
            var onlineBackups = await GetOnlineBackupsAsync(_addonOptions.InstanceName);

            DateTime lastLocalBackupTime = localBackups.Any() ? localBackups.Max(backup => backup.Date) : DateTime.MinValue;
            ConsoleLogger.LogVerbose($"Last local backup Date: {(lastLocalBackupTime == DateTime.MinValue ? "None" : lastLocalBackupTime)}");

            DateTime lastOnlineBackupTime = onlineBackups.Any() ? onlineBackups.Max(backup => backup.BackupDate) : DateTime.MinValue;
            ConsoleLogger.LogVerbose($"Last online backup Date: {(lastOnlineBackupTime== DateTime.MinValue ? "None" : lastOnlineBackupTime)}");

            // Create local backups if needed
            if ((now - lastLocalBackupTime).TotalHours >= _addonOptions.BackupIntervalHours && (now - lastOnlineBackupTime).TotalHours >= _addonOptions.BackupIntervalHours)
            {
                if (_allowedHours[now.Hour] == false)
                {
                    ConsoleLogger.LogWarning("Not performing backup outside allowed times");
                }
                else
				{
					_hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
					await _hassEntityState.UpdateBackupEntityInHass();
					await CreateLocalBackup();
				}
			}

            // Get Online backup candidates
            var onlineBackupCandiates = await GetOnlineBackupCandidatesAsync(localBackups);

            var uploadCandidates = onlineBackupCandiates
                .Select(bc => new { Slug = bc.Slug, Date = bc.Date }).Union(onlineBackups.Select(ob => new { Slug = ob.Slug, Date = ob.BackupDate }))
                .OrderByDescending(backup => backup.Date)
                .Take(_addonOptions.MaxOnedriveBackups).ToList();

            // Get Online Backups Candidates that have not yet been uploaded
            var backupsToUpload = new List<Backup>();
            foreach (var backupId in uploadCandidates)
            {
                if (onlineBackups.Any(ob => ob.Slug == backupId.Slug))
                {
                    continue;
                }

                backupsToUpload.Add(onlineBackupCandiates.Single(bc => bc.Slug == backupId.Slug));
            }
                
            // Upload backups
            if (backupsToUpload.Any())
            {
                ConsoleLogger.LogInfo($"Found {backupsToUpload.Count()} backups to upload.");
                
                // Set Home Assistant Entity state to Syncing
                _hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
                await _hassEntityState.UpdateBackupEntityInHass();
                
                foreach (var backup in backupsToUpload)
                {
                    await UploadLocalBackupToOneDrive(backup);
                }
            }
            else
            {
                ConsoleLogger.LogVerbose("Online backups synced. No upload required");
            }

            // Refresh Online Backups
            onlineBackups = await GetOnlineBackupsAsync(_addonOptions.InstanceName);
            int numOfOnlineBackupsToDelete = Math.Max(0, onlineBackups.Count - _addonOptions.MaxOnedriveBackups);

            // Delete Old Online Backups
            var backupsToDelete = onlineBackups
                .OrderBy(onlineBackup => onlineBackup.BackupDate)
                .Take(numOfOnlineBackupsToDelete)
                .ToList();

            if (backupsToDelete.Any())
            {
				_hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
				await _hassEntityState.UpdateBackupEntityInHass();

				ConsoleLogger.LogInfo($"Found {backupsToDelete.Count()} backups to delete from OneDrive.");
                foreach (var backupToDelete in backupsToDelete)
                {
                    bool deleteSuccessfull = await _graphHelper.DeleteItemFromAppFolderAsync(backupToDelete.FileName);
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
				_hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
				await _hassEntityState.UpdateBackupEntityInHass();

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

		public async Task<bool> CreateLocalBackup()
		{
			List<string>? addons = null;
			List<string>? folders = null;

			ConsoleLogger.LogInfo($"Creating new backup");
			if (_addonOptions.IsPartialBackup)
			{
                addons = _hassContext.Addons
                    .Where(addon => _addonOptions.ExcludedAddons.Any(excludedAddon => excludedAddon.Equals(addon.Slug, StringComparison.OrdinalIgnoreCase)) == false)
                    .Select(addon => addon.Slug)
                    .ToList();
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
            else
            {
				LocalBackups = await GetLocalBackups();
			}

			return backupCreated;		
		}

		public async Task<bool> UploadLocalBackupToOneDrive(Backup backup, Action<int?, int?>? progressCallback = null,  bool updateHassEntityState = true)
        {
            string? tempBackupFilePath = null;
            try
            {
                ConsoleLogger.LogInfo($"Uploading {backup.Name} ({backup.Date})");
                string? instanceSuffix = _addonOptions.InstanceName == null ? null : $".{_addonOptions.InstanceName.Substring(0, Math.Min(InstanceNameMaxLength, _addonOptions.InstanceName.Length))}";
                string destinationFileName = $"{backup.Name}{instanceSuffix}.tar";
                tempBackupFilePath = await _hassIoClient.DownloadBackupAsync(backup.Slug);
                var uploadSuccessful = await _graphHelper.UploadFileAsync(tempBackupFilePath, backup.Date, _addonOptions.InstanceName, _transferSpeedHelper, destinationFileName,
                    async (prog, speed) =>
                    {
                        if (updateHassEntityState)
                        {
							_hassEntityState.UploadPercentage = prog;
                            _hassEntityState.UploadSpeed = speed / 1024;
							await _hassEntityState.UpdateBackupEntityInHass();
						}

                        progressCallback?.Invoke(prog, speed);
					},
                    description: SerializeBackupDescription(tempBackupFilePath, backup)
                   );
                if (uploadSuccessful == false)
                {
                    await _hassIoClient.PublishEventAsync(Events.OneDriveEvents.BackupUploadFailed);
                    if (_addonOptions.NotifyOnError)
                    {
                        await _hassIoClient.SendPersistentNotificationAsync("Failed uploading backup to onedrive. Check Addon logs for more details");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Error uploading backup: {ex}");
                return false;
            }
            finally
            {
                // Delete temporary backup file
                if (tempBackupFilePath != null)
                {
                    System.IO.File.Delete(tempBackupFilePath);
                }
            }

            return true;
        }

        public async Task DownloadCloudBackupsAsync()
        {
            _hassEntityState.State = HassOnedriveEntityState.BackupState.RecoveryMode;
            await _hassEntityState.UpdateBackupEntityInHass();
            var onlineBackups = await GetOnlineBackupsAsync("*");
            var onlineInstanceBackups = onlineBackups.Where(backup => string.Equals(backup.InstanceName, _addonOptions.InstanceName, StringComparison.OrdinalIgnoreCase)).ToList();
            var localBackups = await GetLocalBackups();

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
                await DownloadBackupFromOneDrive(onlineBackup);
            }
        }

        public async Task<bool> DownloadBackupFromOneDrive(OnedriveBackup onlineBackup, Action<int?>? progressCallback = null, bool updateHassEntityState = true)
		{
            string? backupFile = null;
            try
            {
                ConsoleLogger.LogInfo($"Downloading backup {onlineBackup.FileName}");
                backupFile = await _graphHelper.DownloadFileAsync(onlineBackup.FileName, async (prog) =>
                {
					if (updateHassEntityState)
					{
						_hassEntityState.DownloadPercentage = prog;
						await _hassEntityState.UpdateBackupEntityInHass();
					}

					progressCallback?.Invoke(prog);
				});

                if (backupFile == null)
                {
                    ConsoleLogger.LogError($"Error downloading backup {onlineBackup.FileName}");
                    return false;
                }

                // Upload backup to Home Assistant
                ConsoleLogger.LogInfo($"Loading backup {onlineBackup.FileName} to Home Assisant");
                await _hassIoClient.UploadBackupAsync(backupFile);
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Error fetching backup {onlineBackup.FileName} from Onedrive to Home Assistant. {ex}");
                return false;
            }
            finally
            {
                if (backupFile != null)
                {
                    System.IO.File.Delete(backupFile);
                }
            }

            return true;
        }

        private string SerializeBackupDescription(string originalFileName, Backup backup)
        {
            var description = new OnedriveItemDescription
            {
                Slug = Path.GetFileNameWithoutExtension(originalFileName),
                BackupDate = backup.Date,
                InstanceName = _addonOptions.InstanceName,
                BackupType = backup.Type,
                IsProtected = backup.Protected,
                Size = backup.Size,
                Addons = Enumerable.Empty<string>(), //Temporary workaround for size issue
                Folders = backup.Content?.Folders ?? Enumerable.Empty<string>()
            };

            string serializedDesc = JsonConvert.SerializeObject(description);
            ConsoleLogger.LogVerbose($"Backup Description: {serializedDesc}");
            return serializedDesc;
        }

        private async Task UpdateHassEntity()
        {
            var now = DateTimeHelper.Instance!.Now;
            var localBackups = await GetLocalBackups();
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
            if (onedriveSynced && localSynced)
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

        public async Task RefreshBackupData()
        {
            var getLocalBackupsTask = GetLocalBackups();
            var getOneDriveBackupTasks = GetOnlineBackupsAsync(_addonOptions.InstanceName);
            await Task.WhenAll(getLocalBackupsTask, getOneDriveBackupTasks);
        }

        public async Task<List<OnedriveBackup>> GetOnlineBackupsAsync(string? instanceName)
        {
            var onlineBackups = (await _graphHelper.GetItemsInAppFolderAsync()).Select(CheckIfFileIsBackup).ToList();
            onlineBackups.RemoveAll(item => item == null);
            if (instanceName != "*")
            {
                onlineBackups = onlineBackups.Where(item => string.Equals(instanceName, item.InstanceName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            OnlineBackups = onlineBackups;
            OneDriveBackupsUpdated?.Invoke();
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

        public async Task<List<Backup>> GetLocalBackups()
        {
            var ret = await _hassIoClient.GetBackupsAsync(IsMonitoredBackup);
            LocalBackups = ret;
            LocalBackupsUpdated?.Invoke();
            return ret;
        }

        private bool IsMonitoredBackup(Backup backup)
        {
            // Monitoring All Backups
            if (_addonOptions.MonitorAllLocalBackups)
            {
                // If should ignore upgrade backups and backup seems like an upgrade backup skip it
                if (_addonOptions.IgnoreUpgradeBackups && IsUpgradeBackup(backup))
                {
                    ConsoleLogger.LogVerbose($"Ignoring Upgrade Backup: {backup.Name}");
                    return false;
                }

                // Otherwise back it up
                return true;
            }

            // If addon backup always back up
            if (backup.Name.StartsWith(_addonOptions.BackupNameSafe, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                ConsoleLogger.LogVerbose($"Ignoring 'External' backup: {backup.Name}");
                return false;
            }
        }

        private bool IsUpgradeBackup(Backup backup)
        {
            throw new NotImplementedException();
        }
    }
}
