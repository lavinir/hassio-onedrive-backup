using Azure.Core;
using Azure.Identity;
using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Storage;
using Microsoft.Graph;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;
using System.Text.Unicode;
using File = System.IO.File;

namespace hassio_onedrive_backup.Graph
{
    internal class GraphHelper : IGraphHelper
    {
        private const string AuthRecordFile = "record.auth";
        private const int UploadRetryCount = 3;
        private const int DownloadRetryCount = 3;
        private const int GraphRequestTimeoutMinutes = 2;
        private const int ChunkSize = (320 * 1024) * 10;
        private DeviceCodeCredential? _deviceCodeCredential;
        private GraphServiceClient? _userClient;
        private IEnumerable<string> _scopes;
        private string _clientId;
        private Func<DeviceCodeInfo, CancellationToken, Task> _deviceCodePrompt;
        private string _persistentDataPath;
        private HttpClient _downloadHttpClient;

        public GraphHelper(
            IEnumerable<string> scopes,
            string clientId,
            Func<DeviceCodeInfo, CancellationToken, Task> deviceCodePrompt,
            string persistentDataPath = "")
        {
            _scopes = scopes;
            _clientId = clientId;
            _deviceCodePrompt = deviceCodePrompt;
            _persistentDataPath = persistentDataPath;
        }

        private string PersistentAuthRecordFullPath => Path.Combine(_persistentDataPath, AuthRecordFile);

        public async Task<string> GetAndCacheUserTokenAsync()
        {
            if (_deviceCodeCredential == null)
            {
                await InitializeGraphForUserAuthAsync();
            }

            _ = _deviceCodeCredential ??
                throw new NullReferenceException("User Auth not Initialized");

            _ = _scopes ?? throw new ArgumentNullException("'scopes' cannot be null");

            var context = new TokenRequestContext(_scopes.ToArray());
            var response = await _deviceCodeCredential.GetTokenAsync(context);
            await PersistAuthenticationRecordAsync(GetAuthenticationRecordFromCredential());
            return response.Token;
        }

        public async Task<DriveItem?> GetItemInAppFolderAsync(string subPath = "")
        {
            try
            {
                var item = await _userClient.Drive.Special.AppRoot.ItemWithPath(subPath).Request().Expand("children").GetAsync();
                return item;
                // return item.Children.ToList();
            }
            catch (ServiceException se)
            {
                if (se.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw;
                }

                return null;
            }

        }

        public async Task<List<DriveItem>?> GetItemsInAppFolderAsync(string subPath = "")
        {
            var parent = await GetItemInAppFolderAsync(subPath);
            return parent?.Children?.ToList();
        }

        public async Task<bool> DeleteItemFromAppFolderAsync(string itemPath)
        {
            try
            {
                ConsoleLogger.LogInfo($"Deleting item: {itemPath}");
                await _userClient.Drive.Special.AppRoot.ItemWithPath(itemPath).Request().DeleteAsync();
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Error deleting {itemPath}. {ex}");
                return false;
            }

            return true;
        }

        public async Task<DriveItem> GetOrCreateFolder(string folderPath)
        {
            var folder = (await GetItemInAppFolderAsync(folderPath)) ??
                await _userClient.Drive.Special.AppRoot.ItemWithPath(folderPath).Children.Request().AddAsync(new DriveItem
                {
                    // Name = Path.GetFileName(folderPath),
                    // Folder = new Folder { }
                    File = new Microsoft.Graph.File { },
                    Name = "temp.txt",
                    Content = new MemoryStream(Encoding.UTF8.GetBytes("Here's your damn content"))
                });

            return folder;
        }

        public async Task<bool> UploadFileAsync(string filePath, DateTime date, string? instanceName, string? destinationFileName = null, Action<int>? progressCallback = null, bool flatten = true, bool omitDescription = false)
        {
            if (File.Exists(filePath) == false)
            {
                ConsoleLogger.LogError($"File {filePath} not found");
                return false;
            }

            using var fileStream = File.OpenRead(filePath);
            destinationFileName = destinationFileName ?? (flatten ? Path.GetFileName(filePath) : filePath);
            string originalFileName = Path.GetFileNameWithoutExtension(filePath);
            var uploadSession = await _userClient.Drive.Special.AppRoot.ItemWithPath(destinationFileName).CreateUploadSession(new DriveItemUploadableProperties
            {
                Description = omitDescription ? null : SerializeBackupDescription(originalFileName, date, instanceName)                
            }

            ).Request().PostAsync();

            // todo: allow settings this in advanced configuration
            int maxSlizeSize = ChunkSize;
            long totalFileLength = fileStream.Length;
            var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSlizeSize);
            var lastShownPercentageHolder = new UploadProgressHolder();
            IProgress<long> progress = new Progress<long>(prog =>
            {
                double percentage = Math.Round((prog / (double)totalFileLength), 2) * 100;
                if (percentage - lastShownPercentageHolder.Percentage >= 10)
                {
                    ConsoleLogger.LogInfo($"Uploaded {percentage}%");
                    lastShownPercentageHolder.Percentage = percentage;
                }

                progressCallback?.Invoke((int)percentage);
            });

