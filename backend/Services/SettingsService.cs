using System.Text.Json;
using Microsoft.Extensions.Logging;
using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly ILogger<SettingsService> _logger;
    private readonly IOneDriveClient _oneDriveClient;
    private Settings _settings;
    private readonly object _lock = new();

    public SettingsService(ILogger<SettingsService> logger, IOneDriveClient oneDriveClient)
    {
        _settingsPath = Path.Combine("/config", "hassio-onedrive-backup-settings.json");
        _logger = logger;
        _oneDriveClient = oneDriveClient;
        _settings = LoadSettingsFromDisk() ?? CreateDefaultSettings();
    }

    public async Task<Settings> GetSettingsAsync()
    {
        return await Task.FromResult(_settings);
    }

    public async Task<Settings> UpdateSettingsAsync(Settings settings)
    {
        lock (_lock)
        {
            _settings = settings;
            SaveSettingsToDisk();
        }
        return await Task.FromResult(_settings);
    }

    public async Task<bool> TestOneDriveConnectionAsync()
    {
        var authInfo = await _oneDriveClient.IsLoggedInAsync();
        return authInfo.AuthState == OneDriveAuthState.LoggedIn;
    }

    public async Task ResetOneDriveConnectionAsync()
    {
        _oneDriveClient.Disconnect();
        await Task.CompletedTask;
    }

    private Settings? LoadSettingsFromDisk()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<Settings>(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from disk");
        }
        return null;
    }

    private void SaveSettingsToDisk()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            _logger.LogDebug("Settings saved to disk");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to disk");
        }
    }

    private static Settings CreateDefaultSettings() => new()
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
            InstanceName = "Home Assistant",
            BackupName = "{type}-backup-{date}",
            BackupIntervalDays = 3,
            BackupAllowedHours = "*",
            MaxLocalBackups = 10,
            MaxOnedriveBackups = 20,
            GenerationalDays = 7,
            GenerationalWeeks = 4,
            GenerationalMonths = 6,
            GenerationalYears = 1,
            ExcludedAddons = new List<string>(),
            ExcludeMediaFolder = false,
            ExcludeSSLFolder = false,
            ExcludeShareFolder = false,
            ExcludeLocalAddonsFolder = false,
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