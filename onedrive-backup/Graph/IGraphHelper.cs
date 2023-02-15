using Microsoft.Graph;

namespace hassio_onedrive_backup.Graph
{
    public interface IGraphHelper
    {
        Task<string> GetAndCacheUserTokenAsync();

        Task<List<DriveItem>> GetItemsInAppFolderAsync(string subPath = "");

        Task<DriveItem?> GetItemInAppFolderAsync(string subPath = "");

        Task<bool> DeleteItemFromAppFolderAsync(string itemPath);

        Task<bool> UploadFileAsync(string filePath, DateTime date, string? instanceName, string? destinationFileName = null, Action<int>? progressCallback = null, bool flatten = true, bool omitDescription = false);

        Task<string?> DownloadFileAsync(string fileName, Action<int?>? progressCallback);

        Task<double?> GetFreeSpaceInGB();

        Task<DriveItem> GetOrCreateFolder(string path);
    }
}