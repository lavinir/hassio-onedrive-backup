using System.Diagnostics;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class HassioClientMock : IHassioClient
    {
        private List<Backup> _backups = new List<Backup>();

        public Task<bool> CreateBackupAsync(string backupName, bool compressed = true, string? password = null)
        {
            var backup = new Backup
            {
                Compressed = compressed,
                Name = backupName,
                Protected = !string.IsNullOrEmpty(password),
                Type = "Full",
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

        public Task<List<Backup>> GetBackupsAsync(Predicate<Backup> filter)
        {
            return Task.FromResult(_backups);
        }

        public Task SendPersistentNotificationAsync(string message)
        {
            Debug.WriteLine(message);
            return Task.CompletedTask;
        }
    }
}
