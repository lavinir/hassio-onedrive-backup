using System.Text.Json;
using HassioOneDriveBackup.Models;
using Microsoft.Extensions.Configuration;

namespace HassioOneDriveBackup.Services;

/// <summary>
/// Persists the user-pinned ("retain indefinitely") flag for backup slugs.
/// The HA Supervisor API does not store this flag, so we keep it in a local JSON file.
/// </summary>
public class RetentionDataStore
{
    private readonly string _filePath;
    private readonly ILogger<RetentionDataStore> _logger;
    private readonly object _lock = new();
    private HashSet<string> _retainedSlugs;

    public RetentionDataStore(IConfiguration configuration, ILogger<RetentionDataStore> logger)
    {
        var dataFolder = configuration["DataFolder"] ?? "/data";
        var configFolder = configuration["ConfigFolder"] ?? "/config";
        _filePath = Path.Combine(dataFolder, "retained_backups.json");
        _logger = logger;
        _retainedSlugs = Load();
        MigrateFromLegacyIfNeeded(configFolder);
    }

    public IReadOnlySet<string> GetRetainedSlugs()
    {
        lock (_lock)
        {
            return _retainedSlugs.ToHashSet();
        }
    }

    public void SetRetained(string slug, bool retain)
    {
        lock (_lock)
        {
            if (retain)
                _retainedSlugs.Add(slug);
            else
                _retainedSlugs.Remove(slug);

            Save();
        }
    }

    private HashSet<string> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load retained backups list, starting fresh");
        }
        return new HashSet<string>();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_retainedSlugs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save retained backups list");
        }
    }

    private void MigrateFromLegacyIfNeeded(string configFolder)
    {
        var legacyPath = Path.Combine(configFolder, "additionalBackupData.json");
        if (!File.Exists(legacyPath))
            return;

        try
        {
            _logger.LogInformation("Migrating retention flags from legacy additionalBackupData.json");
            var json = File.ReadAllText(legacyPath);
            var legacy = JsonSerializer.Deserialize<LegacyBackupAdditionalData>(json);
            if (legacy?.Backups != null)
            {
                var toMigrate = legacy.Backups
                    .Where(b => !string.IsNullOrWhiteSpace(b.Slug) && (b.RetainLocal || b.RetainOneDrive))
                    .Select(b => b.Slug!);

                lock (_lock)
                {
                    foreach (var slug in toMigrate)
                        _retainedSlugs.Add(slug);
                    Save();
                }

                _logger.LogInformation("Migrated {Count} retained backup(s) from legacy format",
                    legacy.Backups.Count(b => b.RetainLocal || b.RetainOneDrive));
            }

            // Rename so migration doesn't re-run on next restart
            File.Move(legacyPath, legacyPath + ".migrated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate legacy retention data — skipping");
        }
    }
}
