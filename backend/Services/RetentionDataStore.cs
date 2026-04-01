using System.Text.Json;

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

    public RetentionDataStore(ILogger<RetentionDataStore> logger)
    {
        _filePath = Path.Combine("/data", "retained_backups.json");
        _logger = logger;
        _retainedSlugs = Load();
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
}
