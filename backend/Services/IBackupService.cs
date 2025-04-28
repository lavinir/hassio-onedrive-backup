using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services;

public interface IBackupService
{
    Task<IEnumerable<Backup>> GetBackupsAsync();
    Task<string> UploadBackupAsync(string slugId);
    Task<string> DownloadBackupAsync(string slugId);
    Task DeleteBackupAsync(string slugId);
    Task<Backup> TriggerBackupAsync(string name);
    Task<Backup> UpdateBackupRetentionAsync(string slugId, bool retain);
    Task<TransferOperation> GetTransferProgressAsync(string operationId);
}

public class TransferProgress
{
    public long BytesTransferred { get; set; }
    public long? TotalBytes { get; set; }
    public double ProgressPercentage => TotalBytes.HasValue ? (double)BytesTransferred / TotalBytes.Value * 100 : 0;
    public string Status => TotalBytes.HasValue ? $"{BytesTransferred}/{TotalBytes} bytes ({ProgressPercentage:F1}%)" : $"{BytesTransferred} bytes";
}