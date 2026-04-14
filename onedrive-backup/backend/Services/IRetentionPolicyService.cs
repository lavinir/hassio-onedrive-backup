using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services;

public interface IRetentionPolicyService
{
    /// <summary>
    /// Returns the backups that should be deleted based on the configured retention policy.
    /// Pinned (Retained=true) backups are never returned.
    /// </summary>
    /// <param name="maxCount">Maximum number of backups to keep. Caller passes MaxLocalBackups or MaxOnedriveBackups as appropriate.</param>
    IEnumerable<Backup> GetBackupsToDelete(IEnumerable<Backup> backups, BackupSettings settings, int maxCount);
}
