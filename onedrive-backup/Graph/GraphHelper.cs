using Azure.Core;
using Azure.Identity;
using hassio_onedrive_backup.Storage;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using onedrive_backup;
using onedrive_backup.Contracts;
using onedrive_backup.Extensions;
using onedrive_backup.Graph;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using File = System.IO.File;
using DriveUpload = Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using onedrive_backup.Telemetry;
using Azure.Core.Diagnostics;

namespace hassio_onedrive_backup.Graph
{
    public class GraphHelper : IGraphHelper
    {
        private const string AuthRecordFile = "record.auth";
        private const int UploadRetryCount = 3;
        private const int DownloadRetryCount = 3;
        private const int GraphRequestTimeoutMinutes = 2;
        private const int ChunkSize = (320 * 1024) * 10;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ConsoleLogger _logger;
        private readonly TelemetryManager _telemetryManager;
        private DeviceCodeCredential? _deviceCodeCredential;
        protected GraphServiceClient? _userClient;
        private IEnumerable<string> _scopes;
        private string _clientId;
        private string _persistentDataPath;
        private HttpClient _downloadHttpClient;
        private bool? _isAuthenticated = null;

        public event AuthStatusChanged? AuthStatusChangedEventHandler;

        public GraphHelper(
            IEnumerable<string> scopes,
            string clientId,
            IDateTimeProvider dateTimeProvider,
            ConsoleLogger logger,
            TelemetryManager telemetryManager,
            string persistentDataPath = "")
        {
            _scopes = scopes;
            _clientId = clientId;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;
            _telemetryManager = telemetryManager;
            _persistentDataPath = persistentDataPath;

            // AzureEventSourceListener.CreateConsoleLogger();
        }

        public bool? IsAuthenticated
        {
            get => _isAuthenticated;
            private set
            {
                _isAuthenticated = value; AuthStatusChangedEventHandler?.Invoke();
            }
        }

        public string AuthUrl { get; private set; }

        public string AuthCode { get; private set; }

        private string PersistentAuthRecordFullPath => Path.Combine(_persistentDataPath, AuthRecordFile);

        public async Task GetAndCacheUserTokenAsync()
        {
            if (_deviceCodeCredential == null)
            {
                await InitializeGraphForUserAuthAsync();
            }

            _ = _deviceCodeCredential ??
                throw new NullReferenceException("User Auth not Initialized");

            _ = _scopes ?? throw new ArgumentNullException("'scopes' cannot be null");

            if (GetAuthenticationRecordFromCredential(_deviceCodeCredential) == null)
            {
                _logger.LogVerbose("Missing Auth Record in Device Credential");
                var context = new TokenRequestContext(_scopes.ToArray());
                // var response = await _deviceCodeCredential.GetTokenAsync(context);
                var authRecord = await _deviceCodeCredential.AuthenticateAsync(context);
                await PersistAuthenticationRecordAsync(authRecord);
            }
            else
            {
                _logger.LogVerbose("Token Cache exists. Skipping Auth");
            }

            IsAuthenticated = true;
            // return response.Token;
        }

        public async Task<DriveItem?> GetItemInAppFolderAsync(string subPath = "/")
        {
            try
            {
                var driveItem = await _userClient.Me.Drive.GetAsync();
                IsAuthenticated = true;
                var appFolder = await _userClient.Drives[driveItem.Id].Special["approot"].GetAsync();
                DriveItem? item;

                if (subPath == "/")
                {
                    item = await _userClient.Drives[driveItem.Id].Items[appFolder.Id].GetAsync(config => config.QueryParameters.Expand = new string[] { "children" });
                }
                else
                {
                    item = await _userClient.Drives[driveItem.Id].Items[appFolder.Id].ItemWithPath(subPath).GetAsync(config => config.QueryParameters.Expand = new string[] { "children" });
                }

                return item;
            }
            catch (ODataError oe) when (oe.Error?.Code == "itemNotFound")
            {
                _logger.LogInfo($"Item {subPath} not found");
                return null;
            }
            catch (ODataError oe)
            {
                _logger.LogError($"{oe.Error.Code}: {oe.Error.Message}", oe, _telemetryManager);
                return null;
            }


        }

        public async Task<List<DriveItem>?> GetItemsInAppFolderAsync(string subPath = "/")
        {
            var parent = await GetItemInAppFolderAsync(subPath);
            return parent?.Children?.ToList();
        }

