using Microsoft.Graph;
using Microsoft.Graph.Models;

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

public delegate void ProgressCallback(long bytesTransferred, long? totalBytes);

public interface IOneDriveClient
{
    Task<OneDriveAuthInfo> IsLoggedInAsync();
    Task<(string deviceCode, string verificationUrl, string userCode)> InitiateAuthenticationAsync();
    void Disconnect();
    Task<GraphServiceClient> GetGraphClientAsync();
    Task ResetConnectionAsync();
    
    // Upload a file to OneDrive App Folder with progress reporting
    Task<DriveItem> UploadFileAsync(string localFilePath, string oneDrivePath, ProgressCallback? progressCallback = null, string? description = null);
    
    // Download a file from OneDrive App Folder with progress reporting
    Task DownloadFileAsync(string oneDrivePath, string localFilePath, ProgressCallback? progressCallback = null);
    
    // Enumerate all files in a directory in OneDrive App Folder
    Task<IList<DriveItem>> ListFilesInDirectoryAsync(string oneDriveDirectoryPath);

    // Get a single file/folder item — returns null if not found
    Task<DriveItem?> GetFileAsync(string oneDrivePath);

    // Delete a file or folder at the given path
    Task DeleteFileAsync(string oneDrivePath);
}