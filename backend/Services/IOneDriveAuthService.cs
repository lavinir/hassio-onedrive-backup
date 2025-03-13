using Microsoft.Graph;

namespace HassioOneDriveBackup.Services;

public enum OneDriveAuthState
{
    NotLoggedIn,
    LoggingIn,
    LoggedIn
}

public class OneDriveAuthInfo
{
    public OneDriveAuthState AuthState { get; set; }
    public string? UserEmail { get; set; }
}

public interface IOneDriveAuthService
{
    Task<OneDriveAuthInfo> IsLoggedInAsync();
    Task<(string deviceCode, string verificationUrl, string userCode)> InitiateAuthenticationAsync();
    void Disconnect();
    Task<GraphServiceClient> GetGraphClientAsync();
}