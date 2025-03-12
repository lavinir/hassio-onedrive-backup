using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services;

public interface IBackupService
{
    Task<IEnumerable<Backup>> GetBackupsAsync();
    Task<string> UploadBackupAsync(string slugId);
    Task<string> DownloadBackupAsync(string slugId);
    Task DeleteBackupAsync(string slugId);
    Task<Backup> TriggerBackupAsync();
    Task<Backup> UpdateBackupRetentionAsync(string slugId, bool retain);
    Task<double> GetTransferProgressAsync(string operationId);
}