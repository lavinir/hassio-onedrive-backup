using Microsoft.AspNetCore.Mvc;
using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly IOneDriveClient _oneDriveClient;

    public SettingsController(ISettingsService settingsService, IOneDriveClient oneDriveClient)
    {
        _settingsService = settingsService;
        _oneDriveClient = oneDriveClient;
    }

    // Settings endpoints
    [HttpGet]
    public async Task<ActionResult<Settings>> GetSettings()
    {
        return Ok(await _settingsService.GetSettingsAsync());
    }

    [HttpPut]
    public async Task<ActionResult<Settings>> UpdateSettings(Settings settings)
    {
        return Ok(await _settingsService.UpdateSettingsAsync(settings));
    }

    // OneDrive connection endpoints
    [HttpGet("login-status")]
    public async Task<ActionResult<IDictionary<string, string>>> CheckLoginStatus()
    {
        var authInfo = await _oneDriveClient.IsLoggedInAsync();
        return Ok(new Dictionary<string, string> { 
            { "authState", authInfo.AuthState.ToString() },
            { "userEmail", authInfo.UserEmail ?? "" }
        });
    }

    [HttpPost("onedrive/auth")]
    public async Task<ActionResult<IDictionary<string, string>>> InitiateAuth()
    {
        var (deviceCode, verificationUrl, userCode) = await _oneDriveClient.InitiateAuthenticationAsync();
        return Ok(new Dictionary<string, string> 
        { 
            { "verificationUrl", verificationUrl },
            { "userCode", userCode }
        });
    }

    [HttpPost("onedrive/disconnect")]
    public ActionResult Disconnect()
    {
        _oneDriveClient.Disconnect();
        return Ok();
    }

    [HttpPost("onedrive/reset")]
    public async Task<ActionResult> ResetConnection()
    {
        await _oneDriveClient.ResetConnectionAsync();
        return Ok();
    }
}