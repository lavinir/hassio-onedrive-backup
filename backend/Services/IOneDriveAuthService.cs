using Microsoft.Graph;

namespace HassioOneDriveBackup.Services;

public enum OneDriveAuthState
{
    NotLoggedIn,
    LoggingIn,
    LoggedIn
}

public interface IOneDriveAuthService
{
    Task<OneDriveAuthState> IsLoggedInAsync();
    Task<(string deviceCode, string verificationUrl, string userCode)> InitiateAuthenticationAsync();
    void Disconnect();
    Task<GraphServiceClient> GetGraphClientAsync();
}