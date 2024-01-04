using Microsoft.Graph.Models;
using onedrive_backup.Contracts;
using onedrive_backup.Graph;

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

        Task<bool> UploadFileAsync(string filePath, DateTime date, string? instanceName, TransferSpeedHelper transferSpeedHelper, string? destinationFileName = null, Action<int, int>? progressCallback = null, bool flatten = true, string description = null);

        Task<string?> DownloadFileAsync(string fileName, Action<int?>? progressCallback);

        Task<OneDriveFreeSpaceData> GetFreeSpaceInGB();
    }

    public delegate void AuthStatusChanged();
}