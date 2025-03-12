using Microsoft.AspNetCore.Mvc;
using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;

    public BackupController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Backup>>> GetBackups()
    {
        return Ok(await _backupService.GetBackupsAsync());
    }

    [HttpPost("{slug}/download")]
    public async Task<ActionResult<IDictionary<string, string>>> DownloadBackup(string slug)
    {
        var operationId = await _backupService.DownloadBackupAsync(slug);
        return Ok(new Dictionary<string, string> { { "operationId", operationId } });
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
        var operationId = await _backupService.UploadBackupAsync(slug);
        return Ok(new Dictionary<string, string> { { "operationId", operationId } });
    }

    [HttpGet("progress/{operationId}")]
    public async Task<ActionResult<IDictionary<string, double>>> GetTransferProgress(string operationId)
    {
        var progress = await _backupService.GetTransferProgressAsync(operationId);
        return Ok(new Dictionary<string, double> { { "progress", progress } });
    }

    [HttpPost("create")]
    public async Task<ActionResult<Backup>> TriggerBackup()
    {
        return Ok(await _backupService.TriggerBackupAsync());
    }

    [HttpPost("{slug}/retention")]
    public async Task<ActionResult<Backup>> UpdateBackupRetention(string slug, [FromBody] RetentionUpdate update)
    {
        var backup = await _backupService.UpdateBackupRetentionAsync(slug, update.Retain);
        return Ok(backup);
    }
}

public class RetentionUpdate
{
    public bool Retain { get; set; }
}