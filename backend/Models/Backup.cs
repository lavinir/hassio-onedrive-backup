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
    public float Size { get; set; }

    [JsonProperty("type")]
    public string BackupType { get; set; }

    [JsonProperty("protected")]
    public bool Protected { get; set; }

    [JsonProperty("compressed")]
    public bool Compressed { get; set; }

    [JsonProperty("content")]
    public Content Content { get; set; }

    public string Status { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? Path { get; set; }
    public bool Retained { get; set; }
}

public class Content
{
    [JsonProperty("homeassistant")]
    public bool Homeassistant { get; set; }

    [JsonProperty("addons")]
    public string[] Addons { get; set; }

    [JsonProperty("folders")]
    public string[] Folders { get; set; }
}


public class BackupTransferOperation
{
    public string OperationId { get; set; } = string.Empty;
    public string BackupId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string Type { get; set; } = string.Empty;
    public double Progress { get; set; }
}