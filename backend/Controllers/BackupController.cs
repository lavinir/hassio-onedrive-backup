using Microsoft.AspNetCore.Mvc;
using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly FileSyncStateService _fileSyncStateService;

    public BackupController(IBackupService backupService, FileSyncStateService fileSyncStateService)
    {
        _backupService = backupService;
        _fileSyncStateService = fileSyncStateService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Backup>>> GetBackups()
    {
        return Ok(await _backupService.GetBackupsAsync());
    }

    [HttpPost("{slug}/download")]
    public async Task<ActionResult<IDictionary<string, string>>> DownloadBackup(string slug)
    {
        try
        {
            var operationId = await _backupService.DownloadBackupAsync(slug);
            return Ok(new Dictionary<string, string> { { "operationId", operationId } });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{slug}")]
    public async Task<ActionResult> DeleteBackup(string slug)
    {
        await _backupService.DeleteBackupAsync(slug);
        return Ok();
    }

    [HttpPost("{slug}/upload")]
    public async Task<ActionResult<IDictionary<string, string>>> UploadBackup(string slug)
    {
        try
        {
            var operationId = await _backupService.UploadBackupAsync(slug);
            return Ok(new Dictionary<string, string> { { "operationId", operationId } });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("progress/{operationId}")]
    public async Task<ActionResult<object>> GetTransferProgress(string operationId)
    {
        var operation = await _backupService.GetTransferProgressAsync(operationId);
        return Ok(new { progress = operation.Progress, status = operation.Status.ToString() });
    }

    [HttpPost("create")]
    public async Task<ActionResult<Backup>> TriggerBackup(string name)
    {
        return Ok(await _backupService.TriggerBackupAsync(name));
    }

    [HttpPost("{slug}/retention")]
    public async Task<ActionResult<Backup>> UpdateBackupRetention(string slug, [FromBody] RetentionUpdate update)
    {
        var backup = await _backupService.UpdateBackupRetentionAsync(slug, update.Retain);
        return Ok(backup);
    }

    [HttpGet("{slug}/info")]
    public async Task<ActionResult<BackupInfoResult>> GetBackupInfo(string slug)
    {
        return Ok(await _backupService.GetBackupInfoAsync(slug));
    }

    [HttpGet("sync-status")]
    public ActionResult<object> GetSyncStatus()
    {
        var snap = _backupService.GetSyncStatus();
        var (fileSyncState, fileSyncLastTime) = _fileSyncStateService.GetStatus();
        return Ok(new
        {
            isSyncing = snap.IsSyncing,
            lastSyncTime = snap.LastSyncTime,
            activeUpload = snap.ActiveUpload == null ? null : new
            {
                slug = snap.ActiveUpload.BackupSlug,
                progress = snap.ActiveUpload.Progress
            },
            activeDownload = snap.ActiveDownload == null ? null : new
            {
                slug = snap.ActiveDownload.BackupSlug,
                progress = snap.ActiveDownload.Progress
            },
            activeBackupCreation = snap.BackupCreationProgress == null ? null : new
            {
                progress = snap.BackupCreationProgress
            },
            fileSyncState = new
            {
                state = fileSyncState,
                lastSyncTime = fileSyncLastTime
            }
        });
    }
}

public class RetentionUpdate
{
    public bool Retain { get; set; }
}