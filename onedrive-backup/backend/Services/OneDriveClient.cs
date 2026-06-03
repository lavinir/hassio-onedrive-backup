using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using Microsoft.Kiota.Abstractions;
using System.Reflection;

namespace HassioOneDriveBackup.Services;

public class OneDriveClient : IOneDriveClient
{
    private readonly string _clientId;
    private readonly string[] _scopes = new[] { "Files.ReadWrite.AppFolder", "User.Read" };
    private readonly ILogger<OneDriveClient> _logger;
    private readonly string _authRecordPath;
    private DeviceCodeCredential? _deviceCodeCredential;
    private GraphServiceClient? _graphClient;
    private readonly string _tenantId = "consumers";
    private AuthenticationRecord? _authRecord;
    private const string MsalCacheName = "hassio-onedrive-auth";
    // MsalCacheDirectory is computed at call time, AFTER XDG_DATA_HOME is set in the constructor.
    // With XDG_DATA_HOME=/data, LocalApplicationData returns /data, so the cache lands on the
    // persistent volume at /data/.IdentityService/ instead of a relative or ephemeral path.
    private string MsalCacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ".IdentityService");

    // Simple flag to track if there's an ongoing auth flow
    private bool _authFlowInProgress = false;
    private TaskCompletionSource<(string deviceCode, string verificationUrl, string userCode)>? _currentAuthFlowTcs;
    private DateTime? _lastConnectionTest;
    private OneDriveAuthInfo _lastKnownAuthInfo = new() { AuthState = OneDriveAuthState.NotLoggedIn, UserEmail = null };
    private string? _driveId = null;

    public OneDriveClient(IConfiguration configuration, ILogger<OneDriveClient> logger)
    {
        _clientId = configuration["OneDrive:ClientId"] ?? throw new ArgumentNullException("OneDrive:ClientId configuration is missing");
        _logger = logger;

        var dataFolder = configuration["DataFolder"] ?? "/data";
        _authRecordPath = Path.Combine(dataFolder, "record.auth");

        // Force MSAL's persistent token cache onto the /data volume.
        // On Alpine, SpecialFolder.LocalApplicationData returns "" when XDG_DATA_HOME is unset,
        // causing MSAL to write the cache to a relative path in the ephemeral container layer.
        // Setting XDG_DATA_HOME here makes LocalApplicationData return /data so the cache is
        // written to /data/.IdentityService/ which survives addon upgrades.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_DATA_HOME")))
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataFolder);
        }

        _logger.LogInformation($"HOME={Environment.GetEnvironmentVariable("HOME") ?? "(not set)"}, XDG_DATA_HOME={Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? "(not set)"}, MSAL cache dir: {MsalCacheDirectory}");

        // Load the authentication record if it exists
        LoadAuthenticationRecord();

        // Create the credential and graph client
        InitializeCredential();
    }

    private void LoadAuthenticationRecord()
    {
        try
        {
            if (File.Exists(_authRecordPath))
            {
                _logger.LogInformation("Found authentication record, loading...");
                using Stream recordStream = File.OpenRead(_authRecordPath);
                _authRecord = AuthenticationRecord.Deserialize(recordStream);
                _logger.LogInformation("Authentication record loaded successfully");
            }
            else
            {
                _logger.LogInformation("No authentication record found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load authentication record");
        }
    }

    private void SaveAuthenticationRecord(AuthenticationRecord record)
    {
        try
        {
            using Stream recordStream = new FileStream(_authRecordPath, FileMode.Create, FileAccess.Write);
            record.Serialize(recordStream);
            _logger.LogInformation("Authentication record saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save authentication record");
        }
    }

    private void InitializeCredential()
    {
        try
        {
            var options = new DeviceCodeCredentialOptions
            {
                TokenCachePersistenceOptions = new TokenCachePersistenceOptions
                {
                    Name = MsalCacheName,
                    UnsafeAllowUnencryptedStorage = true
                },
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                TenantId = _tenantId,
                // Prevent automatic device code flows on startup — silent cache-based token refresh still works.
                // Without this, any failed silent refresh blocks for ~15 minutes waiting for a device code to expire.
                DisableAutomaticAuthentication = true
            };

            // Set the authentication record in the options if available
            if (_authRecord != null)
            {
                options.AuthenticationRecord = _authRecord;
                _logger.LogInformation("Using existing authentication record");
            }

            // Create the credential
            _deviceCodeCredential = new DeviceCodeCredential((info, cancel) =>
            {
                _logger.LogDebug("Device code callback invoked during initialization (this should only happen during explicit auth)");
                return Task.CompletedTask;
            }, _tenantId, _clientId, options);

            // Hook up the credential with Graph client
            _graphClient = new GraphServiceClient(_deviceCodeCredential, _scopes);

            var msalCacheFile = Path.Combine(MsalCacheDirectory, $"{MsalCacheName}.cache");
            _logger.LogInformation($"Graph client initialized (MSAL cache file present: {File.Exists(msalCacheFile)}, path: {msalCacheFile})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize credential and graph client");
            _deviceCodeCredential = null;
            _graphClient = null;
        }
    }

    public async Task<GraphServiceClient> GetGraphClientAsync()
    {
        if (_graphClient != null)
        {
            try
            {
                // Test the connection with a lightweight call
                await _graphClient.Me.GetAsync();
                _logger.LogDebug("Graph client is valid");
                return _graphClient;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Existing graph client failed authentication check");
                _graphClient = null;
                _deviceCodeCredential = null;
            }
        }

        throw new UnauthorizedAccessException("Not authenticated with OneDrive. Please log in first.");
    }

    public async Task<OneDriveAuthInfo> IsLoggedInAsync()
    {
        // First check if auth flow is in progress
        if (_authFlowInProgress)
        {
            _logger.LogDebug("IsLoggedInAsync: Auth flow in progress, returning LoggingIn state");
            return new OneDriveAuthInfo { AuthState = OneDriveAuthState.LoggingIn, UserEmail = null };
        }

        // If no client or no auth record, definitely not logged in
        if (_graphClient == null || _authRecord == null)
        {
            return new OneDriveAuthInfo { AuthState = OneDriveAuthState.NotLoggedIn, UserEmail = null };
        }

        // Only test the connection if we're in the regular polling interval (every 30s)
        // or if this is the first call after auth flow completed
        var shouldTestConnection = _lastConnectionTest == null ||
                                 DateTime.UtcNow - _lastConnectionTest > TimeSpan.FromSeconds(25);

        if (!shouldTestConnection)
        {
            // Return the last known state if we tested recently
            return _lastKnownAuthInfo;
        }

        try
        {
            // Test the connection with a lightweight call and get user info
            var user = await _graphClient.Me.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = new string[] { "mail", "userPrincipalName" };
            });

            _logger.LogDebug("IsLoggedInAsync: User is authenticated");
            _lastConnectionTest = DateTime.UtcNow;

            // Get the user's email (prefer mail, fallback to userPrincipalName)
            string? email = user?.Mail ?? user?.UserPrincipalName;

            _lastKnownAuthInfo = new OneDriveAuthInfo
            {
                AuthState = OneDriveAuthState.LoggedIn,
                UserEmail = email
            };

            return _lastKnownAuthInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User is not logged in or token is invalid");
            _graphClient = null;
            _deviceCodeCredential = null;
            _lastConnectionTest = DateTime.UtcNow;
            _lastKnownAuthInfo = new OneDriveAuthInfo { AuthState = OneDriveAuthState.NotLoggedIn, UserEmail = null };
            return _lastKnownAuthInfo;
        }
    }

    public async Task<(string deviceCode, string verificationUrl, string userCode)> InitiateAuthenticationAsync()
    {
        // Check if we already have an authentication flow in progress
        if (_authFlowInProgress && _currentAuthFlowTcs != null)
        {
            _logger.LogInformation("Authentication flow already in progress, reusing existing flow");
            return await _currentAuthFlowTcs.Task;
        }

        _logger.LogInformation("Initiating new authentication flow");

        // Clear any existing credentials and auth record
        _graphClient = null;
        _deviceCodeCredential = null;
        _authRecord = null;

        // Mark that we're starting an auth flow and create a TaskCompletionSource
        _authFlowInProgress = true;
        _currentAuthFlowTcs = new TaskCompletionSource<(string deviceCode, string verificationUrl, string userCode)>();

        try
        {
            // Create options with callback for authentication
            var options = new DeviceCodeCredentialOptions
            {
                TokenCachePersistenceOptions = new TokenCachePersistenceOptions
                {
                    Name = MsalCacheName,
                    UnsafeAllowUnencryptedStorage = true
                },
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                TenantId = _tenantId
            };

            // Create a new DeviceCodeCredential with a callback that captures the device code
            _deviceCodeCredential = new DeviceCodeCredential(
                (info, cancel) =>
                {
                    _logger.LogInformation($"Received device code: {info.UserCode} at {info.VerificationUri}");

                    // Set the result with the device code info
                    _currentAuthFlowTcs!.TrySetResult((info.DeviceCode, info.VerificationUri.ToString(), info.UserCode));

                    return Task.CompletedTask;
                },
                _tenantId,
                _clientId,
                options);

            // Create the Graph client with this credential
            _graphClient = new GraphServiceClient(_deviceCodeCredential, _scopes);

            // Start the authentication flow by making an API call that requires auth in a background task
            _ = Task.Run(async () =>
            {
                try
                {
                    // This will trigger the device code flow
                    var user = await _graphClient.Me.GetAsync();
                    _logger.LogInformation($"Successfully authenticated with OneDrive for user: {user?.DisplayName ?? "Unknown"}");

                    var fieldInfo = typeof(DeviceCodeCredential).GetProperty("Record", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo != null)
                    {
                        var record = fieldInfo.GetValue(_deviceCodeCredential) as AuthenticationRecord;
                        if (record != null)
                        {
                            _authRecord = record;
                            SaveAuthenticationRecord(record);
                            _logger.LogInformation("Successfully retrieved and saved authentication record");
                        }
                    }

                    _authFlowInProgress = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to complete authentication flow");
                    _graphClient = null;
                    _deviceCodeCredential = null;
                    _authFlowInProgress = false;
                    _currentAuthFlowTcs = null;
                }
            });

            // Wait for the device code callback to complete or timeout after 60 seconds
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
            var completedTask = await Task.WhenAny(_currentAuthFlowTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogError("Timed out waiting for device code");
                _authFlowInProgress = false;
                _currentAuthFlowTcs.TrySetCanceled();
                throw new TimeoutException("Timed out waiting for device code");
            }

            // Return the device code info
            return await _currentAuthFlowTcs.Task;
        }
        catch (Exception ex)
        {
            // In case of an error, reset the auth flow state
            _authFlowInProgress = false;

            if (_currentAuthFlowTcs != null && !_currentAuthFlowTcs.Task.IsCompleted)
            {
                _currentAuthFlowTcs.TrySetException(ex);
            }

            _currentAuthFlowTcs = null;

            _logger.LogError(ex, "Failed to initiate device code flow");
            throw new Exception("Failed to initiate device code flow", ex);
        }
    }

    public void Disconnect()
    {
        _logger.LogInformation("Disconnecting from OneDrive");

        // Clear the in-memory references
        _graphClient = null;
        _deviceCodeCredential = null;
        _authRecord = null;
        _authFlowInProgress = false;
        _currentAuthFlowTcs = null;

        // Delete the authentication record file
        try
        {
            if (File.Exists(_authRecordPath))
            {
                File.Delete(_authRecordPath);
                _logger.LogInformation("Deleted authentication record file");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete authentication record file");
        }

        // Delete all MSAL cache files from the correct location
        try
        {
            if (Directory.Exists(MsalCacheDirectory))
            {
                foreach (var file in Directory.GetFiles(MsalCacheDirectory, "*.cache"))
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogDebug($"Deleted MSAL cache file: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to delete MSAL cache file: {file}");
                    }
                }
            }

            _logger.LogInformation("Token cache files deleted");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up token cache files");
        }

        _logger.LogInformation("Successfully disconnected from OneDrive");
    }

    public async Task<DriveItem> UploadFileAsync(string localFilePath, string oneDrivePath, ProgressCallback? progressCallback = null, string? description = null)
    {
        var client = await GetGraphClientAsync();
        var driveId = await GetDriveIdFromAppFolder();

        using var fileStream = File.OpenRead(localFilePath);
        var fileSize = new FileInfo(localFilePath).Length;

        // Normalize the path to use forward slashes
        oneDrivePath = oneDrivePath.Replace('\\', '/').TrimStart('/');

        // For files larger than 4MB, use large file upload session
        if (fileSize > 4 * 1024 * 1024)
        {
            _logger.LogInformation($"Using large file upload session for {oneDrivePath} ({fileSize} bytes)");

            var uploadSessionRequestBody = new CreateUploadSessionPostRequestBody
            {
                Item = new DriveItemUploadableProperties
                {
                    Name = Path.GetFileName(oneDrivePath),
                    Description = description,
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "@microsoft.graph.conflictBehavior", "replace" }
                    }
                }
            };

            // Use the app folder with the drive ID
            var appFolder = await client.Drives[driveId].Special["approot"].GetAsync();
            var uploadSession = await client.Drives[driveId].Items[appFolder.Id].ItemWithPath(oneDrivePath).CreateUploadSession.PostAsync(uploadSessionRequestBody);

            if (uploadSession == null)
            {
                throw new Exception($"Failed to create upload session for {oneDrivePath}");
            }

            // Create upload task
            var maxSliceSize = 320 * 1024; // 320 KB chunk size
            var largeFileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSliceSize);

            // Track progress using Progress<T>
            var uploadedBytes = 0L;
            var progress = new Progress<long>(bytes =>
            {
                uploadedBytes = bytes;
                progressCallback?.Invoke(uploadedBytes, fileSize);
            });

            var uploadResult = await largeFileUploadTask.UploadAsync(progress);

            if (!uploadResult.UploadSucceeded)
            {
                throw new Exception($"Failed to upload file {oneDrivePath}");
            }

            return uploadResult.ItemResponse;
        }
        else
        {
            _logger.LogInformation($"Using simple upload for {oneDrivePath} ({fileSize} bytes)");

            // For small files, wrap the stream to track progress
            var progressStream = new ProgressStream(fileStream, progress =>
            {
                progressCallback?.Invoke(progress, fileSize);
            });

            // Use the app folder with the drive ID
            var appFolder = await client.Drives[driveId].Special["approot"].GetAsync();
            var result = await client.Drives[driveId].Items[appFolder.Id].ItemWithPath(oneDrivePath).Content.PutAsync(progressStream);
            if (result == null)
            {
                throw new Exception($"Failed to upload file {oneDrivePath}");
            }

            if (description != null && result.Id != null)
            {
                await client.Drives[driveId].Items[result.Id].PatchAsync(new Microsoft.Graph.Models.DriveItem { Description = description });
            }

            return result;
        }
    }

    public async Task DownloadFileAsync(string oneDrivePath, string localFilePath, ProgressCallback? progressCallback = null)
    {
        var client = await GetGraphClientAsync();
        var driveId = await GetDriveIdFromAppFolder();

        try
        {
            // Normalize the path
            oneDrivePath = oneDrivePath.Replace('\\', '/').TrimStart('/');

            // Ensure the local directory exists
            var localDir = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            // Get the app folder to use as base for all operations
            var appFolder = await client.Drives[driveId].Special["approot"].GetAsync();

            // Get the file size first for progress reporting
            var item = await client.Drives[driveId].Items[appFolder.Id].ItemWithPath(oneDrivePath).GetAsync();
            var totalSize = item?.Size;

            var stream = await client.Drives[driveId].Items[appFolder.Id].ItemWithPath(oneDrivePath).Content.GetAsync();

            if (stream == null)
            {
                throw new Exception($"Failed to get content stream for file {oneDrivePath}");
            }

            try
            {
                using var fileStream = File.Create(localFilePath);

                // Use buffer for efficient copying
                var buffer = new byte[81920];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    progressCallback?.Invoke(totalBytesRead, totalSize);
                }
            }
            catch
            {
                if (File.Exists(localFilePath))
                    File.Delete(localFilePath);
                throw;
            }

            _logger.LogInformation($"Successfully downloaded {oneDrivePath} to {localFilePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to download file {oneDrivePath}");
            throw;
        }
    }

    // Helper class for progress tracking on small file uploads
    private class ProgressStream : Stream
    {
        private readonly Stream _inner;
        private readonly Action<long> _progress;
        private long _position;

        public ProgressStream(Stream inner, Action<long> progress)
        {
            _inner = inner;
            _progress = progress;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _inner.Read(buffer, offset, count);
            _position += bytesRead;
            _progress(_position);
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var pos = _inner.Seek(offset, origin);
            _position = pos;
            return pos;
        }

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            _position += count;
            _progress(_position);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public async Task<string> GetDriveIdFromAppFolder()
    {        
        try
        {
            if (_driveId != null)
            {
                return _driveId;
            }

            var resp = await _graphClient.Drives.WithUrl("https://graph.microsoft.com/v1.0/me/drive/special/approot").GetAsync();
            if (resp?.AdditionalData == null || !resp.AdditionalData.TryGetValue("id", out var rawId) || rawId == null)
                throw new InvalidOperationException("Drive ID missing from app root response");
            _driveId = rawId.ToString()!.Split("!").First();
            return _driveId;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed getting Drive Id", ex);
            throw;
        }
    }

    public async Task ResetConnectionAsync()
    {
        Disconnect(); // This handles clearing the token cache and auth records
        InitializeCredential(); // Reinitialize with fresh credentials
        await Task.CompletedTask;
    }

    public async Task<IList<DriveItem>> ListFilesInDirectoryAsync(string oneDriveDirectoryPath)
    {
        var client = await GetGraphClientAsync();
        var driveId = await GetDriveIdFromAppFolder();

        // Normalize the path to use forward slashes and trim leading slashes
        oneDriveDirectoryPath = oneDriveDirectoryPath.Replace('\\', '/').TrimStart('/');

        // Get the app folder as the base
        var appFolder = await client.Drives[driveId].Special["approot"].GetAsync();
        if (appFolder == null)
        {
            throw new Exception("Failed to get app folder root");
        }

        // If the path is empty, list the root of the app folder
        var itemRequest = string.IsNullOrEmpty(oneDriveDirectoryPath)
            ? client.Drives[driveId].Items[appFolder.Id].Children
            : client.Drives[driveId].Items[appFolder.Id].ItemWithPath(oneDriveDirectoryPath).Children;

        var allItems = new List<DriveItem>();
        var page = await itemRequest.GetAsync();
        if (page?.Value != null)
            allItems.AddRange(page.Value);

        // Handle paging using @odata.nextLink
        var nextLink = page?.OdataNextLink;
        while (!string.IsNullOrEmpty(nextLink))
        {
            var nextPage = await client.RequestAdapter.SendAsync(
                new RequestInformation
                {
                    HttpMethod = Method.GET,
                    UrlTemplate = nextLink,
                    PathParameters = new Dictionary<string, object>()
                },
                DriveItemCollectionResponse.CreateFromDiscriminatorValue,
                null
            );
            if (nextPage?.Value != null)
                allItems.AddRange(nextPage.Value);
            nextLink = nextPage?.OdataNextLink;
        }

        return allItems;
    }

    public async Task<DriveItem?> GetFileAsync(string oneDrivePath)
    {
        var client = await GetGraphClientAsync();
        var driveId = await GetDriveIdFromAppFolder();
        oneDrivePath = oneDrivePath.Replace('\\', '/').TrimStart('/');

        try
        {
            var appFolder = await client.Drives[driveId].Special["approot"].GetAsync();
            if (appFolder == null) return null;
            return await client.Drives[driveId].Items[appFolder.Id].ItemWithPath(oneDrivePath).GetAsync();
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }
    }

    public async Task DeleteFileAsync(string oneDrivePath)
    {
        var client = await GetGraphClientAsync();
        var driveId = await GetDriveIdFromAppFolder();
        oneDrivePath = oneDrivePath.Replace('\\', '/').TrimStart('/');

        var appFolder = await client.Drives[driveId].Special["approot"].GetAsync();
        if (appFolder == null) throw new InvalidOperationException("Failed to get app folder root");

        await client.Drives[driveId].Items[appFolder.Id].ItemWithPath(oneDrivePath).DeleteAsync();
    }
}