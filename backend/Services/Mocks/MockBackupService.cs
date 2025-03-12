using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services.Mocks;

public class MockBackupService : IBackupService
{
    private readonly List<Backup> _backups;
    private readonly Dictionary<string, BackupTransferOperation> _operations;
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
                Date = "2024-01-01 12:00:00",
                Size = "1.2 GB",
                Status = "Local",
                SourceType = "Automated",
                BackupType = "Full"
            },
            new()
            {
                Slug = "backup_2024_01_02",
                Name = "Manual Backup 2024-01-02",
                Date = "2024-01-02 15:30:00",
                Size = "800 MB",
                Status = "OneDrive",
                SourceType = "Manual",
                BackupType = "Partial"
            }
        };
        _operations = new Dictionary<string, BackupTransferOperation>();
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
        var operation = new BackupTransferOperation
        {
            OperationId = operationId,
            BackupId = slugId,
            StartTime = DateTime.UtcNow,
            Type = "upload"
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
        var operation = new BackupTransferOperation
        {
            OperationId = operationId,
            BackupId = slugId,
            StartTime = DateTime.UtcNow,
            Type = "download"
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

    public async Task<Backup> TriggerBackupAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        bool isPartial = settings.Backup.ExcludeMediaFolder || 
                        settings.Backup.ExcludeSSLFolder || 
                        settings.Backup.ExcludeShareFolder || 
                        settings.Backup.ExcludeLocalAddonsFolder ||
                        settings.Backup.ExcludedAddons.Any();

        var backup = new Backup
        {
            Slug = $"backup_{DateTime.UtcNow:yyyy_MM_dd_HHmmss}",
            Name = $"{(isPartial ? "Partial" : "Full")} Backup {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            Size = "1.5 GB",
            Status = "Local",
            SourceType = "Manual",
            BackupType = isPartial ? "Partial" : "Full"
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

        return await Task.FromResult(operation.Progress);
    }

    private async Task SimulateTransferAsync(string operationId)
    {
        var operation = _operations[operationId];
        
        // Simulate progress over 5 seconds
        for (int i = 0; i <= 100; i += 5)
        {
            operation.Progress = i / 100.0;
            await Task.Delay(250);
        }

        // Update backup status when complete
        var backup = _backups.First(b => b.Slug == operation.BackupId);
        backup.Status = operation.Type == "upload" ? "OneDrive" : "Local";

        // Cleanup operation
        _operations.Remove(operationId);
    }
}