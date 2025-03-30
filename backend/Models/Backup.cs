using System.Text.Json.Serialization;

namespace HassioOneDriveBackup.Models;

public class Backup
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size")]
    public string Size { get; set; }

    [JsonPropertyName("backup_type")]
    public string BackupType { get; set; }

    [JsonPropertyName("protected")]
    public bool Protected { get; set; }

    [JsonPropertyName("compressed")]
    public bool Compressed { get; set; }

    [JsonPropertyName("content")]
    public Content Content { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("retained")]
    public bool Retained { get; set; }
}

public class Content
{
    [JsonPropertyName("homeassistant")]
    public bool Homeassistant { get; set; }

    [JsonPropertyName("addons")]
    public string[] Addons { get; set; }

    [JsonPropertyName("folders")]
    public string[] Folders { get; set; }
}


public class BackupTransferOperation
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;
    
    [JsonPropertyName("backupId")]
    public string BackupId { get; set; } = string.Empty;
    
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("progress")]
    public double Progress { get; set; }
}