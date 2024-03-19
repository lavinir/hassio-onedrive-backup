using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using Kusto.Cloud.Platform.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Newtonsoft.Json;
using onedrive_backup;
using onedrive_backup.Contracts;
using onedrive_backup.Extensions;
using onedrive_backup.Graph;
using onedrive_backup.Hass;
using onedrive_backup.Telemetry;
using System.Collections;
using System.Globalization;
using static hassio_onedrive_backup.Contracts.HassAddonsResponse;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    public class BackupManager 
    {
		private const int InstanceNameMaxLength = 20;
		private readonly HassOnedriveEntityState _hassEntityState;
        private readonly HassContext _hassContext;
		private readonly ConsoleLogger _logger;
		private readonly IDateTimeProvider _dateTimeProvider;
        private readonly HassOnedriveFreeSpaceEntityState? _hassOneDriveFreeSpaceEntityState;
        private readonly TelemetryManager? _telemetryManager;
        private readonly BackupAdditionalData _backupAdditionalData;
        private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private IHassioClient _hassIoClient;
		private BitArray _allowedHours;
        protected bool _isExecuting = false;


        public List<Backup> LocalBackups { get; private set; }
        public List<OnedriveBackup> OnlineBackups { get; private set; }

        public BackupManager(IServiceProvider serviceProvider)
        {
            _addonOptions = serviceProvider.GetService<AddonOptions>();
            _graphHelper = serviceProvider.GetService<IGraphHelper>();
            _hassIoClient = serviceProvider.GetService<IHassioClient>();
            _hassEntityState = serviceProvider.GetService<HassOnedriveEntityState>();
            _hassContext = serviceProvider.GetService<HassContext>();
            _logger = serviceProvider.GetService<ConsoleLogger>();
            _dateTimeProvider = serviceProvider.GetService<IDateTimeProvider>();
            _backupAdditionalData = serviceProvider.GetService<BackupAdditionalData>();
            _hassOneDriveFreeSpaceEntityState = serviceProvider.GetService<HassOnedriveFreeSpaceEntityState>();
            _telemetryManager = serviceProvider.GetService<TelemetryManager>();
            UpdateAllowedHours(_addonOptions.BackupAllowedHours);
        }

        public event Action? LocalBackupsUpdated;

        public event Action? OneDriveBackupsUpdated;

        public async Task PerformBackupsAsync()
        {
            if (_isExecuting)
            {
                _logger.LogVerbose("Previous backup iteration still executing. Skipping...");
                return;
            }

            try
            {
                _isExecuting = true;
                var now = _dateTimeProvider.Now;

                _logger.LogVerbose("Refreshing existing backups...");
                await RefreshBackupsAndUpdateHassEntity();

                var onlineBackups = OnlineBackups;

                DateTime lastLocalBackupTime = LocalBackups.Any() ? LocalBackups.Max(backup => backup.Date) : DateTime.MinValue;
                _logger.LogVerbose($"Last local backup Date: {(lastLocalBackupTime == DateTime.MinValue ? "None" : lastLocalBackupTime)}");

                DateTime lastOnlineBackupTime = onlineBackups.Any() ? onlineBackups.Max(backup => backup.BackupDate) : DateTime.MinValue;
                _logger.LogVerbose($"Last online backup Date: {(lastOnlineBackupTime == DateTime.MinValue ? "None" : lastOnlineBackupTime)}");

                // Create local backups if needed
                if ((now - lastLocalBackupTime).TotalHours >= _addonOptions.BackupIntervalHours && (now - lastOnlineBackupTime).TotalHours >= _addonOptions.BackupIntervalHours)
                {
                    if (_allowedHours[now.Hour] == false)
                    {
                        _logger.LogVerbose("Not performing backup outside allowed times");
                    }
                    else
                    {
                        // Refresh Detected Addons
                        var addons = await _hassIoClient.GetAddonsAsync();
                        _logger.LogVerbose($"Detected Addons: {string.Join(",", addons.Select(addon => addon.Slug))}");
                        _hassContext.Addons = addons;

                        //Perform Backup
                        // _hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
                        await _hassEntityState.SyncStart();
                        await CreateLocalBackup();
                        await _hassEntityState.SyncEnd();
                    }
                }

                // Get Online backup candidates
                var onlineBackupCandiates = await GetOnlineBackupCandidatesAsync(LocalBackups);

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
                    _logger.LogInfo($"Found {backupsToUpload.Count()} backups to upload.");

                    // Set Home Assistant Entity state to Syncing
                    // _hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
                    await _hassEntityState.SyncStart();
                    foreach (var backup in backupsToUpload)
                    {
                        await UploadLocalBackupToOneDrive(backup);
                    }

                    // Refresh Online Backups
                    onlineBackups = await GetOnlineBackupsAsync(_addonOptions.InstanceName);
                    await _hassEntityState.SyncEnd();
                }
                else
                {
                    _logger.LogVerbose("Online backups synced. No upload required");
                }


                // Delete Old Online Backups

                // Handle Generational Backups
                IEnumerable<IBackup> generationalBackupsToDelete = Enumerable.Empty<IBackup>();
                if (_addonOptions.GenerationalBackups)
                {
                    _logger.LogVerbose("Evaluating Online Generational Backups");
                    generationalBackupsToDelete = GetGenerationalBackupsForRemoval(onlineBackups.Cast<IBackup>(), "OneDrive");
				}

                int numOfRetainedOnlineBackups = onlineBackups.Count(backup => _backupAdditionalData.IsRetainedOneDrive(backup.Slug));
				int numOfOnlineBackupsToDelete = Math.Max(0, (onlineBackups.Count - numOfRetainedOnlineBackups) - _addonOptions.MaxOnedriveBackups);
                if (numOfOnlineBackupsToDelete > 0)
                {
                    _logger.LogInfo($"Reached Max Online Backups ({_addonOptions.MaxOnedriveBackups})");
                }

                var backupsToDelete = generationalBackupsToDelete
                    .OrderBy(gb => gb.BackupDate)
                    .Union(onlineBackups.OrderBy(ob => ob.BackupDate))
					.Where(backup => !_backupAdditionalData.IsRetainedOneDrive(backup.Slug))
					.Take(numOfOnlineBackupsToDelete).Cast<OnedriveBackup>();
                    
                if (backupsToDelete.Any())
                {
                    //_hassEntityState.State = HassOnedriveEntityState.BackupState.Syncing;
                    //await _hassEntityState.UpdateBackupEntityInHass();
                    await _hassEntityState.SyncStart();

                    _logger.LogInfo($"Found {backupsToDelete.Count()} backups to delete from OneDrive.");
                    foreach (var backupToDelete in backupsToDelete)
                    {
                        bool deleteSuccessfull = await _graphHelper.DeleteItemFromAppFolderAsync(backupToDelete.FileName);
                        if (deleteSuccessfull == false)
                        {
                            await _hassIoClient.PublishEventAsync(Events.OneDriveEvents.OneDriveBackupDeleteFailed);
                            if (_addonOptions.NotifyOnError)
                            {
                                await _hassIoClient.SendPersistentNotificationAsync("Failed deleting old backup from OneDrive. Check Addon logs for more details", PersistantNotificationIds.OneDriveDelete);
                            }
                        }
                    }

                    await _hassEntityState.SyncEnd();
                }

				// Delete Old Local Backups
				// Handle Generational Backups
				generationalBackupsToDelete = Enumerable.Empty<Backup>();
				if (_addonOptions.GenerationalBackups)
				{
					_logger.LogVerbose("Evaluating Local Generational Backups");
					generationalBackupsToDelete = GetGenerationalBackupsForRemoval(LocalBackups.Cast<IBackup>(), "local");
				}

                int numOfRetainedLocalBackups = LocalBackups.Count(backup => _backupAdditionalData.IsRetainedLocally(backup.Slug));
                int numOfLocalBackupsToRemove = (LocalBackups.Count - numOfRetainedLocalBackups) - _addonOptions.MaxLocalBackups;
                if (numOfLocalBackupsToRemove > 0)
                {
					_logger.LogInfo($"Reached Max Local Backups ({_addonOptions.MaxLocalBackups})");
				}


				var localBackupsToRemove = generationalBackupsToDelete
                    .OrderBy(gb => gb.BackupDate)
					.Union(LocalBackups.OrderBy(ob => ob.BackupDate))
					.Where(backup => !_backupAdditionalData.IsRetainedLocally(backup.Slug))
					.Take(numOfLocalBackupsToRemove).Cast<Backup>();

				if (localBackupsToRemove.Any())
                {
                    await _hassEntityState.SyncStart();
					_logger.LogInfo($"Removing {numOfLocalBackupsToRemove} local backups");
					foreach (var localBackup in localBackupsToRemove)
					{
                        _logger.LogVerbose($"Deleting local backup: {localBackup.Slug}");
						bool deleteSuccess = await _hassIoClient.DeleteBackupAsync(localBackup);
						if (deleteSuccess == false)
						{
                            _logger.LogError($"Error removing local backup: {localBackup.Slug}");   
							await _hassIoClient.PublishEventAsync(Events.OneDriveEvents.LocalBackupDeleteFailed);
							if (_addonOptions.NotifyOnError)
							{
								await _hassIoClient.SendPersistentNotificationAsync("Error Deleting Local Backup. Check Addon logs for more details", PersistantNotificationIds.LocalDelete);
							}
						}
					}

				}

                await _hassEntityState.SyncEnd();
				await RefreshBackupsAndUpdateHassEntity();
                if (_hassEntityState.State == HassOnedriveEntityState.BackupState.Stale )
                {

                }

            }
            finally
            {
                _isExecuting = false;
            }   
        }

		private IEnumerable<IBackup> GetGenerationalBackupsForRemoval(IEnumerable<IBackup> backups, string backupType)
		{
            var requiredBackups = new HashSet<IBackup>();
            var now = _dateTimeProvider.Now;

            // Daily Backups
            if (_addonOptions.GenerationalDays.HasValue)
            {
                AddGenerationBackups("Daily", () => backups.GetDailyGenerations(_addonOptions.GenerationalDays.Value, now)); 
			}

			// Weekly Backups
			if (_addonOptions.GenerationalWeeks.HasValue)
			{
				AddGenerationBackups("Weekly", () => backups.GetWeeklyGenerations(_addonOptions.GenerationalWeeks.Value, DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek, now));
			}

			// Monthly Backups
			if (_addonOptions.GenerationalMonths.HasValue)
			{
				AddGenerationBackups("Monthly", () => backups.GetMonthlyGenerations(_addonOptions.GenerationalMonths.Value, now));
			}

			// Yearly Backups
			if (_addonOptions.GenerationalYears.HasValue)
			{
				AddGenerationBackups("Yearly", () => backups.GetYearlyGenerations(_addonOptions.GenerationalYears.Value, now));
			}

            var backupsToRemove = backups.Where(backup => requiredBackups.Contains(backup) == false).ToList();
            _logger.LogVerbose($"Found {backupsToRemove.Count} {backupType} backups that can be removed (Generational Rules)");
            if (backupsToRemove.Any())
            {
				_logger.LogVerbose($"Potential {backupType} backups for removal: {string.Join(",", backupsToRemove.Select(backup => $"{backup.Slug} ({backup.BackupDate})"))}");
			}

			return backupsToRemove;

            // Add Generation Backups to Retention List
			void AddGenerationBackups(string generationName, Func<IEnumerable<IBackup>> getGenerationalBackups)
            {
                var requiredGenerationBackups = getGenerationalBackups();
				foreach (var backup in requiredGenerationBackups)
				{
					_logger.LogVerbose($"Backup ({backup.Slug} ({backup.BackupDate}) retained for Generational {generationName} policy {backupType}");
					requiredBackups.Add(backup);
				}
			}

		}

		public async Task<bool> CreateLocalBackup()
		{
			List<string>? addons = null;
			List<string>? folders = null;

			_logger.LogInfo($"Creating new backup");
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
                _dateTimeProvider.Now,
				compressed: true,
				password: String.IsNullOrEmpty(_addonOptions.BackupPassword) ? null : _addonOptions.BackupPassword,
				addons: addons,
				folders: folders);

			if (backupCreated == false)
			{
				await _hassIoClient.PublishEventAsync(Events.OneDriveEvents.BackupCreateFailed);
				if (_addonOptions.NotifyOnError)
				{
					await _hassIoClient.SendPersistentNotificationAsync("Failed creating local backup. Check Addon logs for more details", PersistantNotificationIds.BackupCreate);
				}
			}
            else
            {
				LocalBackups = await RefreshLocalBackups();
			}

			return backupCreated;		
		}

		public async Task<bool> UploadLocalBackupToOneDrive(Backup backup, Action<int?, int?>? progressCallback = null,  bool updateHassEntityState = true)
        {
            string? tempBackupFilePath = null;
            try
            {
                double backupSizeGB = backup.Size / 1000;
                _logger.LogVerbose($"Backup size to upload: {backupSizeGB.ToString("0.00")}GB");
                if (_hassOneDriveFreeSpaceEntityState!.FreeSpaceGB != null && _hassOneDriveFreeSpaceEntityState.FreeSpaceGB < backupSizeGB)
                {
                    _logger.LogError($"Not enough free space to upload backup ({backup.Slug}). (Required: {backupSizeGB.ToString("0.00")}GB. Available: {((double)_hassOneDriveFreeSpaceEntityState.FreeSpaceGB).ToString("0.00")}GB");
                    return false;
                }

                _logger.LogInfo($"Uploading {backup.Name} ({backup.Date})");
                string? instanceSuffix = _addonOptions.InstanceName == null ? null : $".{_addonOptions.InstanceName.Substring(0, Math.Min(InstanceNameMaxLength, _addonOptions.InstanceName.Length))}";
                string destinationFileName = $"{backup.Name}{instanceSuffix}.tar";
                tempBackupFilePath = await _hassIoClient.DownloadBackupAsync(backup.Slug);
                var uploadSuccessful = await _graphHelper.UploadFileAsync(tempBackupFilePath, backup.Date, _addonOptions.InstanceName, new TransferSpeedHelper(null), destinationFileName,
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
                        await _hassIoClient.SendPersistentNotificationAsync("Failed uploading backup to onedrive. Check Addon logs for more details", PersistantNotificationIds.BackupUpload);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading backup: {ex}", ex, _telemetryManager);
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

        public async Task<bool> DownloadBackupFromOneDrive(OnedriveBackup onlineBackup, Action<int?, int?>? progressCallback = null, bool updateHassEntityState = true)
		{
            string? backupFile = null;
            try
            {
                _logger.LogInfo($"Downloading backup {onlineBackup.FileName}");
                backupFile = await _graphHelper.DownloadFileAsync(onlineBackup.FileName, new TransferSpeedHelper(null), async (prog, speed) =>
                {
					if (updateHassEntityState)
					{
						_hassEntityState.DownloadPercentage = prog;
						await _hassEntityState.UpdateBackupEntityInHass();
					}

					progressCallback?.Invoke(prog, speed);
				});

                if (backupFile == null)
                {
                    _logger.LogError($"Error downloading backup {onlineBackup.FileName}");
                    return false;
                }

                // Upload backup to Home Assistant
                _logger.LogInfo($"Loading backup {onlineBackup.FileName} to Home Assisant");
                await _hassIoClient.UploadBackupAsync(backupFile);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching backup {onlineBackup.FileName} from Onedrive to Home Assistant. {ex}", ex, _telemetryManager);
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
            _logger.LogVerbose($"Backup Description: {serializedDesc}");
            return serializedDesc;
        }

        public async Task UpdateHassEntity()
        {
            var now = _dateTimeProvider.Now;

            if (OnlineBackups != null)
            {
                _hassEntityState.BackupsInOnedrive = OnlineBackups.Count(backup => !backup.IsRetainedOneDrive(_backupAdditionalData));
                _hassEntityState.RetainedOneDriveBackups = _backupAdditionalData.Backups.Count(backup => backup.RetainOneDrive);
                _hassEntityState.LastOnedriveBackupDate = OnlineBackups.Any() ? OnlineBackups.Max(backup => backup.BackupDate) : null;
            }

            if (LocalBackups != null)
            {
                _hassEntityState.LastLocalBackupDate = LocalBackups.Any() ? LocalBackups.Max(backup => backup.Date) : null;
                _hassEntityState.RetainedLocalBackups = _backupAdditionalData.Backups.Count(backup => backup.RetainLocal);
                _hassEntityState.BackupsInHomeAssistant = LocalBackups.Count(backup => !backup.IsRetainedLocally(_backupAdditionalData));
            }

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

        public void UpdateAllowedHours(string allowedHours)
        {
            _allowedHours = TimeRangeHelper.GetAllowedHours(allowedHours);
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
			await UpdateHassEntity();
			return onlineBackups;
        }

        public async Task RefreshBackupsAndUpdateHassEntity()
        {
            var localBackups = await RefreshLocalBackups();
            var onedriveBackups = await GetOnlineBackupsAsync(_addonOptions.InstanceName);
            var existingSlugs = localBackups.Select(backup => backup.Slug).Union(onedriveBackups.Select(backup => backup.Slug)).ToArrayIfNotAlready();
            _backupAdditionalData.PruneAdditionalBackupData(existingSlugs);
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
                _logger.LogWarning($"Unrecognized file found in backup folder : {item.Name}");
                if (string.IsNullOrEmpty(item.Description) == false)
                {
                    _logger.LogVerbose($"{item.Name} Description: {item.Description}");
                }
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

        public async Task<List<Backup>> RefreshLocalBackups()
        {
            var ret = await _hassIoClient.GetBackupsAsync(IsMonitoredBackup);
            LocalBackups = ret;
            LocalBackupsUpdated?.Invoke();
            await UpdateHassEntity();
            return ret;
        }

        protected bool IsMonitoredBackup(Backup backup)
        {
            // Monitoring All Backups
            if (_addonOptions.MonitorAllLocalBackups)
            {
                // If should ignore upgrade backups and backup seems like an upgrade backup skip it
                if (_addonOptions.IgnoreUpgradeBackups && IsUpgradeBackup(backup))
                {
                    _logger.LogVerbose($"Ignoring Upgrade Backup: {backup.Name}");
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
                _logger.LogVerbose($"Ignoring 'External' backup: {backup.Name}");
                return false;
            }
        }

        private bool IsUpgradeBackup(Backup backup)
        {
            // Check if addon upgrade backup
            if (backup.Name.StartsWith("addon_", StringComparison.OrdinalIgnoreCase))
            {
                if (backup.Content?.Homeassistant != true && backup.Content?.Addons?.Count() == 1)
                {
                    _logger.LogVerbose($"Backup {backup.Name} detected as Addon auto upgrade backup");
                    return true;
                }
            }

            // check if core upgrade backup
            if (backup.Name.StartsWith("core_", StringComparison.OrdinalIgnoreCase))
            {
                if (backup.Content?.Homeassistant == true && backup.Content?.Addons.Count() == 0)
                {
                    _logger.LogVerbose($"Backup {backup.Name} detected as Home Assistant auto upgrade backup");
                    return true;
                }
            }

            return false;
        }
    }
}
