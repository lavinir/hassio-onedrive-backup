using Microsoft.Graph.Models;

namespace onedrive_backup.Contracts
{
	public class BackupAdditionalData
	{
        public BackupAdditionalData()
        {
            Backups = new();
        }

        public List<BackupData> Backups { get; set; }

        public bool IsRetainedLocally(string slug)
        {
            var backup = Backups.FirstOrDefault(backupData => backupData.Slug == slug);
            return (backup != null && backup.RetainLocal);
        }

        public bool IsRetainedOneDrive(string slug)
        {
            var backup = Backups.FirstOrDefault(backupData => backupData.Slug == slug);
            return (backup != null && backup.RetainOneDrive);
        }

        public int PruneAdditionalBackupData(params string[] slugs)
        {
            return Backups.RemoveAll(backup => slugs.Contains(backup.Slug));
        }

        public void UpdateRetainLocally(string slug, bool retain)
        {
            var backup = GetOrCreateBackupData(slug);
            backup.RetainLocal = retain;
        }

        public void UpdateRetainOneDrive(string slug, bool retain)
        {
            var backup = GetOrCreateBackupData(slug);
            backup.RetainOneDrive = retain;
        }

        private BackupData GetOrCreateBackupData(string slug)
        {
            var backup = Backups.FirstOrDefault(backupData => backupData.Slug == slug);
            if (backup == null)
            {
                backup = new BackupData
                {
                    Slug = slug
                };

                Backups.Add(backup);
            }

            return backup;
        }

        public class BackupData
		{
            public string Slug { get; set; }

            public bool RetainLocal { get; set; }

            public bool RetainOneDrive { get; set; }
        }
    }
}
