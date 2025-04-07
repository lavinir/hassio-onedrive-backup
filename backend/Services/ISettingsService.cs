using HassioOneDriveBackup.Models;

namespace HassioOneDriveBackup.Services;

public interface ISettingsService
{
    Task<Settings> GetSettingsAsync();
    Task<Settings> UpdateSettingsAsync(Settings settings);
}