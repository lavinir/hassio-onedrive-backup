using hassio_onedrive_backup.Hass.Events;
using onedrive_backup.Contracts;
using System.Security.Cryptography;
using static hassio_onedrive_backup.Contracts.HassAddonsResponse;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    public interface IHassioClient
    {
        Task<List<Backup>> GetBackupsAsync(Predicate<Backup> filter);

        Task SendPersistentNotificationAsync(string message);

        Task<bool> CreateBackupAsync(string backupName, DateTime timeStamp, bool appendTimestamp = true, bool compressed = true, string? password = null, IEnumerable<string>? folders = null, IEnumerable<string>? addons = null);

        Task<bool> DeleteBackupAsync(Backup backup);

        Task UpdateHassEntityStateAsync(string entityId, string payload);

        Task<string> DownloadBackupAsync(string backupSlug);

        Task<bool> UploadBackupAsync(string filePath);

        Task<List<Addon>> GetAddonsAsync();

        Task<HassAddonInfoResponse> GetAddonInfo(string slug);

        Task<string> GetTimeZoneAsync();

        Task PublishEventAsync(OneDriveEvents eventType, string payload = "");
		
        Task RestartSelf();

        void UpdateTimeoutValue(int timeoutMinutes);
	}
}