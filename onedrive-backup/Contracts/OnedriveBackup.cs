using onedrive_backup.Contracts;

namespace hassio_onedrive_backup.Contracts
{
    public class OnedriveBackup : IBackup
    {
        public OnedriveBackup()
        {

        }

        public OnedriveBackup(string fileName, OnedriveItemDescription itemDescription)
        {
            FileName = fileName;
            Slug = itemDescription.Slug;
            BackupDate = itemDescription.BackupDate;
            InstanceName = itemDescription.InstanceName;
            Type = itemDescription.BackupType;
            IsProtected = itemDescription.IsProtected;
            Size = itemDescription.Size;
            Addons = itemDescription.Addons;
            Folders = itemDescription.Folders;
        }

        public OnedriveBackup(
          string slug,
          string fileName,
          DateTime backupDate,
          string? instanceName,
          string type,
          bool isProtected,
          float size,
          IEnumerable<string> addons,
          IEnumerable<string> folders)
        {
            Slug = slug;
            FileName = fileName;
            BackupDate = backupDate;
            InstanceName = instanceName;
            Type = type;
            IsProtected = isProtected;
            Size = size;
            Addons = addons;
            Folders = folders;
        }

        public string Slug { get; set; }

        public string FileName { get; set; }

        public DateTime BackupDate { get; set; }

        public string? InstanceName { get; set; }

        public string Type { get; set; }

        public bool IsProtected { get; set; }

        public float Size { get; set; }

        public IEnumerable<string> Addons { get; set; }

        public IEnumerable<string> Folders { get; set; }

        public BackupTypeDisplayName TypeDisplayName => BackupTypeDisplayName.Online;
    }
}
