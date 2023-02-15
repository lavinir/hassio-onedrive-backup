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
        }

        public string Slug { get; }

        public string FileName { get; }

        public DateTime BackupDate { get; set; }

        public string? InstanceName { get; set; }
    }
}
