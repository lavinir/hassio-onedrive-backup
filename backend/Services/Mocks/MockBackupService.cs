using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services.Mocks;

public class MockBackupService : IBackupService
{
    private readonly List<Backup> _backups;
    private readonly Dictionary<string, TransferOperation> _operations;
    private readonly ISettingsService _settingsService;

    public MockBackupService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _backups = new List<Backup>
        {
            new()
            {
                Slug = "backup_2024_01_01",
                Name = "Automated Backup 2024-01-01",
                Date = DateTime.Parse("2024-01-01 12:00:00"),
                Size = "1 GB",
                Status = "Local",
                SourceType = "Automated",
                BackupType = "Full",
                Content = new Content
                {
                    Homeassistant = true,
                    Addons = new string[] { "addon1", "addon2" },
                    Folders = new string[] { "folder1", "folder2" }
                }
            },
            new()
            {
                Slug = "backup_2024_01_02",
                Name = "Manual Backup 2024-01-02",
                Date = DateTime.Parse("2024-01-02 15:30:00"),
                Size = "100 MB",
                Status = "OneDrive",
                SourceType = "Manual",
                BackupType = "Partial",
                Content = new Content
                {
                    Homeassistant = true,
                    Addons = new string[] { "addon1" },
                    Folders = new string[] { "folder1" }
                }
            }
        };
        _operations = new Dictionary<string, TransferOperation>();
    }

    public async Task<IEnumerable<Backup>> GetBackupsAsync()
    {
        return await Task.FromResult(_backups);
    }

    public async Task<string> UploadBackupAsync(string slugId)
    {
        var backup = _backups.FirstOrDefault(b => b.Slug == slugId)
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        if (backup.Status != "Local")
            throw new InvalidOperationException("Backup must be local to upload");

        string operationId = Guid.NewGuid().ToString();
        var operation = new TransferOperation
        {
            Id = operationId,
            BackupSlug = slugId,
            Type = TransferType.Upload,
            Status = TransferStatus.InProgress,
            StartTime = DateTime.UtcNow
        };

        _operations[operationId] = operation;

        // Start simulated upload in background
        _ = SimulateTransferAsync(operationId);

        return await Task.FromResult(operationId);
    }

    public async Task<string> DownloadBackupAsync(string slugId)
    {
        var backup = _backups.FirstOrDefault(b => b.Slug == slugId)
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        if (backup.Status != "OneDrive")
            throw new InvalidOperationException("Backup must be on OneDrive to download");

        string operationId = Guid.NewGuid().ToString();
        var operation = new TransferOperation
        {
            Id = operationId,
            BackupSlug = slugId,
            Type = TransferType.Download,
            Status = TransferStatus.InProgress,
            StartTime = DateTime.UtcNow
        };

        _operations[operationId] = operation;

        // Start simulated download in background
        _ = SimulateTransferAsync(operationId);

        return await Task.FromResult(operationId);
    }

    public async Task DeleteBackupAsync(string slugId)
    {
        var backup = _backups.FirstOrDefault(b => b.Slug == slugId)
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        _backups.Remove(backup);
        await Task.CompletedTask;
    }

    public async Task<Backup> TriggerBackupAsync(string name)
    {
        var settings = await _settingsService.GetSettingsAsync();
        bool isPartial = settings.Backup.ExcludeMediaFolder || 
                        settings.Backup.ExcludeSSLFolder || 
                        settings.Backup.ExcludeShareFolder || 
                        settings.Backup.ExcludeLocalAddonsFolder ||
                        settings.Backup.ExcludedAddons.Any();

        // Use provided name if not empty or null, otherwise use default naming pattern
        string backupName = !string.IsNullOrWhiteSpace(name) 
            ? name 
            : $"{(isPartial ? "Partial" : "Full")} Backup {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

        var backup = new Backup
        {
            Slug = $"backup_{DateTime.UtcNow:yyyy_MM_dd_HHmmss}",
            Name = backupName,
            Date = DateTime.UtcNow,
            Size = "2.2 GB",
            Status = "Local",
            SourceType = "Manual",
            BackupType = isPartial ? "Partial" : "Full",
            Content = new Content
            {
                Homeassistant = true,
                Addons = new string[0],
                Folders = new string[0]
            }
        };

        _backups.Add(backup);
        return await Task.FromResult(backup);
    }

    public async Task<Backup> UpdateBackupRetentionAsync(string slugId, bool retain)
    {
        var backup = _backups.FirstOrDefault(b => b.Slug == slugId)
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        backup.Retained = retain;
        return await Task.FromResult(backup);
    }

    public async Task<double> GetTransferProgressAsync(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            throw new ArgumentException("Operation not found", nameof(operationId));

        return await Task.FromResult((double)operation.Progress);
    }

    public Task<TransferProgress> GetDetailedTransferProgressAsync(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            throw new ArgumentException("Operation not found", nameof(operationId));

        return Task.FromResult(new TransferProgress
        {
            BytesTransferred = operation.Progress,
            TotalBytes = 100 // Since we're using percentage progress
        });
    }

    private async Task SimulateTransferAsync(string operationId)
    {
        var operation = _operations[operationId];
        
        // Simulate progress over 5 seconds
        for (int i = 0; i <= 100; i += 5)
        {
            operation.Progress = i;
            await Task.Delay(250);
        }

        // Update backup status when complete
        var backup = _backups.First(b => b.Slug == operation.BackupSlug);
        backup.Status = operation.Type == TransferType.Upload ? "OneDrive" : "Local";

        // Update operation status
        operation.Status = TransferStatus.Completed;
        operation.EndTime = DateTime.UtcNow;

        // Cleanup operation
        _operations.Remove(operationId);
    }
}