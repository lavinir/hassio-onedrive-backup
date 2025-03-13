using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Reflection;

namespace HassioOneDriveBackup.Services;

public class OneDriveAuthService : IOneDriveAuthService
{
    private readonly string _clientId;
    private readonly string[] _scopes = new[] { "Files.ReadWrite.AppFolder", "User.Read" };
    private readonly ILogger<OneDriveAuthService> _logger;
    private readonly string _tokenCachePath;
    private DeviceCodeCredential? _deviceCodeCredential;
    private GraphServiceClient? _graphClient;
    private readonly string _tenantId = "consumers";
    private string _authRecordPath => Path.Combine(_tokenCachePath, "auth_record.json");
    private AuthenticationRecord? _authRecord;
    
    // Simple flag to track if there's an ongoing auth flow
    private bool _authFlowInProgress = false;
    private TaskCompletionSource<(string deviceCode, string verificationUrl, string userCode)>? _currentAuthFlowTcs;
    
    public OneDriveAuthService(IConfiguration configuration, ILogger<OneDriveAuthService> logger)
    {
        _clientId = configuration["OneDrive:ClientId"] ?? throw new ArgumentNullException("OneDrive:ClientId configuration is missing");
        _logger = logger;

        // Set up the token cache location
        _tokenCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HassioOneDriveBackup"
        );

        _logger.LogInformation($"Token cache path: {_tokenCachePath}");

        // Ensure the directory exists
        try
        {
            Directory.CreateDirectory(_tokenCachePath);
            _logger.LogInformation("Token cache directory created or verified");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to create token cache directory: {_tokenCachePath}");
        }

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
                _logger.LogInformation("No authentication record found at: " + _authRecordPath);
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
            using Stream recordStream = File.OpenWrite(_authRecordPath);
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
                    Name = "HassioOneDriveBackup",
                    UnsafeAllowUnencryptedStorage = true
                },
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                TenantId = _tenantId,
                // Only prompt when explicitly requested
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
            
            _logger.LogInformation("Graph client initialized");
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

    public async Task<bool> IsLoggedInAsync()
    {
        // Don't try to authenticate if an auth flow is already in progress
        if (_authFlowInProgress)
        {
            _logger.LogDebug("IsLoggedInAsync: Auth flow in progress, returning false");
            return false;
        }
        
        if (_graphClient == null)
        {
            return false;
        }

        try
        {
            // Test the connection with a lightweight call
            await _graphClient.Me.GetAsync();
            _logger.LogDebug("IsLoggedInAsync: User is authenticated");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User is not logged in or token is invalid");
            _graphClient = null;
            _deviceCodeCredential = null;
            return false;
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
                    Name = "HassioOneDriveBackup",
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

        // Delete all MSAL cache files in the directory
        try
        {
            var cacheDir = new DirectoryInfo(_tokenCachePath);
            foreach (var file in cacheDir.GetFiles("msal.cache*"))
            {
                try
                {
                    file.Delete();
                    _logger.LogDebug($"Deleted MSAL cache file: {file.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete MSAL cache file: {file.FullName}");
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
}