        public async Task<bool> DeleteItemFromAppFolderAsync(string itemPath)
        {
            try
            {
                _logger.LogInfo($"Deleting item: {itemPath}");
                var driveItem = await _userClient.Me.Drive.GetAsync();
                var appFolder = await _userClient.Drives[driveItem.Id].Special["approot"].GetAsync();
                await _userClient.Drives[driveItem.Id].Items[appFolder.Id].ItemWithPath(itemPath).DeleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting {itemPath}. {ex}");
                return false;
            }

            return true;
        }

        public async Task<bool> UploadFileAsync(string filePath, DateTime date, string? instanceName, TransferSpeedHelper transferSpeedHelper, string? destinationFileName = null, Action<int, int>? progressCallback = null, bool flatten = true, string description = null)
        {
            if (File.Exists(filePath) == false)
            {
                _logger.LogError($"File {filePath} not found", telemetryManager: _telemetryManager);                
                return false;
            }

            using var fileStream = File.OpenRead(filePath);
            destinationFileName = destinationFileName ?? (flatten ? Path.GetFileName(filePath) : filePath);
            string sanitizedDestinationFileName = NormalizeDestinationFileName(destinationFileName);
            var driveItem = await _userClient.Me.Drive.GetAsync();
            var appFolder = await _userClient.Drives[driveItem.Id].Special["approot"].GetAsync();

            var uploadSession = await _userClient.Drives[driveItem?.Id]
                .Items[appFolder.Id]
                .ItemWithPath(sanitizedDestinationFileName)
                .CreateUploadSession
                .PostAsync(new DriveUpload.CreateUploadSessionPostRequestBody()
                {
                    Item = new DriveItemUploadableProperties
                    {
                        Description = description
                    }
                });

            // todo: allow settings this in advanced configuration
            int maxSliceSize = ChunkSize;
            long totalFileLength = fileStream.Length;
            var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSliceSize);
            var lastShownPercentageHolder = new TransferProgressHolder();
            IProgress<long> progress = new Progress<long>(async prog =>
            {
                (double delayMS, double speed) = transferSpeedHelper.MarkAndCalcThrottle(prog);
                double percentage = Math.Round((prog / (double)totalFileLength), 2) * 100;
                if (percentage - lastShownPercentageHolder.Percentage >= 10 || percentage == 100)
                {
                    _logger.LogVerbose($"Uploaded {percentage}%");
                    lastShownPercentageHolder.Percentage = percentage;
                }

                progressCallback?.Invoke((int)percentage, (int)speed);
            });

