using System.Security.Cryptography;
using HassioOneDriveBackup.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace HassioOneDriveBackup.Services;

public class FileSyncService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);

    private readonly IOneDriveClient _oneDriveClient;
    private readonly ISettingsService _settingsService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HassEntityStateService _entityStateService;
    private readonly FileSyncStateService _fileSyncStateService;
    private readonly ILogger<FileSyncService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public FileSyncService(
        IOneDriveClient oneDriveClient,
        ISettingsService settingsService,
        IDateTimeProvider dateTimeProvider,
        HassEntityStateService entityStateService,
        FileSyncStateService fileSyncStateService,
        ILogger<FileSyncService> logger)
    {
        _oneDriveClient = oneDriveClient;
        _settingsService = settingsService;
        _dateTimeProvider = dateTimeProvider;
        _entityStateService = entityStateService;
        _fileSyncStateService = fileSyncStateService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File sync service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await _semaphore.WaitAsync(0, stoppingToken))
            {
                try
                {
                    await RunFileSyncTickAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Unhandled error in file sync tick");
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            else
            {
                _logger.LogDebug("File sync tick skipped — previous run still in progress");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        _logger.LogInformation("File sync service stopped");
    }

    private async Task RunFileSyncTickAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsAsync();

        var syncPaths = settings.FileSync.SyncPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (syncPaths.Count == 0)
        {
            _logger.LogDebug("File sync skipped — no sync paths configured");
            return;
        }

        if (!settings.FileSync.IgnoreAllowedHoursForFileSync)
        {
            var now = _dateTimeProvider.Now;
            var allowedHours = TimeRangeHelper.GetAllowedHours(settings.Backup.BackupAllowedHours);
            if (!allowedHours[now.Hour])
            {
                _logger.LogDebug("Skipping file sync — current hour {Hour} is outside allowed hours", now.Hour);
                return;
            }
        }

        var instanceName = settings.General.InstanceName;
        var syncRoot = string.IsNullOrWhiteSpace(instanceName)
            ? "FileSync"
            : $"FileSync/{instanceName}";

        _logger.LogDebug("File sync tick starting (root: {SyncRoot})", syncRoot);

        // Rebuild Matcher each tick so path changes are picked up without restart
        var matcher = new Matcher();
        matcher.AddIncludePatterns(syncPaths);

        _entityStateService.UpdateFileSyncState("Syncing");
        _fileSyncStateService.SetSyncing();

        var matchingFiles = matcher.GetResultsInFullPath("/").ToList();
        _logger.LogDebug("Found {Count} file(s) matching sync paths", matchingFiles.Count);

        foreach (var localPath in matchingFiles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await SyncFileAsync(localPath, syncRoot, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error syncing file {Path}", localPath);
            }
        }

        if (settings.FileSync.FileSyncRemoveDeleted)
        {
            var localRemotePaths = matchingFiles
                .Select(p => $"{syncRoot}/{Path.GetRelativePath("/", p).Replace('\\', '/')}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            try
            {
                await DeleteRemovedFilesAsync(syncRoot, localRemotePaths, matcher, ct, syncRoot);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during delete-removed-files pass");
            }
        }

        _entityStateService.UpdateFileSyncState("Synced");
        _fileSyncStateService.SetSynced();
        _logger.LogDebug("File sync tick complete");
    }

    private async Task SyncFileAsync(string localPath, string syncRoot, CancellationToken ct)
    {
        var fileInfo = new FileInfo(localPath);
        if (fileInfo.Length == 0)
        {
            _logger.LogDebug("Skipping 0-byte file {Path}", localPath);
            return;
        }

        var relPath = Path.GetRelativePath("/", localPath).Replace('\\', '/');
        var remotePath = $"{syncRoot}/{relPath}";

        var localHash = ComputeSha256(localPath);
        var remoteItem = await _oneDriveClient.GetFileAsync(remotePath);

        bool needsUpload = remoteItem == null
            || remoteItem.Size != fileInfo.Length
            || !string.Equals(remoteItem.File?.Hashes?.Sha256Hash, localHash, StringComparison.OrdinalIgnoreCase);

        if (!needsUpload)
        {
            _logger.LogDebug("File {Path} is up to date — skipping", localPath);
            return;
        }

        _logger.LogInformation("Uploading file {Path} to OneDrive", localPath);
        await _oneDriveClient.UploadFileAsync(localPath, remotePath);
        _logger.LogInformation("File {Path} uploaded successfully", localPath);
    }

    private async Task DeleteRemovedFilesAsync(
        string remotePath,
        HashSet<string> localRemotePaths,
        Matcher matcher,
        CancellationToken ct,
        string? syncRoot = null)
    {
        syncRoot ??= remotePath;

        IList<Microsoft.Graph.Models.DriveItem> items;
        try
        {
            items = await _oneDriveClient.ListFilesInDirectoryAsync(remotePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not list {Path} (may not exist yet)", remotePath);
            return;
        }

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            if (item.Name == null) continue;

            var itemRemotePath = $"{remotePath}/{item.Name}";

            if (item.Folder != null)
            {
                // Recurse into subfolder first
                await DeleteRemovedFilesAsync(itemRemotePath, localRemotePaths, matcher, ct, syncRoot);

                // Remove empty folders
                var remaining = await _oneDriveClient.ListFilesInDirectoryAsync(itemRemotePath);
                if (remaining.Count == 0)
                {
                    _logger.LogInformation("Deleting empty OneDrive folder {Path}", itemRemotePath);
                    await _oneDriveClient.DeleteFileAsync(itemRemotePath);
                }
            }
            else if (item.File != null)
            {
                // Local path corresponding to this remote file
                var localPath = "/" + itemRemotePath[(syncRoot.Length + 1)..].Replace('/', Path.DirectorySeparatorChar);
                bool localExists = File.Exists(localPath);
                bool inPatterns = matcher.Match(Path.GetRelativePath("/", localPath)).HasMatches;

                if (!localExists || !inPatterns)
                {
                    _logger.LogInformation("Deleting removed/unmatched file from OneDrive: {Path}", itemRemotePath);
                    await _oneDriveClient.DeleteFileAsync(itemRemotePath);
                }
            }
        }
    }

    private static string ComputeSha256(string path)
    {
        using var hasher = SHA256.Create();
        using var stream = File.OpenRead(path);
        return BitConverter.ToString(hasher.ComputeHash(stream)).Replace("-", "");
    }
}
