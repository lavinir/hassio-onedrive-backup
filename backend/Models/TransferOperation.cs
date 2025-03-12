namespace HassioOneDriveBackup.Models;

public class TransferOperation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BackupSlug { get; set; } = string.Empty;
    public TransferType Type { get; set; }
    public int Progress { get; set; }
    public TransferStatus Status { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
}

public enum TransferType
{
    Upload,
    Download
}

public enum TransferStatus
{
    InProgress,
    Completed,
    Failed
}