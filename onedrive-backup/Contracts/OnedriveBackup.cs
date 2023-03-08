namespace hassio_onedrive_backup.Contracts
{
    public class OnedriveBackup
    {
        public OnedriveBackup(string fileName, OnedriveItemDescription itemDescription)
        {
            FileName = fileName;
            Slug = itemDescription.Slug;
            BackupDate = itemDescription.BackupDate;
            InstanceName = itemDescription.InstanceName;
            Type = itemDescription.BackupType;
            IsProtected = itemDescription.IsProtected;
            Size = itemDescription.Size;
        }

        public string Slug { get; }

        public string FileName { get; }

        public DateTime BackupDate { get; set; }

        public string? InstanceName { get; set; }

        public string Type { get; set; }

        public bool IsProtected { get; set; }

        public float Size { get; set; }

    }
}
