using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Graph;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace hassio_onedrive_backup.Sync
{
    internal class SyncManager
    {
        private const string HashAlgo = "SHA256";
        private const string OneDriveFileSyncRootDir = "FileSync";
        private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private IHassioClient _hassIoClient;
        private BitArray _allowedHours;
        private HassOnedriveFileSyncEntityState _hassEntityState;
		private IWebHostEnvironment? _environment;
		private Matcher _fileMatcher;

        public SyncManager(IServiceProvider serviceProvider, BitArray allowedHours)
        {
            _addonOptions = serviceProvider.GetService<AddonOptions>();
            _graphHelper = serviceProvider.GetService<IGraphHelper>();
            _hassIoClient = serviceProvider.GetService<IHassioClient>();
            _hassEntityState = serviceProvider.GetService<HassOnedriveFileSyncEntityState>();
            _environment = serviceProvider.GetService<IWebHostEnvironment>();
            _allowedHours = allowedHours;
            _fileMatcher = new();
            _fileMatcher.AddIncludePatterns(_addonOptions.SyncPaths);
        }

        public async void SyncLoop(CancellationToken ct)
        {
            while (true && ct.IsCancellationRequested == false)
            {
                try
                {
                    var now = DateTimeHelper.Instance!.Now;

                    // Check if we are in the allowed hours
                    if (_allowedHours[now.Hour] == false)
                    {
                        continue;
                    }

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
                            ConsoleLogger.LogError($"Error syncing file {matchingFile}: {ex}");
						}
					}

                    if (_addonOptions.FileSyncRemoveDeleted)
                    {
                        await DeleteRemovedFilesFromOneDrive($"{OneDriveFileSyncRootDir}", "/");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Error Syncing: {ex}");
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
                ConsoleLogger.LogWarning($"Skipping 0-byte file: {path}");
                return;
            }

            var now = DateTimeHelper.Instance!.Now;
            string fileHash = CalculateFileHash(path);
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
                return;
            }
            
            ConsoleLogger.LogInfo($"File {path} out of sync. Starting Upload");
            var uploadSuccessful = await _graphHelper.UploadFileAsync(
                path, 
                now, 
                _addonOptions.InstanceName, 
                remotePath,
                async (prog) =>
                {
                    _hassEntityState.UploadPercentage = prog;
                    await _hassEntityState.UpdateBackupEntityInHass();
                    Debug.WriteLine($"Progress: {prog}");

                },
                flatten: false                
            );
        }

        private string CalculateFileHash(string path)
        {
            using (var hasher = HashAlgorithm.Create(HashAlgo))
            {
                using (var fileStream = System.IO.File.OpenRead(path))
                {
                    byte[] hash = hasher!.ComputeHash(fileStream);
                    return BitConverter.ToString(hash).Replace("-", "");
                }
            }
        }

        private async Task DeleteRemovedFilesFromOneDrive(string remotePath, string localPath)
        {
            var item = await _graphHelper.GetItemInAppFolderAsync(remotePath);
            localPath = Path.Combine(localPath, item.Name);
            if (localPath.StartsWith($"/{OneDriveFileSyncRootDir}"))
            {
                localPath = "/";
            }
            
            if (item.Folder != null)
            {
                if (System.IO.Directory.Exists(localPath) == false)
                {
                    ConsoleLogger.LogInfo($"{localPath} does not exist locally. Deleting from OneDrive");
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
                    ConsoleLogger.LogInfo($"{localPath} does not exist locally. Deleting from OneDrive");
                    await _graphHelper.DeleteItemFromAppFolderAsync(remotePath);
                }
            }
        }

    }
}
