namespace onedrive_backup.Models
{
    public class BackupModel
    {
        public string Name { get; set; }

        public bool IsPartial { get; set; }

        public bool OnlineOnly { get; set; }
    }
}
