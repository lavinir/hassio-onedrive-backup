using Microsoft.Graph;

namespace HassioOneDriveBackup.Services;

public interface IOneDriveAuthService
{
    Task<bool> IsLoggedInAsync();
    Task<(string deviceCode, string verificationUrl, string userCode)> InitiateAuthenticationAsync();
    void Disconnect();
    Task<GraphServiceClient> GetGraphClientAsync();
}