            int uploadAttempt = 0;
            while (uploadAttempt++ < UploadRetryCount)
            {
                try
                {
                    ConsoleLogger.LogInfo($"Starting file upload. (Size:{totalFileLength} bytes. Attempt: {uploadAttempt}/{UploadRetryCount})");
                    UploadResult<DriveItem> uploadResult;
                    if (uploadAttempt > 1)
                    {
                        uploadResult = await fileUploadTask.ResumeAsync(progress);
                    }
                    else
                    {
                        uploadResult = await fileUploadTask.UploadAsync(progress);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2));
                    if (uploadResult.UploadSucceeded)
                    {
                        ConsoleLogger.LogInfo("Upload completed successfully");
                        break;
                    }
                    else
                    {
                        ConsoleLogger.LogError("Upload failed");
                    }
                }
                catch (ServiceException ex)
                {
                    ConsoleLogger.LogError($"Error uploading: {ex}");
                    return false;
                }
            }

            return true;
        }

        public async Task<double?> GetFreeSpaceInGB()
        {
            try
            {
                var drive = await _userClient.Drive.Request().GetAsync();
                double? ret = drive.Quota.Remaining == null ? null : drive.Quota.Remaining.Value / (double)Math.Pow(1024, 3);
                return (double?)ret;

            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Error getting free space: {ex}");
                return null;
            }
        }
    
        public async Task<string?> DownloadFileAsync(string fileName, Action<int?>? progressCallback) 
        {
            var item = await _userClient.Drive.Special.AppRoot.ItemWithPath(fileName).Request().GetAsync();
            if (item.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out var downloadUrl) == false)
            {
                ConsoleLogger.LogError($"Failed getting file download data. ${fileName}");
                return null;
            }

            var fileInfo = new FileInfo($"{LocalStorage.TempFolder}/{fileName}");
            using var fileStream = File.Create(fileInfo.FullName);

            _downloadHttpClient = _downloadHttpClient ?? new HttpClient();
            long position = 0;
            int attempt = 1;
            while (position < item.Size)
            {
                try
                {
                    long chunkSize = Math.Min(position + ChunkSize, item.Size.Value - 1);
                    _downloadHttpClient.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(position, chunkSize);
                    var contentStream = await _downloadHttpClient.GetStreamAsync(downloadUrl.ToString());
                    await contentStream.CopyToAsync(fileStream);
                    position = chunkSize + 1;
                    progressCallback?.Invoke((int)(position * 100 / item.Size.Value));
                }
                catch (Exception ex)
                {
                    if (attempt >= DownloadRetryCount)
                    {
                        ConsoleLogger.LogError($"Failed downloading file {fileName}. {ex}");
                        progressCallback?.Invoke(null);
                        return null;
                    }

                    await Task.Delay(5000);
                }
            }

            progressCallback?.Invoke(null);
            ConsoleLogger.LogInfo($"{fileName} downloaded successfully");
            return fileInfo.FullName;
        }

        private string SerializeBackupDescription(string originalFileName, DateTime date, string instanceName)
        {
            var description = new OnedriveItemDescription
            {
                Slug = originalFileName,
                BackupDate = date,
                InstanceName = instanceName
            };

            return JsonConvert.SerializeObject(description);
        }

        private AuthenticationRecord GetAuthenticationRecordFromCredential()
        {
            var record = typeof(DeviceCodeCredential)
                .GetProperty("Record", System.Reflection.BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_deviceCodeCredential) as AuthenticationRecord;

            return record;
        }

        private async Task PersistAuthenticationRecordAsync(AuthenticationRecord record)
        {
            using var authRecordStream = new FileStream(PersistentAuthRecordFullPath, FileMode.Create, FileAccess.Write);
            await record.SerializeAsync(authRecordStream);
        }

        private async Task<AuthenticationRecord?> ReadPersistedAuthenticationRecordAsync()
        {
            if (File.Exists(PersistentAuthRecordFullPath) == false)
            {
                ConsoleLogger.LogWarning("Token cache is empty");
                return null;
            }

            using var authRecordStream = new FileStream(PersistentAuthRecordFullPath, FileMode.Open, FileAccess.Read);
            var record = await AuthenticationRecord.DeserializeAsync(authRecordStream);
            return record;
        }

        private async Task InitializeGraphForUserAuthAsync()
        {
            AuthenticationRecord? authRecord = await ReadPersistedAuthenticationRecordAsync();
            var deviceCodeCredOptions = new DeviceCodeCredentialOptions
            {
                ClientId = _clientId,
                DeviceCodeCallback = _deviceCodePrompt,
                TenantId = "common",
                AuthenticationRecord = authRecord,
                TokenCachePersistenceOptions = new TokenCachePersistenceOptions
                {
                    Name = "hassio-onedrive-backup",
                    UnsafeAllowUnencryptedStorage = true
                }
            };

            _deviceCodeCredential = new DeviceCodeCredential(deviceCodeCredOptions);
            _userClient = new GraphServiceClient(_deviceCodeCredential, _scopes);
            _userClient.HttpProvider.OverallTimeout = TimeSpan.FromMinutes(GraphRequestTimeoutMinutes);
        }

        private class UploadProgressHolder
        {
            public double Percentage { get; set; } = 0;
        }
    }
}
