using System.Globalization;
using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services.Mocks;

public class MockHassioClient : IHassioClient
{
    private readonly List<Backup> _backups;
    private readonly List<Addon> _addons;
    private readonly ILogger<MockHassioClient> _logger;

    public MockHassioClient(ILogger<MockHassioClient> logger)
    {
        _logger = logger;

        // Initialize with some mock backups
        _backups = new List<Backup>
        {
            new()
            {
                Slug = "mock_backup_full_20240101",
                Name = "Full Backup 2024-01-01",
                Date = DateTime.Parse("2024-01-01 08:00:00"),
                Size = "2.5 GB",
                Status = "Local",
                SourceType = "Automated",
                BackupType = "Full",
            },
            new()
            {
                Slug = "mock_backup_partial_20240215",
                Name = "Partial Backup 2024-02-15",
                Date = DateTime.Parse("2024-02-15 15:30:00"),
                Size = "800 MB",
                Status = "Local",
                SourceType = "Manual",
                BackupType = "Partial",
            }
        };

        // Initialize with some mock addons
        _addons = new List<Addon>
        {
            new()
            {
                Name = "Terminal & SSH",
                Slug = "core_ssh",
            },
            new()
            {
                Name = "Mosquitto broker",
                Slug = "core_mosquitto",
            },
            new()
            {
                Name = "Samba share",
                Slug = "core_samba",
            },
            new()
            {
                Name = "File editor",
                Slug = "core_configurator",
            }
        };
    }

    public async Task<List<Backup>> GetBackupsAsync(Predicate<Backup> filter)
    {
        _logger.LogInformation("Mock: Getting backups");
        return await Task.FromResult(filter != null 
            ? _backups.Where(b => filter(b)).ToList() 
            : _backups.ToList());
    }

    public async Task SendPersistentNotificationAsync(string message, string? notificationId = null)
    {
        _logger.LogInformation($"Mock: Sending notification: {message}, ID: {notificationId ?? "none"}");
        await Task.CompletedTask;
    }

    public async Task<bool> CreateBackupAsync(string backupName, DateTime timeStamp, bool appendTimestamp = true, bool compressed = true, string? password = null, IEnumerable<string>? folders = null, IEnumerable<string>? addons = null)
    {
        const string dt_format = "yyyyMMdd_HHmmss";
        
        // Create a simulated backup
        var isPartial = folders != null || addons != null;
        string finalName = appendTimestamp 
            ? $"{backupName}_{timeStamp.ToString(dt_format, CultureInfo.InvariantCulture)}" 
            : backupName;
        
        var newBackup = new Backup
        {
            Slug = $"mock_backup_{(isPartial ? "partial" : "full")}_{timeStamp.ToString("yyyyMMdd_HHmmss")}",
            Name = finalName,
            Date = timeStamp,
            Size = isPartial ? "750 MB" : "2.3 GB",
            Status = "Local",
            SourceType = "Manual",
            BackupType = isPartial ? "Partial" : "Full",            
        };

        _logger.LogInformation($"Mock: Created new {newBackup.BackupType} backup: {newBackup.Name}");
        _backups.Add(newBackup);
        
        // Simulate some delay for backup creation
        await Task.Delay(500);
        return true;
    }

    public async Task<bool> DeleteBackupAsync(Backup backup)
    {
        var backupToDelete = _backups.FirstOrDefault(b => b.Slug == backup.Slug);
        if (backupToDelete == null)
        {
            _logger.LogWarning($"Mock: Attempted to delete non-existent backup: {backup.Slug}");
            return false;
        }

        _backups.Remove(backupToDelete);
        _logger.LogInformation($"Mock: Deleted backup: {backup.Name} ({backup.Slug})");
        await Task.Delay(200); // Simulate a short delay
        return true;
    }

    public async Task UpdateHassEntityStateAsync(string entityId, string payload)
    {
        _logger.LogInformation($"Mock: Updating entity state: {entityId} with payload: {payload}");
        await Task.CompletedTask;
    }

    public async Task<Backup> DownloadBackupAsync(string backupSlug)
    {
        var backup = _backups.FirstOrDefault(b => b.Slug == backupSlug);
        if (backup == null)
        {
            _logger.LogWarning($"Mock: Attempted to download non-existent backup: {backupSlug}");
            throw new ArgumentException($"Backup with slug {backupSlug} not found", nameof(backupSlug));
        }

        // Create a temporary directory if it doesn't exist
        string tempDir = Path.Combine(Path.GetTempPath(), "hassio-mock-backups");
        Directory.CreateDirectory(tempDir);
        
        // Create an actual file with some dummy content
        string filePath = Path.Combine(tempDir, $"{backupSlug}.tar");
        
        // Generate some random content to simulate the backup file
        byte[] dummyContent = new byte[1024 * 100]; // 100KB dummy file
        new Random().NextBytes(dummyContent);
        
        await File.WriteAllBytesAsync(filePath, dummyContent);
        
        _logger.LogInformation($"Mock: Downloaded backup to: {filePath}");
        

        backup.LocalPath = filePath; // Set the local path for the downloaded backup
        // Simulate a download delay
        await Task.Delay(1000);
        return backup;
    }

    public async Task<bool> UploadBackupAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !filePath.Contains(".tar"))
        {
            _logger.LogWarning($"Mock: Invalid backup file path: {filePath}");
            return false;
        }

        _logger.LogInformation($"Mock: Uploaded backup from file: {filePath}");
        
        // Simulate an upload delay
        await Task.Delay(1500);
        return true;
    }

    public async Task<List<Addon>> GetAddonsAsync()
    {
        _logger.LogInformation("Mock: Getting addons");
        return await Task.FromResult(_addons.ToList());
    }

    public async Task<HassAddonInfoResponse> GetAddonInfo(string slug)
    {
        var addon = _addons.FirstOrDefault(a => a.Slug == slug);
        if (addon == null)
        {
            _logger.LogWarning($"Mock: Addon info requested for non-existent addon: {slug}");
            throw new ArgumentException($"Addon with slug {slug} not found", nameof(slug));
        }

        var response = new HassAddonInfoResponse
        {
            DataProperty = new HassAddonInfoResponse.Data
            {
                IngressEntry = $"/api/hassio_ingress/{slug}",
                IngressUrl = $"https://homeassistant.local:8123/api/hassio_ingress/{slug}"
            }
        };

        _logger.LogInformation($"Mock: Retrieved info for addon: {slug}");
        await Task.Delay(100); // Simulate a short delay
        return response;
    }

    public async Task<string> GetTimeZoneAsync()
    {
        _logger.LogInformation("Mock: Getting timezone");
        return await Task.FromResult("UTC");
    }

    public async Task PublishEventAsync(OneDriveEvents eventType, string payload = "")
    {
        _logger.LogInformation($"Mock: Published event: {eventType} with payload: {payload}");
        await Task.CompletedTask;
    }

    public async Task RestartSelf()
    {
        _logger.LogInformation("Mock: Restarting self");
        await Task.Delay(500); // Simulate restart
    }

}