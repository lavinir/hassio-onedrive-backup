using Newtonsoft.Json;

namespace HassioOneDriveBackup.Models;

public class HassBackupInfoResponse
{
    [JsonProperty("result")]
    public string Result { get; set; } = string.Empty;

    [JsonProperty("data")]
    public BackupInfoData? Data { get; set; }

    public class BackupInfoData
    {
        [JsonProperty("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("date")]
        public DateTime? Date { get; set; }

        [JsonProperty("size")]
        public float? Size { get; set; }

        [JsonProperty("compressed")]
        public bool Compressed { get; set; }

        [JsonProperty("protected")]
        public bool Protected { get; set; }

        [JsonProperty("supervisor_version")]
        public string? SupervisorVersion { get; set; }

        [JsonProperty("homeassistant")]
        public string? HomeAssistantVersion { get; set; }

        [JsonProperty("addons")]
        public List<BackupAddonInfoData>? Addons { get; set; }

        [JsonProperty("folders")]
        public List<string>? Folders { get; set; }

        [JsonProperty("homeassistant_exclude_database")]
        public bool HomeAssistantExcludeDatabase { get; set; }
    }

    public class BackupAddonInfoData
    {
        [JsonProperty("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("size")]
        public float Size { get; set; }
    }
}
