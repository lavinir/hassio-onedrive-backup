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
    private volatile bool _isSyncing;
    private DateTime? _lastSyncTime;
    private float? _backupCreationProgress;

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
        var settings = await _settingsService.GetSettingsAsync();
        var localBackups = await _hassioClient.GetBackupsAsync(_ => true);

        IEnumerable<DriveItem> oneDriveItems;
        try
        {
            oneDriveItems = await _oneDriveClient.ListFilesInDirectoryAsync(
                $"backups/{settings.General.InstanceName}");
        }
        catch (UnauthorizedAccessException)
        {
            return localBackups.OrderByDescending(b => b.Date);
        }

        var oneDriveSlugs = new HashSet<string>(
            oneDriveItems.Select(i => Path.GetFileNameWithoutExtension(i.Name ?? "")),
            StringComparer.OrdinalIgnoreCase);
        var retainedSlugs = _retentionDataStore.GetRetainedSlugs();

        foreach (var b in localBackups)
        {
            b.Retained = retainedSlugs.Contains(b.Slug);
            b.Status = oneDriveSlugs.Contains(b.Slug) ? "Synced" : "Local";
        }

        var localSlugs = new HashSet<string>(localBackups.Select(b => b.Slug), StringComparer.OrdinalIgnoreCase);
        var oneDriveOnly = oneDriveItems
            .Where(i => i.Name != null && !localSlugs.Contains(Path.GetFileNameWithoutExtension(i.Name!)))
            .Select(i =>
            {
                var slug = Path.GetFileNameWithoutExtension(i.Name!);
                var meta = OneDriveBackupMetadata.TryDecode(i.Description);
                return new Backup
                {
                    Slug = slug,
                    Name = meta?.Name ?? slug,
                    Date = meta?.Date ?? i.LastModifiedDateTime?.UtcDateTime ?? DateTime.MinValue,
                    Size = FormatBytes(meta?.Size ?? i.Size ?? 0),
                    Status = "OneDrive",
                    SourceType = "External",
                    BackupType = "Full",
                    Retained = retainedSlugs.Contains(slug)
                };
            });

        return localBackups.Concat(oneDriveOnly).OrderByDescending(b => b.Date);
    }

    public Task<string> UploadBackupAsync(string slugId)
    {
        if (_operations.Values.Any(o => o.BackupSlug == slugId && o.Status == TransferStatus.InProgress))
            throw new InvalidOperationException($"An operation for backup '{slugId}' is already in progress");

        var operation = new TransferOperation
        {
            BackupSlug = slugId,
            Type = TransferType.Upload,
            Status = TransferStatus.InProgress
        };

        if (!_operations.TryAdd(operation.Id, operation))
            throw new InvalidOperationException("Failed to create transfer operation");

        // Start the upload process in the background — store the Task so failures are observable
        operation.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                Settings settings = await _settingsService.GetSettingsAsync();
                var backup = await _hassioClient.DownloadBackupAsync(slugId);
                var localPath = backup.LocalPath;
                var oneDrivePath = $"backups/{settings.General.InstanceName}/{slugId}.tar";

                if (string.IsNullOrEmpty(localPath))
                    throw new Exception($"Backup local path is null or empty for slug {slugId}");

                var metadata = new OneDriveBackupMetadata
                {
                    Name = backup.Name,
                    Date = backup.Date,
                    Size = new FileInfo(localPath).Length
                };

                await _oneDriveClient.UploadFileAsync(localPath, oneDrivePath, (bytesTransferred, totalBytes) =>
                {
                    if (totalBytes.HasValue)
                        operation.Progress = (int)((double)bytesTransferred / totalBytes.Value * 100);
                }, metadata.Encode());

                operation.Status = TransferStatus.Completed;
                operation.EndTime = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                    File.Delete(localPath);

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
        if (_operations.Values.Any(o => o.BackupSlug == slugId && o.Status == TransferStatus.InProgress))
            throw new InvalidOperationException($"An operation for backup '{slugId}' is already in progress");

        var operation = new TransferOperation
        {
            BackupSlug = slugId,
            Type = TransferType.Download,
            Status = TransferStatus.InProgress
        };

        if (!_operations.TryAdd(operation.Id, operation))
            throw new InvalidOperationException("Failed to create transfer operation");

        // Start the download process in the background — store the Task so failures are observable
        operation.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                Settings settings = await _settingsService.GetSettingsAsync();
                var oneDrivePath = $"backups/{settings.General.InstanceName}/{slugId}.tar";
                var localPath = Path.Combine(Path.GetTempPath(), $"{slugId}.tar");

                await _oneDriveClient.DownloadFileAsync(oneDrivePath, localPath, (bytesTransferred, totalBytes) =>
                {
                    if (totalBytes.HasValue)
                        operation.Progress = (int)((double)bytesTransferred / totalBytes.Value * 100);
                });

                var success = await _hassioClient.UploadBackupAsync(localPath);
                if (!success)
                    throw new Exception("Failed to upload backup to Home Assistant");

                operation.Status = TransferStatus.Completed;
                operation.EndTime = DateTime.UtcNow;

                if (File.Exists(localPath))
                    File.Delete(localPath);

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

        var jobId = await _hassioClient.CreateBackupAsync(
            name,
            timeStamp,
            appendTimestamp: true,
            compressed: true,
            password: settings.Backup.BackupPassword,
            folders: includedFolders,
            addons: includedAddons
        );

        if (jobId is null)
            throw new Exception("Failed to create backup");

        var backups = await _hassioClient.GetBackupsAsync(b => b.Name.StartsWith(name));
        return backups.OrderByDescending(b => b.Date).First();
    }

    public async Task<Backup> UpdateBackupRetentionAsync(string slugId, bool retain)
    {
        var backup = (await _hassioClient.GetBackupsAsync(b => b.Slug == slugId)).FirstOrDefault()
            ?? throw new ArgumentException("Backup not found", nameof(slugId));

        _retentionDataStore.SetRetained(slugId, retain);
        backup.Retained = retain;
        return backup;
    }

    public Task<TransferOperation> GetTransferProgressAsync(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            throw new ArgumentException("Operation not found", nameof(operationId));

        // Sync status from the background task if it faulted before setting Status itself
        if (operation.BackgroundTask?.IsFaulted == true && operation.Status == TransferStatus.InProgress)
        {
            operation.Status = TransferStatus.Failed;
            operation.EndTime = DateTime.UtcNow;
        }

        return Task.FromResult(operation);
    }

    public async Task AwaitOperationAsync(string operationId, CancellationToken ct = default)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            throw new ArgumentException("Operation not found", nameof(operationId));

        if (operation.BackgroundTask != null)
            await operation.BackgroundTask.WaitAsync(ct);
    }

    public void SetSyncing(bool syncing)
    {
        _isSyncing = syncing;
        if (!syncing)
            _lastSyncTime = DateTime.UtcNow;
    }

    public void SetBackupCreationProgress(float? progress)
    {
        _backupCreationProgress = progress;
    }

    public SyncStatusSnapshot GetSyncStatus()
    {
        var activeUpload = _operations.Values
            .FirstOrDefault(o => o.Type == TransferType.Upload && o.Status == TransferStatus.InProgress);
        var activeDownload = _operations.Values
            .FirstOrDefault(o => o.Type == TransferType.Download && o.Status == TransferStatus.InProgress);
        return new SyncStatusSnapshot(_isSyncing, _lastSyncTime, activeUpload, activeDownload, _backupCreationProgress);
    }

    public async Task<BackupInfoResult> GetBackupInfoAsync(string slug)
    {
        var infoResponse = await _hassioClient.GetBackupInfoAsync(slug);

        if (infoResponse?.Data == null)
            return new BackupInfoResult { IsAvailableLocally = false, Slug = slug };

        var data = infoResponse.Data;
        return new BackupInfoResult
        {
            IsAvailableLocally = true,
            Slug = data.Slug,
            Name = data.Name,
            Date = data.Date,
            Size = data.Size,
            Type = data.Type,
            Compressed = data.Compressed,
            IsProtected = data.Protected,
            SupervisorVersion = data.SupervisorVersion,
            HomeAssistantVersion = data.HomeAssistantVersion,
            Addons = data.Addons?.Select(a => new BackupAddonInfo
            {
                Slug = a.Slug,
                Name = a.Name,
                Version = a.Version,
                Size = a.Size
            }).ToList(),
            Folders = data.Folders,
            HomeAssistantExcludeDatabase = data.HomeAssistantExcludeDatabase
        };
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1_048_576 => $"{bytes / 1024.0:F1} KB",
        < 1_073_741_824 => $"{bytes / 1_048_576.0:F1} MB",
        _ => $"{bytes / 1_073_741_824.0:F1} GB"
    };

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
