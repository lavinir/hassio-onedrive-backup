using System.Text.Json;
using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services;

/// <summary>
/// Posts backup and sync state to Home Assistant sensor entities.
/// All updates are fire-and-forget — failures are logged but never thrown to callers.
/// </summary>
public class HassEntityStateService
{
    private const string BackupEntityId = "sensor.onedrivebackup";
    private const string FileSyncEntityId = "sensor.onedrivefilesync";

    private readonly IHassioClient _hassioClient;
    private readonly ILogger<HassEntityStateService> _logger;

    // State tracked for the backup sensor
    private BackupEntityState _backupState = new();
    private readonly object _backupStateLock = new();

    public HassEntityStateService(IHassioClient hassioClient, ILogger<HassEntityStateService> logger)
    {
        _hassioClient = hassioClient;
        _logger = logger;
    }

    public void UpdateBackupState(Action<BackupEntityState> update)
    {
        lock (_backupStateLock)
        {
            update(_backupState);
        }

        _ = PostBackupStateAsync(_backupState);
    }

    public void SetSyncing(bool syncing)
    {
        lock (_backupStateLock)
        {
            _backupState.IsSyncing = syncing;
            // Only clear progress when leaving syncing state, not when entering it
            if (!syncing)
            {
                _backupState.UploadPercentage = null;
                _backupState.DownloadPercentage = null;
            }
        }

        _ = PostBackupStateAsync(_backupState);
    }

    public void UpdateFileSyncState(string state, string? details = null)
    {
        _ = PostFileSyncStateAsync(state, details);
    }

    private async Task PostBackupStateAsync(BackupEntityState state)
    {
        try
        {
            string effectiveState = state.IsSyncing ? "Syncing" : state.State;

            var attributes = new Dictionary<string, object?>
            {
                ["last_local_backup"] = state.LastLocalBackupDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                ["last_onedrive_backup"] = state.LastOneDriveBackupDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                ["local_backup_count"] = state.LocalBackupCount,
                ["onedrive_backup_count"] = state.OneDriveBackupCount,
                ["upload_percentage"] = state.IsSyncing ? state.UploadPercentage : null,
                ["download_percentage"] = state.IsSyncing ? state.DownloadPercentage : null,
            };

            var payload = JsonSerializer.Serialize(new { state = effectiveState, attributes });
            await _hassioClient.UpdateHassEntityStateAsync(BackupEntityId, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update {EntityId} in Home Assistant", BackupEntityId);
        }
    }

    private async Task PostFileSyncStateAsync(string state, string? details)
    {
        try
        {
            var attributes = new Dictionary<string, object?> { ["details"] = details };
            var payload = JsonSerializer.Serialize(new { state, attributes });
            await _hassioClient.UpdateHassEntityStateAsync(FileSyncEntityId, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update {EntityId} in Home Assistant", FileSyncEntityId);
        }
    }
}

public class BackupEntityState
{
    public string State { get; set; } = "Unknown";
    public bool IsSyncing { get; set; }
    public DateTime? LastLocalBackupDate { get; set; }
    public DateTime? LastOneDriveBackupDate { get; set; }
    public int LocalBackupCount { get; set; }
    public int OneDriveBackupCount { get; set; }
    public int? UploadPercentage { get; set; }
    public int? DownloadPercentage { get; set; }
}
