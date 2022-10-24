namespace hassio_onedrive_backup.Contracts
{
    internal class OnedriveBackup
    {
        public OnedriveBackup(string fileName, OnedriveItemDescription itemDescription)
        {
            FileName = fileName;
            Slug = itemDescription.Slug;
            BackupDate = itemDescription.BackupDate;
        }

        public string Slug { get; }

        public string FileName { get; }

        public DateTime BackupDate { get; set; }
    }
}
