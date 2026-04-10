using Newtonsoft.Json;

namespace HassioOneDriveBackup.Models;

public class Backup
{
    [JsonProperty("slug")]
    public string Slug { get; set; }

    [JsonProperty("date")]
    public DateTime Date { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("size")]
    public string Size { get; set; }

    [JsonProperty("backup_type")]
    public string BackupType { get; set; }

    [JsonProperty("protected")]
    public bool Protected { get; set; }

    [JsonProperty("compressed")]
    public bool Compressed { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("source_type")]
    public string SourceType { get; set; } = string.Empty;

    [JsonProperty("local_path")]
    public string? LocalPath { get; set; }

    [JsonProperty("retained")]
    public bool Retained { get; set; }
}

public class BackupTransferOperation
{
    [JsonProperty("operationId")]
    public string OperationId { get; set; } = string.Empty;

    [JsonProperty("backupId")]
    public string BackupId { get; set; } = string.Empty;

    [JsonProperty("startTime")]
    public DateTime StartTime { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("progress")]
    public double Progress { get; set; }
}
