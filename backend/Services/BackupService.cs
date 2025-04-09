using System.Collections.Concurrent;
using HassioOneDriveBackup.Models;
using Microsoft.Graph.Models;

namespace HassioOneDriveBackup.Services;

public class BackupService : IBackupService
{
    private readonly IHassioClient _hassioClient;
    private readonly IOneDriveClient _oneDriveClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<BackupService> _logger;
    private readonly ConcurrentDictionary<string, TransferOperation> _operations;

    public BackupService(
        IHassioClient hassioClient,
        IOneDriveClient oneDriveClient,
        ISettingsService settingsService,
        ILogger<BackupService> logger)
    {
        _hassioClient = hassioClient;
        _oneDriveClient = oneDriveClient;
        _settingsService = settingsService;
        _logger = logger;
        _operations = new ConcurrentDictionary<string, TransferOperation>();
    }

    public async Task<IEnumerable<Backup>> GetBackupsAsync()
    {
        // Get all backups from Home Assistant
        var backups = await _hassioClient.GetBackupsAsync(_ => true);
        return backups;
    }

    public Task<string> UploadBackupAsync(string slugId)
    {
        // Create a new transfer operation
        var operation = new TransferOperation
        {
            BackupSlug = slugId,
            Type = TransferType.Upload,
            Status = TransferStatus.InProgress
        };
        
        if (!_operations.TryAdd(operation.Id, operation))
        {
            throw new InvalidOperationException("Failed to create transfer operation");
        }

        // Start the upload process in the background
        _ = Task.Run(async () =>
        {
            try
            {
                // First download the backup locally from Home Assistant
                var localPath = await _hassioClient.DownloadBackupAsync(slugId);
                var oneDrivePath = $"backups/{slugId}.tar";

                // Upload to OneDrive with progress tracking
                await _oneDriveClient.UploadFileAsync(localPath, oneDrivePath, (bytesTransferred, totalBytes) =>
                {
                    if (totalBytes.HasValue)
                    {
                        operation.Progress = (int)((double)bytesTransferred / totalBytes.Value * 100);
                    }
                });

                // Update operation status
                operation.Status = TransferStatus.Completed;
                operation.EndTime = DateTime.UtcNow;

                // Clean up local file
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }

                // Notify Home Assistant
                await _hassioClient.PublishEventAsync(OneDriveEvents.BackupUploaded, slugId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload backup {SlugId}", slugId);
                operation.Status = TransferStatus.Failed;
                operation.EndTime = DateTime.UtcNow;
                await _hassioClient.PublishEventAsync(OneDriveEvents.BackupUploadFailed, slugId);
            }
        });

        return Task.FromResult(operation.Id);
    }

    public Task<string> DownloadBackupAsync(string slugId)
    {
        // Create a new transfer operation
        var operation = new TransferOperation
        {
            BackupSlug = slugId,
            Type = TransferType.Download,
            Status = TransferStatus.InProgress
        };
        
        if (!_operations.TryAdd(operation.Id, operation))
        {
            throw new InvalidOperationException("Failed to create transfer operation");
        }

        // Start the download process in the background
        _ = Task.Run(async () =>
        {
            try
            {
                var oneDrivePath = $"backups/{slugId}.tar";
                var localPath = Path.Combine(Path.GetTempPath(), $"{slugId}.tar");

                // Download from OneDrive with progress tracking
                await _oneDriveClient.DownloadFileAsync(oneDrivePath, localPath, (bytesTransferred, totalBytes) =>
                {
                    if (totalBytes.HasValue)
                    {
                        operation.Progress = (int)((double)bytesTransferred / totalBytes.Value * 100);
                    }
                });

                // Upload the backup to Home Assistant
                var success = await _hassioClient.UploadBackupAsync(localPath);
                if (!success)
                {
                    throw new Exception("Failed to upload backup to Home Assistant");
                }

                // Update operation status
                operation.Status = TransferStatus.Completed;
                operation.EndTime = DateTime.UtcNow;

                // Clean up local file
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }

                // Notify Home Assistant
                await _hassioClient.PublishEventAsync(OneDriveEvents.BackupDownloaded, slugId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download backup {SlugId}", slugId);
                operation.Status = TransferStatus.Failed;
                operation.EndTime = DateTime.UtcNow;
                await _hassioClient.PublishEventAsync(OneDriveEvents.BackupDownloadFailed, slugId);
            }
        });

        return Task.FromResult(operation.Id);
    }

    public async Task DeleteBackupAsync(string slugId)
    {
        // Get the backup details
        var backup = (await _hassioClient.GetBackupsAsync(b => b.Slug == slugId)).FirstOrDefault()
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        // Delete from Home Assistant
        await _hassioClient.DeleteBackupAsync(backup);
    }

    public async Task<Backup> TriggerBackupAsync(string name)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var timeStamp = DateTime.UtcNow;
        var excludedAddons = settings.Backup.ExcludedAddons ?? new List<string>();
        var excludedFolders = GetExcludedFolders(settings);

        bool isPartial = settings.Backup.ExcludeMediaFolder ||
                        settings.Backup.ExcludeSSLFolder ||
                        settings.Backup.ExcludeShareFolder ||
                        settings.Backup.ExcludeLocalAddonsFolder ||
                        excludedAddons.Any();

        // Create the backup
        var success = await _hassioClient.CreateBackupAsync(
            name,
            timeStamp,
            appendTimestamp: true,
            compressed: true, // Always use compression
            password: settings.Backup.BackupPassword,
            folders: isPartial ? excludedFolders : null,
            addons: isPartial ? excludedAddons : null
        );

        if (!success)
        {
            throw new Exception("Failed to create backup");
        }

        // Return the newly created backup
        var backups = await _hassioClient.GetBackupsAsync(b => b.Name.StartsWith(name));
        return backups.OrderByDescending(b => b.Date).First();
    }

    public async Task<Backup> UpdateBackupRetentionAsync(string slugId, bool retain)
    {
        var backup = (await _hassioClient.GetBackupsAsync(b => b.Slug == slugId)).FirstOrDefault()
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        backup.Retained = retain;
        return backup;
    }

    public Task<double> GetTransferProgressAsync(string operationId)
    {
        if (_operations.TryGetValue(operationId, out var operation))
        {
            // If the transfer has failed, return -1 to indicate failure
            if (operation.Status == TransferStatus.Failed)
            {
                return Task.FromResult(-1.0);
            }
            
            return Task.FromResult((double)operation.Progress);
        }

        throw new ArgumentException("Operation not found", nameof(operationId));
    }

    public Task<TransferProgress> GetDetailedTransferProgressAsync(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
        {
            throw new ArgumentException("Operation not found", nameof(operationId));
        }

        return Task.FromResult(new TransferProgress
        {
            BytesTransferred = operation.Progress,
            TotalBytes = 100 // Since we're using percentage progress
        });
    }

    private static string[] GetExcludedFolders(Settings settings)
    {
        var excludedFolders = new List<string>();
        
        if (settings.Backup.ExcludeMediaFolder)
            excludedFolders.Add("media");
        
        if (settings.Backup.ExcludeSSLFolder)
            excludedFolders.Add("ssl");
        
        if (settings.Backup.ExcludeShareFolder)
            excludedFolders.Add("share");
        
        if (settings.Backup.ExcludeLocalAddonsFolder)
            excludedFolders.Add("addons/local");

        return excludedFolders.ToArray();
    }
}