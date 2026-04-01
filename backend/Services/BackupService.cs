using System.Collections.Concurrent;
using HassioOneDriveBackup.Models;
using Microsoft.Graph.Models;

namespace HassioOneDriveBackup.Services;

public class BackupService : IBackupService
{
    private readonly IHassioClient _hassioClient;
    private readonly IOneDriveClient _oneDriveClient;
    private readonly ISettingsService _settingsService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RetentionDataStore _retentionDataStore;
    private readonly ILogger<BackupService> _logger;
    private readonly ConcurrentDictionary<string, TransferOperation> _operations;

    public BackupService(
        IHassioClient hassioClient,
        IOneDriveClient oneDriveClient,
        ISettingsService settingsService,
        IDateTimeProvider dateTimeProvider,
        RetentionDataStore retentionDataStore,
        ILogger<BackupService> logger)
    {
        _hassioClient = hassioClient;
        _oneDriveClient = oneDriveClient;
        _settingsService = settingsService;
        _dateTimeProvider = dateTimeProvider;
        _retentionDataStore = retentionDataStore;
        _logger = logger;
        _operations = new ConcurrentDictionary<string, TransferOperation>();
    }

    public async Task<IEnumerable<Backup>> GetBackupsAsync()
    {
        var backups = await _hassioClient.GetBackupsAsync(_ => true);

        // Merge the persisted retained flags — the HA API doesn't store this
        var retainedSlugs = _retentionDataStore.GetRetainedSlugs();
        foreach (var backup in backups)
            backup.Retained = retainedSlugs.Contains(backup.Slug);

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
                Settings settings = await _settingsService.GetSettingsAsync();
                // First download the backup locally from Home Assistant
                var backup = await _hassioClient.DownloadBackupAsync(slugId);
                var localPath = backup.LocalPath;
                var oneDrivePath = $"backups/{settings.General.InstanceName}/{slugId}.tar";

                if (string.IsNullOrEmpty(localPath))
                {
                    throw new Exception($"Backup local path is null or empty for slug {slugId}");
                }
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
                if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
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
                Settings settings = await _settingsService.GetSettingsAsync();
                var oneDrivePath = $"backups/{settings.General.InstanceName}/{slugId}.tar";
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
        var backup = (await _hassioClient.GetBackupsAsync(b => b.Slug == slugId)).FirstOrDefault()
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        await _hassioClient.DeleteBackupAsync(backup);

        // Clean up any persisted retention flag for this backup
        _retentionDataStore.SetRetained(slugId, false);
    }

    public async Task<Backup> TriggerBackupAsync(string name)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var timeStamp = _dateTimeProvider.Now;
        var excludedAddonSlugs = settings.Backup.ExcludedAddons ?? new List<string>();

        bool isPartial = settings.Backup.ExcludeMediaFolder ||
                         settings.Backup.ExcludeSSLFolder ||
                         settings.Backup.ExcludeShareFolder ||
                         settings.Backup.ExcludeLocalAddonsFolder ||
                         excludedAddonSlugs.Any(s => !string.IsNullOrWhiteSpace(s));

        // For a partial backup the HA API expects the lists of what TO include, not what to exclude
        List<string>? includedAddons = null;
        List<string>? includedFolders = null;
        if (isPartial)
        {
            var allAddons = await _hassioClient.GetAddonsAsync();
            includedAddons = allAddons
                .Where(a => !excludedAddonSlugs.Any(ex => ex.Equals(a.Slug, StringComparison.OrdinalIgnoreCase)))
                .Select(a => a.Slug)
                .ToList();
            includedFolders = GetIncludedFolders(settings);
        }

        var success = await _hassioClient.CreateBackupAsync(
            name,
            timeStamp,
            appendTimestamp: true,
            compressed: true,
            password: settings.Backup.BackupPassword,
            folders: includedFolders,
            addons: includedAddons
        );

        if (!success)
            throw new Exception("Failed to create backup");

        var backups = await _hassioClient.GetBackupsAsync(b => b.Name.StartsWith(name));
        return backups.OrderByDescending(b => b.Date).First();
    }

    public async Task<Backup> UpdateBackupRetentionAsync(string slugId, bool retain)
    {
        var backup = (await _hassioClient.GetBackupsAsync(b => b.Slug == slugId)).FirstOrDefault()
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        // Persist so the flag survives the next GetBackupsAsync call
        _retentionDataStore.SetRetained(slugId, retain);
        backup.Retained = retain;
        return backup;
    }

    public Task<TransferOperation> GetTransferProgressAsync(string operationId)
    {
        if (_operations.TryGetValue(operationId, out var operation))
        {            
            return Task.FromResult(operation);
        }

        throw new ArgumentException("Operation not found", nameof(operationId));
    }

    // Returns the standard HA folders TO include in a partial backup (i.e. all except the ones the user excluded)
    private static List<string> GetIncludedFolders(Settings settings)
    {
        var folders = new List<string>();

        if (!settings.Backup.ExcludeLocalAddonsFolder)
            folders.Add("addons/local");

        if (!settings.Backup.ExcludeMediaFolder)
            folders.Add("media");

        if (!settings.Backup.ExcludeShareFolder)
            folders.Add("share");

        if (!settings.Backup.ExcludeSSLFolder)
            folders.Add("ssl");

        return folders;
    }
}