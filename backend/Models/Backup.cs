namespace HassioOneDriveBackup.Models;

public class Backup
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string BackupType { get; set; } = string.Empty;
    public string? Path { get; set; }
    public bool Retained { get; set; }
}

public class BackupTransferOperation
{
    public string OperationId { get; set; } = string.Empty;
    public string BackupId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string Type { get; set; } = string.Empty;
    public double Progress { get; set; }
}