using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

namespace HassioOneDriveBackup.Services;

public class OneDriveAuthService : IOneDriveAuthService
{
    private readonly IPublicClientApplication _msalClient;
    private readonly string[] _scopes = new[] { "Files.ReadWrite.AppFolder" };
    private string? _cachedToken;
    private readonly ILogger<OneDriveAuthService> _logger;
    private readonly MsalTokenCache _tokenCache;
    private GraphServiceClient? _graphClient;

    public OneDriveAuthService(IConfiguration configuration, ILogger<OneDriveAuthService> logger, 
        ILoggerFactory loggerFactory)
    {
        var clientId = configuration["OneDrive:ClientId"];
        _logger = logger;

        // Create and initialize the token cache
        var cacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HassioOneDriveBackup",
            "msal_cache.dat"
        );
        _tokenCache = new MsalTokenCache(cacheFilePath, loggerFactory.CreateLogger<MsalTokenCache>());

        // Create MSAL client with persistent cache
        _msalClient = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount, true)
            .Build();

        // Initialize the token cache
        _tokenCache.Initialize(_msalClient.UserTokenCache);

        // Try to silently acquire token on startup
        _ = RefreshTokenAsync();
    }

    private GraphServiceClient CreateGraphClient(string accessToken)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenProvider(accessToken));
        return new GraphServiceClient(authProvider);
    }

    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            if (!accounts.Any())
                return false;

            // Verify we can get a valid token and test the connection
            var result = await _msalClient.AcquireTokenSilent(_scopes, accounts.First())
                .ExecuteAsync();
            
            _cachedToken = result.AccessToken;
            // _graphClient = CreateGraphClient(result.AccessToken);
            
            // // Test the connection
            // await _graphClient.Me.Drive.GetAsync();
            
            return true;
        }
        catch
        {
            _cachedToken = null;
            _graphClient = null;
            return false;
        }
    }

    private async Task RefreshTokenAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            if (accounts.Any())
            {
                var result = await _msalClient.AcquireTokenSilent(_scopes, accounts.First())
                    .ExecuteAsync();
                
                _cachedToken = result.AccessToken;
                _graphClient = CreateGraphClient(result.AccessToken);
                
                // Test the connection
                await _graphClient.Me.Drive.GetAsync();
                
                _logger.LogInformation("Successfully refreshed OneDrive authentication token");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh OneDrive authentication token");
            _cachedToken = null;
            _graphClient = null;
        }
    }

    public async Task<(string deviceCode, string verificationUrl, string userCode)> InitiateAuthenticationAsync()
    {
        try
        {
            var deviceCodeTcs = new TaskCompletionSource<DeviceCodeResult>();

            // Start the device code flow but don't await the full authentication
            var result = _msalClient.AcquireTokenWithDeviceCode(
                _scopes,
                deviceCodeResult =>
                {
                    deviceCodeTcs.SetResult(deviceCodeResult);
                    return Task.CompletedTask;
                }
            );

            // Start the authentication process in the background
            _ = result.ExecuteAsync().ContinueWith(async task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    _cachedToken = task.Result.AccessToken;
                    _graphClient = CreateGraphClient(_cachedToken);
                    
                    // Test the connection
                    await _graphClient.Me.Drive.GetAsync();
                    
                    _logger.LogInformation("Successfully authenticated with OneDrive");
                }
                else if (task.Exception != null)
                {
                    _logger.LogError(task.Exception, "Failed to complete OneDrive authentication");
                    _cachedToken = null;
                    _graphClient = null;
                }
            });

            // Wait for the device code callback to complete
            var deviceCodeInfo = await deviceCodeTcs.Task;
            
            return (deviceCodeInfo.DeviceCode,
                    deviceCodeInfo.VerificationUrl,
                    deviceCodeInfo.UserCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate device code flow");
            throw new Exception("Failed to initiate device code flow", ex);
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _msalClient.RemoveAsync(account);
            }
            _cachedToken = null;
            _graphClient = null;
            _logger.LogInformation("Successfully disconnected from OneDrive");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from OneDrive");
            throw;
        }
    }

    private class TokenProvider : IAccessTokenProvider
    {
        private readonly string _token;

        public TokenProvider(string token)
        {
            _token = token;
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public Task<string> GetAuthorizationTokenAsync(
            Uri uri, 
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_token);
        }
    }
}