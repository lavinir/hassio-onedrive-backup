using System.Text.Json.Serialization;

namespace HassioOneDriveBackup.Models;

public class BackupInfoResult
{
    [JsonPropertyName("isAvailableLocally")]
    public bool IsAvailableLocally { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("size")]
    public float? Size { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("compressed")]
    public bool? Compressed { get; set; }

    [JsonPropertyName("isProtected")]
    public bool? IsProtected { get; set; }

    [JsonPropertyName("supervisorVersion")]
    public string? SupervisorVersion { get; set; }

    [JsonPropertyName("homeAssistantVersion")]
    public string? HomeAssistantVersion { get; set; }

    [JsonPropertyName("addons")]
    public List<BackupAddonInfo>? Addons { get; set; }

    [JsonPropertyName("folders")]
    public List<string>? Folders { get; set; }

    [JsonPropertyName("homeAssistantExcludeDatabase")]
    public bool? HomeAssistantExcludeDatabase { get; set; }
}

public class BackupAddonInfo
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public float Size { get; set; }
}