            int uploadAttempt = 0;
            while (uploadAttempt++ < UploadRetryCount)
            {
                try
                {
                    _logger.LogInfo($"Starting file upload. (Size:{totalFileLength} bytes. Attempt: {uploadAttempt}/{UploadRetryCount})");
                    UploadResult<DriveItem> uploadResult;
                    transferSpeedHelper.Start();
                    if (uploadAttempt > 1)
                    {
                        uploadResult = await fileUploadTask.ResumeAsync(progress);
                    }
                    else
                    {
                        uploadResult = await fileUploadTask.UploadAsync(progress);
                    }

                    transferSpeedHelper.Reset();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    if (uploadResult.UploadSucceeded)
                    {
                        _logger.LogInfo("Upload completed successfully");
                        break;
                    }
                    else
                    {
                        _logger.LogError("Upload failed");
                    }
                }
                catch (ServiceException ex)
                {
                    _logger.LogError($"Error uploading: {ex}", ex, _telemetryManager);
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeDestinationFileName(string destinationFileName)
        {
            int fileNameIdx = destinationFileName.IndexOf(System.IO.Path.GetFileName(destinationFileName));
            string destinationFileNameWithoutFileName = destinationFileName.Substring(0, fileNameIdx);
            string fileName = Path.GetFileName(destinationFileName);
            string sanitizedFileName = destinationFileNameWithoutFileName + fileName.SanitizeString();
            return sanitizedFileName;
        }

        public async Task<OneDriveFreeSpaceData> GetFreeSpaceInGB()
        {
            try
            {
                var drive = await _userClient.Me.Drive.GetAsync();
                IsAuthenticated = true;
                double? totalSpace = drive.Quota.Total == null ? null : drive.Quota.Total.Value / (double)Math.Pow(1024, 3);
                double? freeSpace = drive.Quota.Remaining == null ? null : drive.Quota.Remaining.Value / (double)Math.Pow(1024, 3);
                return new OneDriveFreeSpaceData
                {
                    FreeSpace = freeSpace,
                    TotalSpace = totalSpace
                };

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting free space: {ex}", ex, _telemetryManager);
                return null;
            }
        }

        public async Task<string?> DownloadFileAsync(string fileName, TransferSpeedHelper transferSpeedHelper, Action<int, int>? progressCallback)
        {
            var drive = await _userClient.Me.Drive.GetAsync();

            var driveItem = await _userClient.Me.Drive.GetAsync();
            var appFolder = await _userClient.Drives[driveItem.Id].Special["approot"].GetAsync();
            var item = await _userClient.Drives[driveItem?.Id]
                .Items[appFolder.Id]
                .ItemWithPath(fileName)
                .GetAsync();

            transferSpeedHelper.Start();
            var itemStream = await _userClient.Drives[driveItem?.Id]
                .Items[appFolder.Id]
                .ItemWithPath(fileName)
                .Content
                .GetAsync();

            
            var fileInfo = new FileInfo($"{LocalStorage.TempFolder}/{fileName}");
            using var fileStream = File.Create(fileInfo.FullName);

            long totalBytesDownloaded = 0;
            int attempt = 1;
            int bytesRead = 1;
            var lastShownPercentageHolder = new TransferProgressHolder();
            while (totalBytesDownloaded < item.Size && bytesRead > 0)
            {
                try
                {
                    var buffer = new byte[ChunkSize];
                    bytesRead = await itemStream.ReadAsync(buffer, 0, ChunkSize);
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesDownloaded += bytesRead;
                    (double delay, double speed) = transferSpeedHelper.MarkAndCalcThrottle(totalBytesDownloaded);
                    double percentage = Math.Round((totalBytesDownloaded / (double)item.Size), 2) * 100;
                    if (percentage - lastShownPercentageHolder.Percentage >= 10 || percentage == 100)
                    {
                        _logger.LogVerbose($"Downloaded {percentage}%");
                        lastShownPercentageHolder.Percentage = percentage;
                    }
                    if (percentage - lastShownPercentageHolder.Percentage >= 5 || percentage == 100)
                    {
                        progressCallback?.Invoke((int)percentage, (int)speed);
                    }
                }
                catch (Exception ex)
                {
                    if (attempt >= DownloadRetryCount)
                    {
                        _logger.LogError($"Failed downloading file {fileName}. {ex}", ex, _telemetryManager);
                        progressCallback?.Invoke(0, 0);
                        transferSpeedHelper.Reset();
                        return null;
                    }

                    await Task.Delay(5000);
                }
            }

            progressCallback?.Invoke(0, 0);
            transferSpeedHelper.Reset();
            _logger.LogInfo($"{fileName} downloaded successfully");
            return fileInfo.FullName;
        }

        protected virtual async Task InitializeGraphForUserAuthAsync()
        {
            AuthenticationRecord? authRecord = await ReadPersistedAuthenticationRecordAsync();
            var deviceCodeCredOptions = new DeviceCodeCredentialOptions
            {
                ClientId = _clientId,
                DeviceCodeCallback = DeviceCodeBallBackPrompt,
                TenantId = "consumers",
                AuthenticationRecord = authRecord,
                TokenCachePersistenceOptions = new TokenCachePersistenceOptions
                {
                    Name = "hassio-onedrive-auth",
                    UnsafeAllowUnencryptedStorage = true
                },                
            };

            _deviceCodeCredential = new DeviceCodeCredential(deviceCodeCredOptions);
            _userClient = new GraphServiceClient(_deviceCodeCredential, _scopes);
        }

        private AuthenticationRecord GetAuthenticationRecordFromCredential(DeviceCodeCredential credential)
        {
            var record = typeof(DeviceCodeCredential)
                .GetProperty("Record", System.Reflection.BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(credential) as AuthenticationRecord;

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
                _logger.LogVerbose("Auth Record not found on disk");
                return null;
            }

            using var authRecordStream = new FileStream(PersistentAuthRecordFullPath, FileMode.Open, FileAccess.Read);
            var record = await AuthenticationRecord.DeserializeAsync(authRecordStream);
            return record;
        }

        private Task DeviceCodeBallBackPrompt(DeviceCodeInfo info, CancellationToken ct)
        {
            IsAuthenticated = false;
            _logger.LogInfo(info.Message);
            (AuthUrl, AuthCode) = ExtractAuthParams(info.Message);
            return Task.FromResult(0);
        }

        private (string url, string code) ExtractAuthParams(string message)
        {
            Match match = Regex.Match(message, "To sign in, use a web browser to open the page ([^ ]*) and enter the code ([\\w]*) to authenticate");
            string url = match.Groups[1].Value;
            string code = match.Groups[2].Value;
            return (url, code);
        }

        private class TransferProgressHolder
        {
            public double Percentage { get; set; } = 0;
        }
    }
}
