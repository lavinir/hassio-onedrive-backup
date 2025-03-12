using Microsoft.AspNetCore.Mvc;
using HassioOneDriveBackup.Services;
using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly IOneDriveAuthService _authService;

    public SettingsController(ISettingsService settingsService, IOneDriveAuthService authService)
    {
        _settingsService = settingsService;
        _authService = authService;
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
    public async Task<ActionResult<IDictionary<string, bool>>> CheckLoginStatus()
    {
        var isLoggedIn = await _authService.IsLoggedInAsync();
        return Ok(new Dictionary<string, bool> { { "isLoggedIn", isLoggedIn } });
    }

    [HttpPost("onedrive/auth")]
    public async Task<ActionResult<IDictionary<string, string>>> InitiateAuth()
    {
        var (deviceCode, verificationUrl, userCode) = await _authService.InitiateAuthenticationAsync();
        return Ok(new Dictionary<string, string> 
        { 
            { "verificationUrl", verificationUrl },
            { "userCode", userCode }
        });
    }

    [HttpPost("onedrive/disconnect")]
    public async Task<ActionResult> Disconnect()
    {
        await _authService.DisconnectAsync();
        return Ok();
    }

    [HttpPost("onedrive/test")]
    public async Task<ActionResult<IDictionary<string, object>>> TestConnection()
    {
        var success = await _settingsService.TestOneDriveConnectionAsync();
        return Ok(new Dictionary<string, object>
        {
            { "success", success },
            { "message", success ? "Connection successful" : "Connection failed" }
        });
    }

    [HttpPost("onedrive/reset")]
    public async Task<ActionResult> ResetConnection()
    {
        await _settingsService.ResetOneDriveConnectionAsync();
        return Ok();
    }
}