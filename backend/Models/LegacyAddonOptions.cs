using System.Text.Json.Serialization;

namespace HassioOneDriveBackup.Models;

/// <summary>
/// Minimal model matching the dev-branch (2.x) /data/settings.json flat snake_case format.
/// Used only for one-time migration on first startup after upgrading from the old addon.
/// </summary>
public class LegacyAddonOptions
{
    [JsonPropertyName("local_backup_num_to_keep")]
    public int? MaxLocalBackups { get; set; }

    [JsonPropertyName("onedrive_backup_num_to_keep")]
    public int? MaxOnedriveBackups { get; set; }

    [JsonPropertyName("backup_interval_days")]
    public float? BackupIntervalDays { get; set; }

    [JsonPropertyName("backup_passwd")]
    public string? BackupPassword { get; set; }

    [JsonPropertyName("backup_name")]
    public string? BackupName { get; set; }

    [JsonPropertyName("backup_instance_name")]
    public string? InstanceName { get; set; }

    [JsonPropertyName("monitor_all_local_backups")]
    public bool? MonitorAllLocalBackups { get; set; }

    [JsonPropertyName("notify_on_error")]
    public bool? NotifyOnError { get; set; }

    [JsonPropertyName("hass_api_timeout_minutes")]
    public int? HassAPITimeoutMinutes { get; set; }

    [JsonPropertyName("exclude_media_folder")]
    public bool? ExcludeMediaFolder { get; set; }

    [JsonPropertyName("exclude_ssl_folder")]
    public bool? ExcludeSSLFolder { get; set; }

    [JsonPropertyName("exclude_share_folder")]
    public bool? ExcludeShareFolder { get; set; }

    [JsonPropertyName("exclude_local_addons_folder")]
    public bool? ExcludeLocalAddonsFolder { get; set; }

    [JsonPropertyName("backup_allowed_hours")]
    public string? BackupAllowedHours { get; set; }

    [JsonPropertyName("sync_paths")]
    public List<string>? SyncPaths { get; set; }

    [JsonPropertyName("file_sync_remove_deleted")]
    public bool? FileSyncRemoveDeleted { get; set; }

    [JsonPropertyName("ignore_allowed_hours_for_file_sync")]
    public bool? IgnoreAllowedHoursForFileSync { get; set; }

    [JsonPropertyName("excluded_addons")]
    public List<string>? ExcludedAddons { get; set; }

    [JsonPropertyName("log_level")]
    public string? LogLevelStr { get; set; }

    [JsonPropertyName("ignore_upgrade_backups")]
    public bool? IgnoreUpgradeBackups { get; set; }

    [JsonPropertyName("enable_anonymous_telemetry")]
    public bool? EnableAnonymousTelemetry { get; set; }

    [JsonPropertyName("enable_anonymous_error_reporting")]
    public bool? EnableAnonymousErrorReporting { get; set; }

    [JsonPropertyName("generational_days")]
    public int? GenerationalDays { get; set; }

    [JsonPropertyName("generational_weeks")]
    public int? GenerationalWeeks { get; set; }

    [JsonPropertyName("generational_months")]
    public int? GenerationalMonths { get; set; }

    [JsonPropertyName("generational_years")]
    public int? GenerationalYears { get; set; }

    /// <summary>
    /// Returns true if any field that would never appear in a new-format Settings JSON is present.
    /// Used to detect legacy format before migration.
    /// </summary>
    public bool LooksLikeLegacyFormat =>
        MaxLocalBackups.HasValue ||
        MaxOnedriveBackups.HasValue ||
        BackupIntervalDays.HasValue ||
        InstanceName != null ||
        BackupName != null ||
        SyncPaths != null;

    /// <summary>Maps this legacy model to the new Settings structure.</summary>
    public Settings ToSettings()
    {
        var defaults = CreateDefaultSettings();
        return new Settings
        {
            General = new GeneralSettings
            {
                InstanceName = InstanceName ?? defaults.General.InstanceName,
                HassAPITimeoutMinutes = HassAPITimeoutMinutes ?? defaults.General.HassAPITimeoutMinutes,
                LogLevelStr = LogLevelStr ?? defaults.General.LogLevelStr,
                NotifyOnError = NotifyOnError ?? defaults.General.NotifyOnError,
                EnableAnonymousErrorReporting = EnableAnonymousErrorReporting ?? defaults.General.EnableAnonymousErrorReporting,
                EnableAnonymousTelemetry = EnableAnonymousTelemetry ?? defaults.General.EnableAnonymousTelemetry,
            },
            Backup = new BackupSettings
            {
                BackupName = BackupName ?? defaults.Backup.BackupName,
                BackupPassword = BackupPassword,
                BackupIntervalDays = BackupIntervalDays.HasValue ? (int)BackupIntervalDays.Value : defaults.Backup.BackupIntervalDays,
                BackupAllowedHours = BackupAllowedHours ?? defaults.Backup.BackupAllowedHours,
                MaxLocalBackups = MaxLocalBackups ?? defaults.Backup.MaxLocalBackups,
                MaxOnedriveBackups = MaxOnedriveBackups ?? defaults.Backup.MaxOnedriveBackups,
                GenerationalDays = GenerationalDays,
                GenerationalWeeks = GenerationalWeeks,
                GenerationalMonths = GenerationalMonths,
                GenerationalYears = GenerationalYears,
                ExcludedAddons = ExcludedAddons ?? new List<string>(),
                ExcludeMediaFolder = ExcludeMediaFolder ?? defaults.Backup.ExcludeMediaFolder,
                ExcludeSSLFolder = ExcludeSSLFolder ?? defaults.Backup.ExcludeSSLFolder,
                ExcludeShareFolder = ExcludeShareFolder ?? defaults.Backup.ExcludeShareFolder,
                ExcludeLocalAddonsFolder = ExcludeLocalAddonsFolder ?? defaults.Backup.ExcludeLocalAddonsFolder,
                MonitorAllLocalBackups = MonitorAllLocalBackups ?? defaults.Backup.MonitorAllLocalBackups,
                IgnoreUpgradeBackups = IgnoreUpgradeBackups ?? defaults.Backup.IgnoreUpgradeBackups,
            },
            FileSync = new FileSyncSettings
            {
                SyncPaths = SyncPaths ?? new List<string>(),
                FileSyncRemoveDeleted = FileSyncRemoveDeleted ?? defaults.FileSync.FileSyncRemoveDeleted,
                IgnoreAllowedHoursForFileSync = IgnoreAllowedHoursForFileSync ?? defaults.FileSync.IgnoreAllowedHoursForFileSync,
            }
        };
    }

    private static Settings CreateDefaultSettings() => new()
    {
        General = new GeneralSettings
        {
            InstanceName = "",
            HassAPITimeoutMinutes = 5,
            LogLevelStr = "info",
            NotifyOnError = true,
            EnableAnonymousErrorReporting = false,
            EnableAnonymousTelemetry = false
        },
        Backup = new BackupSettings
        {
            BackupName = "HassBackup",
            BackupIntervalDays = 3,
            BackupAllowedHours = "",
            MaxLocalBackups = 10,
            MaxOnedriveBackups = 20,
            ExcludedAddons = new List<string>(),
            MonitorAllLocalBackups = true,
        },
        FileSync = new FileSyncSettings
        {
            SyncPaths = new List<string>(),
            FileSyncRemoveDeleted = true,
        }
    };
}
