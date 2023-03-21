using Microsoft.Graph;
using onedrive_backup.Contracts;

namespace hassio_onedrive_backup.Graph
{
    public interface IGraphHelper
    {
        string AuthUrl { get; }

        string AuthCode { get; }

        bool? IsAuthenticated { get; }

        event AuthStatusChanged AuthStatusChangedEventHandler;

        Task<string> GetAndCacheUserTokenAsync();

        Task<List<DriveItem>> GetItemsInAppFolderAsync(string subPath = "");

        Task<DriveItem?> GetItemInAppFolderAsync(string subPath = "");

        Task<bool> DeleteItemFromAppFolderAsync(string itemPath);

        Task<bool> UploadFileAsync(string filePath, DateTime date, string? instanceName, string? destinationFileName = null, Action<int>? progressCallback = null, bool flatten = true, string description = null);

        Task<string?> DownloadFileAsync(string fileName, Action<int?>? progressCallback);

        Task<OneDriveFreeSpaceData> GetFreeSpaceInGB();

        Task<DriveItem> GetOrCreateFolder(string path);
    }

    public delegate void AuthStatusChanged();
}