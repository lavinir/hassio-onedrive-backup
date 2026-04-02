using System.Text.Json.Serialization;

namespace HassioOneDriveBackup.Models;

/// <summary>
/// Minimal model matching the dev-branch /config/additionalBackupData.json format,
/// used only for one-time migration on first startup after upgrading from the old addon.
/// </summary>
public class LegacyBackupAdditionalData
{
    [JsonPropertyName("Backups")]
    public List<BackupData> Backups { get; set; } = new();

    public class BackupData
    {
        [JsonPropertyName("Slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("RetainLocal")]
        public bool RetainLocal { get; set; }

        [JsonPropertyName("RetainOneDrive")]
        public bool RetainOneDrive { get; set; }
    }
}
