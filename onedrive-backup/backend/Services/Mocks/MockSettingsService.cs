using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services.Mocks;

public class MockSettingsService : ISettingsService
{
    private Settings _settings;

    public MockSettingsService()
    {
        // Initialize with default settings
        _settings = new Settings
        {
            General = new GeneralSettings
            {
                HassAPITimeoutMinutes = 5,
                LogLevelStr = "info",
                NotifyOnError = true,
                EnableAnonymousErrorReporting = false,
                EnableAnonymousTelemetry = false
            },
            Backup = new BackupSettings
            {
                // Core backup settings
                BackupName = "{type}-backup-{date}",
                BackupIntervalDays = 3,
                BackupAllowedHours = "*",

                // Retention settings
                MaxLocalBackups = 10,
                MaxOnedriveBackups = 20,
                GenerationalDays = 7,
                GenerationalWeeks = 4,
                GenerationalMonths = 6,
                GenerationalYears = 1,

                // Exclusion settings
                ExcludedAddons = new List<string>(),
                ExcludeMediaFolder = false,
                ExcludeSSLFolder = false,
                ExcludeShareFolder = false,
                ExcludeLocalAddonsFolder = false,

                // Behavioral settings
                MonitorAllLocalBackups = true,
                IgnoreUpgradeBackups = false
            },
            FileSync = new FileSyncSettings
            {
                SyncPaths = new List<string>(),
                FileSyncRemoveDeleted = true,
                IgnoreAllowedHoursForFileSync = false
            }
        };
    }

    public async Task<Settings> GetSettingsAsync()
    {
        return await Task.FromResult(_settings);
    }

    public async Task<Settings> UpdateSettingsAsync(Settings settings)
    {
        _settings = settings;
        return await Task.FromResult(_settings);
    }

    public async Task<bool> TestOneDriveConnectionAsync()
    {
        // Simulate a successful connection test
        await Task.Delay(1000); // Simulate some network delay
        return true;
    }

    public async Task ResetOneDriveConnectionAsync()
    {
        await Task.Delay(500); // Simulate some processing time
        // In a real implementation, this would clear OAuth tokens etc.
    }
}