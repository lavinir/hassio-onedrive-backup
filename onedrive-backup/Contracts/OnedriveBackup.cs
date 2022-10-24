namespace hassio_onedrive_backup.Contracts
{
    internal class OnedriveBackup
    {
        public OnedriveBackup(string slug, string fileName)
        {
            Slug = slug ?? String.Empty;
            FileName = fileName;
        }

        public string Slug { get; }

        public string FileName { get; }
    }
}
