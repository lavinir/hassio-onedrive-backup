using HassioOneDriveBackup.Models;
using HassioOneDriveBackup.Utils;

namespace HassioOneDriveBackup.Services;

public class BackupOrchestratorService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);

    private readonly IHassioClient _hassioClient;
    private readonly IOneDriveClient _oneDriveClient;
    private readonly IBackupService _backupService;
    private readonly ISettingsService _settingsService;
    private readonly IRetentionPolicyService _retentionPolicy;
    private readonly RetentionDataStore _retentionDataStore;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HassEntityStateService _entityStateService;
    private readonly ILogger<BackupOrchestratorService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public BackupOrchestratorService(
        IHassioClient hassioClient,
        IOneDriveClient oneDriveClient,
        IBackupService backupService,
        ISettingsService settingsService,
        IRetentionPolicyService retentionPolicy,
        RetentionDataStore retentionDataStore,
        IDateTimeProvider dateTimeProvider,
        HassEntityStateService entityStateService,
        ILogger<BackupOrchestratorService> logger)
    {
        _hassioClient = hassioClient;
        _oneDriveClient = oneDriveClient;
        _backupService = backupService;
        _settingsService = settingsService;
        _retentionPolicy = retentionPolicy;
        _retentionDataStore = retentionDataStore;
        _dateTimeProvider = dateTimeProvider;
        _entityStateService = entityStateService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backup orchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await _semaphore.WaitAsync(0, stoppingToken))
            {
                try
                {
                    await RunOrchestratorTickAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Unhandled error in orchestrator tick");
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            else
            {
                _logger.LogDebug("Orchestrator tick skipped — previous run still in progress");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        _logger.LogInformation("Backup orchestrator stopped");
    }

    private async Task RunOrchestratorTickAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var now = _dateTimeProvider.Now;

        // Check allowed hours gate
        var allowedHours = TimeRangeHelper.GetAllowedHours(settings.Backup.BackupAllowedHours);
        if (!allowedHours[now.Hour])
        {
            _logger.LogDebug("Skipping orchestrator tick — current hour {Hour} is outside allowed hours", now.Hour);
            return;
        }

        _logger.LogDebug("Orchestrator tick starting at {Time}", now);

        var authInfo = await _oneDriveClient.IsLoggedInAsync();
        if (authInfo.AuthState != OneDriveAuthState.LoggedIn)
        {
            _logger.LogDebug("Skipping orchestrator tick — not authenticated with OneDrive");
            return;
        }

        _backupService.SetSyncing(true);

        // Fetch current state from both sources
        var localBackups = await _hassioClient.GetBackupsAsync(_ => true);
        var oneDriveItems = await _oneDriveClient.ListFilesInDirectoryAsync($"backups/{settings.General.InstanceName}");
        var oneDriveSlugs = new HashSet<string>(
            oneDriveItems.Select(i => Path.GetFileNameWithoutExtension(i.Name ?? string.Empty)),
            StringComparer.OrdinalIgnoreCase
        );

        // Also scan the legacy OneDrive root (dev-branch format: {name}_{slug}[.instanceName].tar at app root)
        // to prevent re-uploading backups that were already uploaded by the old addon version.
        await AddLegacyOneDriveSlugsAsync(oneDriveSlugs);

        // Merge persisted retention flags
        var retainedSlugs = _retentionDataStore.GetRetainedSlugs();
        foreach (var b in localBackups)
            b.Retained = retainedSlugs.Contains(b.Slug);

        // --- Step 1: Create a new backup if interval has elapsed ---
        await TryCreateBackupAsync(localBackups, settings, now, ct);

        // Re-fetch local backups after possible creation
        localBackups = await _hassioClient.GetBackupsAsync(_ => true);
        foreach (var b in localBackups)
            b.Retained = retainedSlugs.Contains(b.Slug);

        // --- Step 2: Upload local backups not yet on OneDrive ---
        await UploadPendingBackupsAsync(localBackups, oneDriveSlugs, ct);

        // Re-fetch OneDrive listing after uploads
        oneDriveItems = await _oneDriveClient.ListFilesInDirectoryAsync($"backups/{settings.General.InstanceName}");
        oneDriveSlugs = new HashSet<string>(
            oneDriveItems.Select(i => Path.GetFileNameWithoutExtension(i.Name ?? string.Empty)),
            StringComparer.OrdinalIgnoreCase
        );

        // --- Step 3: Enforce retention policy ---
        await EnforceRetentionAsync(localBackups, oneDriveItems, settings, ct);

        // --- Step 4: Update HA entity state ---
        var finalLocalBackups = await _hassioClient.GetBackupsAsync(_ => true);
        var finalOneDriveItems = await _oneDriveClient.ListFilesInDirectoryAsync($"backups/{settings.General.InstanceName}");

        _backupService.SetSyncing(false);

        _entityStateService.UpdateBackupState(s =>
        {
            s.State = "Backed_Up";
            s.LocalBackupCount = finalLocalBackups.Count;
            s.OneDriveBackupCount = finalOneDriveItems.Count;
            s.LastLocalBackupDate = finalLocalBackups.OrderByDescending(b => b.Date).FirstOrDefault()?.Date;
            s.LastOneDriveBackupDate = finalOneDriveItems
                .OrderByDescending(i => i.LastModifiedDateTime)
                .FirstOrDefault()?.LastModifiedDateTime?.LocalDateTime;
        });
    }

    private async Task TryCreateBackupAsync(List<Backup> localBackups, Settings settings, DateTime now, CancellationToken ct)
    {
        if (settings.Backup.BackupIntervalDays <= 0)
            return;

        var lastBackup = localBackups.OrderByDescending(b => b.Date).FirstOrDefault();
        bool isDue = lastBackup == null ||
                     (now - lastBackup.Date).TotalDays >= settings.Backup.BackupIntervalDays;

        if (!isDue)
        {
            _logger.LogDebug("Backup not due yet. Last: {Last}, Interval: {Interval} days",
                lastBackup?.Date, settings.Backup.BackupIntervalDays);
            return;
        }

        if (await _hassioClient.IsBackupManagerJobInProgressAsync())
        {
            _logger.LogInformation("Home Assistant Backup Manager job in progress — skipping backup creation");
            return;
        }

        _logger.LogInformation("Scheduled backup is due — creating new backup");

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
            settings.Backup.BackupName,
            now,
            appendTimestamp: true,
            compressed: true,
            password: settings.Backup.BackupPassword,
            folders: includedFolders,
            addons: includedAddons
        );

        if (jobId is null)
        {
            _logger.LogError("Scheduled backup creation failed");
            if (settings.General.NotifyOnError)
                await _hassioClient.SendPersistentNotificationAsync(
                    "Scheduled backup failed. Check addon logs for details.");
            await _hassioClient.PublishEventAsync(OneDriveEvents.BackupCreateFailed);
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromHours(1));
        try
        {
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(15), cts.Token);
                var (isDone, progress) = await _hassioClient.GetJobStatusAsync(jobId);
                _backupService.SetBackupCreationProgress(progress);
                _logger.LogInformation("Backup job {JobId} progress: {Progress:P0}", jobId, progress);
                if (isDone)
                    break;
            }
            _logger.LogInformation("Scheduled backup created successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Backup job {JobId} timed out after 1 hour", jobId);
            if (settings.General.NotifyOnError)
                await _hassioClient.SendPersistentNotificationAsync(
                    "Scheduled backup timed out. Check addon logs for details.");
            await _hassioClient.PublishEventAsync(OneDriveEvents.BackupCreateFailed);
        }
        finally
        {
            _backupService.SetBackupCreationProgress(null);
        }
    }

    private async Task UploadPendingBackupsAsync(List<Backup> localBackups, HashSet<string> oneDriveSlugs, CancellationToken ct)
    {
        var toUpload = localBackups
            .Where(b => !oneDriveSlugs.Contains(b.Slug))
            .OrderBy(b => b.Date)
            .ToList();

        if (toUpload.Count == 0)
        {
            _logger.LogDebug("No pending uploads");
            return;
        }

        _logger.LogInformation("Uploading {Count} backup(s) to OneDrive", toUpload.Count);

        foreach (var backup in toUpload)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation("Uploading backup {Slug}", backup.Slug);
                var operationId = await _backupService.UploadBackupAsync(backup.Slug);
                await _backupService.AwaitOperationAsync(operationId, ct);
                _logger.LogInformation("Backup {Slug} uploaded successfully", backup.Slug);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to upload backup {Slug}", backup.Slug);
                var settings = await _settingsService.GetSettingsAsync();
                if (settings.General.NotifyOnError)
                    await _hassioClient.SendPersistentNotificationAsync(
                        $"Failed to upload backup {backup.Slug} to OneDrive. Check addon logs for details.");
            }
        }
    }

    private async Task EnforceRetentionAsync(
        List<Backup> localBackups,
        IList<Microsoft.Graph.Models.DriveItem> oneDriveItems,
        Settings settings,
        CancellationToken ct)
    {
        // Local retention
        var localToDelete = _retentionPolicy.GetBackupsToDelete(localBackups, settings.Backup, settings.Backup.MaxLocalBackups).ToList();
        if (localToDelete.Count > 0)
        {
            _logger.LogInformation("Deleting {Count} local backup(s) per retention policy", localToDelete.Count);
            foreach (var backup in localToDelete)
            {
                ct.ThrowIfCancellationRequested();
                var success = await _hassioClient.DeleteBackupAsync(backup);
                if (!success)
                {
                    _logger.LogError("Failed to delete local backup {Slug}", backup.Slug);
                    await _hassioClient.PublishEventAsync(OneDriveEvents.LocalBackupDeleteFailed, backup.Slug);
                }
                else
                {
                    _retentionDataStore.SetRetained(backup.Slug, false);
                }
            }
        }

        // OneDrive retention — build Backup list from DriveItems for the policy service
        var oneDriveBackups = oneDriveItems
            .Where(i => i.Name != null)
            .Select(i => new Backup
            {
                Slug = Path.GetFileNameWithoutExtension(i.Name!),
                Date = i.LastModifiedDateTime?.UtcDateTime ?? DateTime.MinValue,
                Retained = _retentionDataStore.GetRetainedSlugs().Contains(
                    Path.GetFileNameWithoutExtension(i.Name!))
            })
            .ToList();

        var oneDriveToDelete = _retentionPolicy.GetBackupsToDelete(oneDriveBackups, settings.Backup, settings.Backup.MaxOnedriveBackups).ToList();
        if (oneDriveToDelete.Count > 0)
        {
            _logger.LogInformation("Deleting {Count} OneDrive backup(s) per retention policy", oneDriveToDelete.Count);
            foreach (var backup in oneDriveToDelete)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fileName = $"{backup.Slug}.tar";
                    var item = oneDriveItems.FirstOrDefault(i =>
                        string.Equals(i.Name, fileName, StringComparison.OrdinalIgnoreCase));

                    if (item?.Id != null)
                    {
                        var graphClient = await _oneDriveClient.GetGraphClientAsync();
                        var driveId = await GetDriveIdAsync(graphClient);
                        await graphClient.Drives[driveId].Items[item.Id].DeleteAsync();
                        _logger.LogInformation("Deleted OneDrive backup {Slug}", backup.Slug);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to delete OneDrive backup {Slug}", backup.Slug);
                    await _hassioClient.PublishEventAsync(OneDriveEvents.OneDriveBackupDeleteFailed, backup.Slug);
                }
            }
        }
    }

    private async Task<string> GetDriveIdAsync(Microsoft.Graph.GraphServiceClient graphClient)
    {
        var drive = await graphClient.Me.Drive.GetAsync();
        return drive?.Id ?? throw new InvalidOperationException("Could not retrieve OneDrive drive ID");
    }

    private static List<string> GetIncludedFolders(Settings settings)
    {
        var folders = new List<string>();
        if (!settings.Backup.ExcludeLocalAddonsFolder) folders.Add("addons/local");
        if (!settings.Backup.ExcludeMediaFolder) folders.Add("media");
        if (!settings.Backup.ExcludeShareFolder) folders.Add("share");
        if (!settings.Backup.ExcludeSSLFolder) folders.Add("ssl");
        return folders;
    }

    /// <summary>
    /// Scans the OneDrive app folder root for backups uploaded by the old dev-branch addon,
    /// which stored files as {name}_{slug}[.instanceName].tar at the root (no subfolder).
    /// Extracts slugs and adds them to <paramref name="oneDriveSlugs"/> so those backups
    /// are not re-uploaded after upgrading to ng-alpha.
    /// </summary>
    private async Task AddLegacyOneDriveSlugsAsync(HashSet<string> oneDriveSlugs)
    {
        try
        {
            var rootItems = await _oneDriveClient.ListFilesInDirectoryAsync(string.Empty);
            foreach (var item in rootItems)
            {
                if (item.Name == null || !item.Name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                    continue;

                var slug = ExtractSlugFromLegacyFileName(item.Name);
                if (slug != null)
                    oneDriveSlugs.Add(slug);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not scan OneDrive root for legacy backups — skipping");
        }
    }

    /// <summary>
    /// Parses the dev-branch filename pattern {name}_{slug}[.instanceName].tar and returns the slug.
    /// The slug is the segment between the last underscore and the first dot (or .tar) after it.
    /// Returns null if the filename doesn't match the expected pattern.
    /// </summary>
    internal static string? ExtractSlugFromLegacyFileName(string fileName)
    {
        // Strip .tar extension
        var withoutExt = fileName[..^4]; // remove ".tar"

        // Slug is after the last underscore
        var underscoreIdx = withoutExt.LastIndexOf('_');
        if (underscoreIdx < 0)
            return null;

        var afterUnderscore = withoutExt[(underscoreIdx + 1)..];

        // If instance name is appended as .instanceName, strip it
        var dotIdx = afterUnderscore.IndexOf('.');
        var slug = dotIdx >= 0 ? afterUnderscore[..dotIdx] : afterUnderscore;

        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }
}
