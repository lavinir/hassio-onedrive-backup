using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using onedrive_backup;
using onedrive_backup.Extensions;
using onedrive_backup.Graph;
using onedrive_backup.Sync;
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Sync
{
    public class SyncManager
    {
        public const string OneDriveFileSyncRootDir = "FileSync";
		private readonly ConsoleLogger _logger;
        private readonly HassOnedriveFreeSpaceEntityState? _hassOneDriveFreeSpaceEntityState;
        private readonly IDateTimeProvider _dateTimeProvider;
		private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private BitArray _allowedHours;
        private HassOnedriveFileSyncEntityState _hassEntityState;
		private IWebHostEnvironment? _environment;
        private TransferSpeedHelper _transferSpeedHelper;
        private Matcher _fileMatcher;

        public SyncManager(IServiceProvider serviceProvider, BitArray allowedHours, TransferSpeedHelper? transferSpeedHelper, ConsoleLogger logger, IDateTimeProvider dateTimeProvider)
        {
            _addonOptions = serviceProvider.GetService<AddonOptions>();
            _graphHelper = serviceProvider.GetService<IGraphHelper>();
            _hassEntityState = serviceProvider.GetService<HassOnedriveFileSyncEntityState>();
            _environment = serviceProvider.GetService<IWebHostEnvironment>();
            _hassOneDriveFreeSpaceEntityState = serviceProvider.GetService<HassOnedriveFreeSpaceEntityState>();
            _transferSpeedHelper = transferSpeedHelper;
            _allowedHours = allowedHours;
            _fileMatcher = new();
            _fileMatcher.AddIncludePatterns(_addonOptions.SyncPaths.Where(path => string.IsNullOrWhiteSpace(path) == false));
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;
        }

        public void UpdateFileMatcherPaths()
        {
			_fileMatcher = new();
			_fileMatcher.AddIncludePatterns(_addonOptions.SyncPaths.Where(path => string.IsNullOrWhiteSpace(path) == false));
		}

		public async void SyncLoop(CancellationToken ct)
        {
            _logger.LogVerbose($"Setting up Sync Loop");
            while (true && ct.IsCancellationRequested == false)
            {
                try
                {
                    if (_addonOptions.FileSyncEnabled == false)
                    {
						_logger.LogVerbose($"File Sync Disabled..."); //Todo: Remove
						await Task.Delay(TimeSpan.FromMinutes(5));
						continue;
					}

					var now = _dateTimeProvider.Now;

                    // Check if we are in the allowed hours
                    if (_allowedHours[now.Hour] == false && _addonOptions.IgnoreAllowedHoursForFileSync == false)
                    {
                        _logger.LogVerbose($"Skipping syncing (Outside allowed hours)");
						await Task.Delay(TimeSpan.FromMinutes(5));
						continue;
                    }

					_logger.LogVerbose($"Evaulating File Sync Diffs");
					_hassEntityState.State = HassOnedriveFileSyncEntityState.FileState.Syncing;
                    await _hassEntityState.UpdateBackupEntityInHass();

                    var matchingFiles = _fileMatcher.GetResultsInFullPath("/");
                    foreach (var matchingFile in matchingFiles)
                    {
                        try
                        {
							await SyncFile(matchingFile);
						}
						catch (Exception ex)
                        {
                            _logger.LogError($"Error syncing file {matchingFile}: {ex}");
						}
					}

                    if (_addonOptions.FileSyncRemoveDeleted)
                    {
                        await DeleteRemovedFilesFromOneDrive($"{OneDriveFileSyncRootDir}", "/");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error Syncing: {ex}");
                }
                finally
                {
                    _hassEntityState.State = HassOnedriveFileSyncEntityState.FileState.Synced;
                    await _hassEntityState.UpdateBackupEntityInHass();
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            }
        }

        private async Task SyncFile(string path)
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length == 0)
            {
                _logger.LogWarning($"Skipping 0-byte file: {path}");
                return;
            }

            _logger.LogVerbose($"Evaluating file: {fileInfo.FullName}");
            var now = _dateTimeProvider.Now;
            string fileHash = FileOperationHelper.CalculateFileHash(path);
            var localFileSyncData = new SyncFileData(path, fileHash, fileInfo.Length);

            string remotePath = $"/{OneDriveFileSyncRootDir}{path}".Replace("//", "/").Replace(@"\\", @"\");
            if (_environment!.IsDevelopment())
            {
                remotePath = remotePath.Replace(@"c:\", "/", StringComparison.OrdinalIgnoreCase);
            }

            DriveItem? remoteFile = await _graphHelper.GetItemInAppFolderAsync(remotePath);                             
            bool requiresUpload = 
                remoteFile == null 
                || remoteFile.Size != fileInfo.Length 
                || remoteFile.File.Hashes.Sha256Hash.Equals(fileHash, StringComparison.OrdinalIgnoreCase) == false;

            if (requiresUpload == false)
            {
				_logger.LogVerbose($"File: {fileInfo.FullName} up to date. No sync required");
				return;
            }

            double fileSizeGB = fileInfo.Length / Math.Pow(10, 9);
            _logger.LogVerbose($"File size to upload: {fileSizeGB.ToString("0.00")}GB");
            if (_hassOneDriveFreeSpaceEntityState!.FreeSpaceGB != null && _hassOneDriveFreeSpaceEntityState.FreeSpaceGB < fileSizeGB)
            {
                _logger.LogError($"Not enough free space to upload file ({fileInfo.Name}). (Required: {fileSizeGB.ToString("0.00")}GB. Available: {(double)_hassOneDriveFreeSpaceEntityState.FreeSpaceGB:0.00}GB");
                return;
            }


            _logger.LogInfo($"File {path} out of sync. Starting Upload");
            var uploadSuccessful = await _graphHelper.UploadFileAsync(
                path, 
                now, 
                _addonOptions.InstanceName, 
                _transferSpeedHelper,
                remotePath,
                async (prog, speed) =>
                {
                    _hassEntityState.UploadPercentage = prog;
                    _hassEntityState.UploadSpeed = speed / 1024;
                    await _hassEntityState.UpdateBackupEntityInHass();
                    Debug.WriteLine($"Progress: {prog}");

                },
                flatten: false                
            );
        }

        private async Task DeleteRemovedFilesFromOneDrive(string remotePath, string localPath)
        {
            var item = await _graphHelper.GetItemInAppFolderAsync(remotePath);
            if (item == null)
            {
                return;
            }

            _logger.LogVerbose($"Evaulating {remotePath}");
            localPath = Path.Combine(localPath, item.Name);
            if (localPath.StartsWith($"/{OneDriveFileSyncRootDir}"))
            {
                localPath = "/";
            }
            
            if (item.Folder != null)
            {
                if (System.IO.Directory.Exists(localPath) == false)
                {
                    _logger.LogInfo($"{localPath} does not exist locally. Deleting from OneDrive");
                    await _graphHelper.DeleteItemFromAppFolderAsync(remotePath);
                }
                else
                {
                    // Recursive call for files in Folder
                    var folderItems = await _graphHelper.GetItemsInAppFolderAsync(remotePath);
                    foreach (var folderItem in folderItems)
                    {
                        await DeleteRemovedFilesFromOneDrive(Path.Combine(remotePath, folderItem.Name), localPath);
                    }

                    // If no content left in folder then remove it
					folderItems = await _graphHelper.GetItemsInAppFolderAsync(remotePath);
                    if (folderItems.Count == 0)
                    {
						await _graphHelper.DeleteItemFromAppFolderAsync(remotePath);
					}
				}
			}
            else if (item.File != null)
            {
                if (System.IO.File.Exists(localPath) == false)
                {
                    _logger.LogInfo($"{localPath} does not exist locally. Deleting from OneDrive");
                    await _graphHelper.DeleteItemFromAppFolderAsync(remotePath);
                }
                else if (_fileMatcher.Match(localPath.StripLeadingSlash()).HasMatches == false)
                {
					_logger.LogInfo($"{localPath} not included in Sync Paths. Deleting from OneDrive");
					await _graphHelper.DeleteItemFromAppFolderAsync(remotePath);
				}
                else
                {
                    _logger.LogVerbose($"{remotePath} in sync with {localPath}");
                }
			}
        }

    }
}
