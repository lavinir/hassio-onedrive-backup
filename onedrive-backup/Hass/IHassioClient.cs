using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal interface IHassioClient
    {
        Task<List<Backup>> GetBackupsAsync(Predicate<Backup> filter);

        Task SendPersistentNotificationAsync(string message);

        Task<bool> CreateBackupAsync(string backupName, bool appendTimestamp = true, bool compressed = true, string? password = null, IEnumerable<string>? folders = null, IEnumerable<string>? addons = null);

        Task<bool> DeleteBackupAsync(Backup backup);

        Task UpdateHassEntityState(string entityId, string payload);

        Task<string> DownloadBackup(string backupSlug);

        Task<bool> UploadBackupAsync(string filePath);

        Task<List<string>> GetAddons();
    }
}