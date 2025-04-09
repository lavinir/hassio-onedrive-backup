namespace HassioOneDriveBackup.Models;

public class Settings
{
    public GeneralSettings General { get; set; } = new();
    public BackupSettings Backup { get; set; } = new();
    public FileSyncSettings FileSync { get; set; } = new();
}

public class GeneralSettings
{
    public int HassAPITimeoutMinutes { get; set; }
    public string LogLevelStr { get; set; } = "info";
    public bool NotifyOnError { get; set; }
    public bool EnableAnonymousErrorReporting { get; set; }
    public bool EnableAnonymousTelemetry { get; set; }
}

public class BackupSettings
{
    // Core backup settings
    public string InstanceName { get; set; } = string.Empty;
    public string BackupName { get; set; } = string.Empty;
    public string? BackupPassword { get; set; }
    public int BackupIntervalDays { get; set; }
    public string BackupAllowedHours { get; set; } = "*";

    // Retention settings
    public int MaxLocalBackups { get; set; }
    public int MaxOnedriveBackups { get; set; }
    public int? GenerationalDays { get; set; }
    public int? GenerationalWeeks { get; set; }
    public int? GenerationalMonths { get; set; }
    public int? GenerationalYears { get; set; }

    // Exclusion settings
    public List<string> ExcludedAddons { get; set; } = new();
    public bool ExcludeMediaFolder { get; set; }
    public bool ExcludeSSLFolder { get; set; }
    public bool ExcludeShareFolder { get; set; }
    public bool ExcludeLocalAddonsFolder { get; set; }

    // Behavioral settings
    public bool MonitorAllLocalBackups { get; set; }
    public bool IgnoreUpgradeBackups { get; set; }
}

public class FileSyncSettings
{
    public List<string> SyncPaths { get; set; } = new();
    public bool FileSyncRemoveDeleted { get; set; }
    public bool IgnoreAllowedHoursForFileSync { get; set; }
}