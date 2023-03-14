
using static hassio_onedrive_backup.Contracts.HassAddonsResponse;

namespace onedrive_backup.Models
{
    public class BackupModel
    {
        public string Name { get; set; }

        public string Slug { get; set; }

        public DateTime Date { get; set; }

        public BackupLocation Location { get; set; }

        public string OneDriveFileName { get; set; }

        public string Type { get; set; }

        public bool IsProtected { get; set; }

        public float Size { get; set; }

        public IEnumerable<Addon> Addons { get; set; }

        public IEnumerable<string> Folders { get; set; }

        public string DisplayName => string.IsNullOrEmpty(Name) ? Path.GetFileNameWithoutExtension(OneDriveFileName) : Name;

        public enum BackupLocation
        {
            Local,
            OneDrive,
            Both
        }
    }
}
