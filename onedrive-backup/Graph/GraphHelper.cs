using Azure.Core;
using Azure.Identity;
using hassio_onedrive_backup.Contracts;
using Microsoft.Graph;
using Newtonsoft.Json;
using System.Reflection;
using File = System.IO.File;

namespace hassio_onedrive_backup.Graph
{
    internal class GraphHelper : IGraphHelper
    {
        private const string AuthRecordFile = "record.auth";
        private const int UploadRetryCount = 3;
        private const int GraphRequestTimeoutMinutes = 2;
        private DeviceCodeCredential? _deviceCodeCredential;
        private GraphServiceClient? _userClient;
        private IEnumerable<string> _scopes;
        private string _clientId;
        private Func<DeviceCodeInfo, CancellationToken, Task> _deviceCodePrompt;
        private string _persistentDataPath;

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

        public async Task<List<DriveItem>> GetItemsInAppFolderAsync()
        {
            var items = await _userClient.Drive.Special.AppRoot.Children.Request().GetAsync();
            return items.ToList();
        }

        public async Task<bool> DeleteFileFromAppFolderAsync(string filePath)
        {
            try
            {
                ConsoleLogger.LogInfo($"Deleting file: {filePath}");
                await _userClient.Drive.Special.AppRoot.ItemWithPath(filePath).Request().DeleteAsync();
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Error deleting {filePath}. {ex}");
                return false;
            }

            return true;
        }

        public async Task<bool> UploadFileAsync(string filePath, DateTime date, string? destinationFileName = null, Action<int>? progressCallback = null)
        {
            if (File.Exists(filePath) == false)
            {
                ConsoleLogger.LogError($"File {filePath} not found");
                return false;
            }

            using var fileStream = File.OpenRead(filePath);
            destinationFileName = destinationFileName ?? Path.GetFileName(filePath);
            string originalFileName = Path.GetFileNameWithoutExtension(filePath);
            var uploadSession = await _userClient.Drive.Special.AppRoot.ItemWithPath(destinationFileName).CreateUploadSession(new DriveItemUploadableProperties
            {
                Description = SerializeBackupDescription(originalFileName, date)
            }
                
            ).Request().PostAsync();

            // todo: allow settings this in advanced configuration
            int maxSlizeSize = (320 * 1024) * 10;
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

        private string SerializeBackupDescription(string originalFileName, DateTime date)
        {
            var description = new OnedriveItemDescription
            {
                Slug = originalFileName,
                BackupDate = date
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
