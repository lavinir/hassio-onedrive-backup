using System.Text.Json;
using Microsoft.Extensions.Configuration;
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

    public SettingsService(ILogger<SettingsService> logger, IOneDriveClient oneDriveClient, IConfiguration configuration)
    {
        var dataFolder = configuration["DataFolder"] ?? "/data";
        _settingsPath = Path.Combine(dataFolder, "settings.json");
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
            if (!File.Exists(_settingsPath))
                return null;

            var json = File.ReadAllText(_settingsPath);

            // Try new format first
            var newFormat = JsonSerializer.Deserialize<Settings>(json);
            if (newFormat?.General != null && !IsLikelyLegacyFile(newFormat, json))
                return newFormat;

            // Attempt legacy (dev-branch 2.x) flat snake_case migration
            var legacy = JsonSerializer.Deserialize<LegacyAddonOptions>(json);
            if (legacy != null && legacy.LooksLikeLegacyFormat)
            {
                _logger.LogInformation("Detected legacy settings format — migrating to new format");
                var migrated = legacy.ToSettings();
                // Overwrite file with new format so migration doesn't re-run
                File.WriteAllText(_settingsPath,
                    JsonSerializer.Serialize(migrated, new JsonSerializerOptions { WriteIndented = true }));
                _logger.LogInformation("Legacy settings migrated and saved");
                return migrated;
            }

            return newFormat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from disk");
        }
        return null;
    }

    /// <summary>
    /// Returns true when a JSON file that parsed as Settings looks like it may actually be the
    /// old flat snake_case format (new-format deserialization silently returns all defaults).
    /// Heuristic: if all numeric fields are zero/default AND the raw JSON contains snake_case keys,
    /// it's almost certainly a legacy file.
    /// </summary>
    private static bool IsLikelyLegacyFile(Settings parsed, string rawJson) =>
        parsed.General.InstanceName == string.Empty &&
        parsed.Backup.MaxLocalBackups == 0 &&
        parsed.Backup.MaxOnedriveBackups == 0 &&
        (rawJson.Contains("local_backup_num_to_keep") ||
         rawJson.Contains("backup_interval_days") ||
         rawJson.Contains("backup_instance_name"));

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
            GenerationalDays = null,
            GenerationalWeeks = null,
            GenerationalMonths = null,
            GenerationalYears = null,
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