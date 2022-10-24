using Microsoft.Graph;

namespace hassio_onedrive_backup.Graph
{
    internal interface IGraphHelper
    {
        Task<string> GetAndCacheUserTokenAsync();

        Task<List<DriveItem>> GetItemsInAppFolderAsync();

        Task<bool> DeleteFileFromAppFolderAsync(string filePath);

        Task<bool> UploadFileAsync(string filePath, DateTime date, string? destinationFileName = null);
    }
}