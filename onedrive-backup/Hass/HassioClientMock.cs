using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class HassioClientMock : IHassioClient
    {
        private List<Backup> _backups = new List<Backup>();

        public Task<bool> CreateBackupAsync(string backupName, bool appendTimestamp = true, bool compressed = true, string? password = null, IEnumerable<string>? folders = null, IEnumerable<string>? addons = null)
        {
            DateTime timeStamp = DateTimeHelper.Instance!.Now;
            string finalBackupName = appendTimestamp ? $"{backupName}_{timeStamp.ToString("yyyy-MM-dd-HH-mm")}" : backupName;
            var backup = new Backup
            {
                Compressed = compressed,
                Name = finalBackupName,
                Protected = !string.IsNullOrEmpty(password),
                Type = folders == null && addons == null ? "Full" : "Partial",
                Date = DateTime.Now,
                Slug = Guid.NewGuid().ToString()
            };

            _backups.Add(backup);
            return Task.FromResult(true);
        }

        public Task<bool> DeleteBackupAsync(Backup backup)
        {
            var backupToRemove = _backups.SingleOrDefault(bck => bck.Slug.Equals(backup.Slug, StringComparison.OrdinalIgnoreCase));
            if (backupToRemove != null)
            {
                _backups.Remove(backupToRemove);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<string> DownloadBackupAsync(string backupSlug)
        {
            string backupFile = $"./mockBackup_{Guid.NewGuid()}.tar";
            
            // 100MB File
            byte[] data = new byte[100 * 1024 * 1024];
            new Random().NextBytes(data);
            File.WriteAllBytes(backupFile, data); 
            return Task.FromResult(backupFile);
        }

        public Task<List<string>> GetAddonsAsync()
        {
            return Task.FromResult(new List<string>()
            {
                "Addon1",
                "Addon2"
            });
        }

        public Task<List<Backup>> GetBackupsAsync(Predicate<Backup> filter)
        {
            return Task.FromResult(_backups);
        }

        public Task<string> GetTimeZoneAsync()
        {
            return Task.FromResult("Local");
        }

        public Task SendPersistentNotificationAsync(string message)
        {
            var payload = new
            {
                message = message,
                title = "hassio-onedrive-backup"
            };

            string payloadStr = JsonConvert.SerializeObject(payload);
            Debug.WriteLine(payloadStr);
            return Task.CompletedTask;
        }

        public Task UpdateHassEntityStateAsync(string entityId, string payload)
        {
            Debug.WriteLine($"EntityId: {entityId}. State: {payload}");
            return Task.CompletedTask;
        }

        public Task<bool> UploadBackupAsync(string filePath)
        {
            _backups.Add(new Backup
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Slug = Guid.NewGuid().ToString()
            });

            return Task.FromResult(true);
        }
    }
}
