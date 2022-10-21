using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Reflection;
using File = System.IO.File;

namespace hassio_onedrive_backup.Graph
{
    internal class GraphHelper
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

        public string PersistantAuthRecordFullPath => Path.Combine(_persistentDataPath, AuthRecordFile);

        public async Task InitializeGraphForUserAuthAsync()
        {
            AuthenticationRecord? authRecord = await ReadPersistedAuthenticationRecord();
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

        public async Task<string> GetUserTokenAsync()
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
            await PersistAuthenticationRecord(GetAuthenticationRecordFromCredential());
            return response.Token;
        }

        public async Task TestClient()
        {
            var item = await _userClient.Drive.Special.AppRoot.Request().GetAsync();
        }


        public async Task UploadFileAsync(string filePath, string? destinationFileName = null)
        {
            if (File.Exists(filePath) == false)
            {
                ConsoleLogger.LogError($"File {filePath} not found");
                return;
            }

            using var fileStream = System.IO.File.OpenRead(filePath);
            destinationFileName = destinationFileName ?? Path.GetFileName(filePath);           
            var uploadSession = await _userClient.Drive.Special.AppRoot.ItemWithPath(destinationFileName).CreateUploadSession().Request().PostAsync();

            int maxSlizeSize = (320 * 1024) * 10;
            long totalFileLength = fileStream.Length;
            var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSlizeSize);
            IProgress<long> progress = new Progress<long>(prog =>
            {
                ConsoleLogger.LogInfo($"Uploaded {prog} bytes of {totalFileLength} bytes");
            });

            int uploadAttempt = 0;
            while (uploadAttempt++ < UploadRetryCount)
            {
                try
                {
                    ConsoleLogger.LogInfo($"Attempting File Upload (Attempt {uploadAttempt}/{UploadRetryCount})");
                    UploadResult<DriveItem> uploadResult;
                    if (uploadAttempt > 1)
                    {
                        uploadResult = await fileUploadTask.ResumeAsync(progress);
                    }
                    else
                    {
                        uploadResult = await fileUploadTask.UploadAsync(progress);
                    }

                    if (uploadResult.UploadSucceeded)
                    {
                        ConsoleLogger.LogInfo("Upload Completed Successfully");
                        break;
                    }
                    else
                    {
                        ConsoleLogger.LogError("Upload Failed");
                    }
                }
                catch (ServiceException ex)
                {
                    ConsoleLogger.LogError($"Error uploading: {ex}");
                }
            }
        }

        private AuthenticationRecord GetAuthenticationRecordFromCredential()
        {
            var record = typeof(DeviceCodeCredential)
                .GetProperty("Record", System.Reflection.BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_deviceCodeCredential) as AuthenticationRecord;

            return record;
        }

        private async Task PersistAuthenticationRecord(AuthenticationRecord record)
        {
            using var authRecordStream = new FileStream(PersistantAuthRecordFullPath, FileMode.Create, FileAccess.Write);
            await record.SerializeAsync(authRecordStream);
        }

        private async Task<AuthenticationRecord?> ReadPersistedAuthenticationRecord()
        {
            if (File.Exists(PersistantAuthRecordFullPath) == false)
            {
                ConsoleLogger.LogWarning("Token Cache is Empty");
                return null;
            }

            using var authRecordStream = new FileStream(PersistantAuthRecordFullPath, FileMode.Open, FileAccess.Read);
            var record = await AuthenticationRecord.DeserializeAsync(authRecordStream);
            return record;
        }
    }
}
