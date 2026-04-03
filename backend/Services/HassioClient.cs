using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using HassioOneDriveBackup.Models;
using Newtonsoft.Json;

namespace HassioOneDriveBackup.Services;

public class HassioClient : IHassioClient
{
    private const string SupervisorBaseUri = "http://supervisor";
    private const string HassBaseUri = "http://supervisor/core/api";

    private readonly ILogger<HassioClient> _logger;
    private readonly ITelemetryManager _telemetryManager;
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;

    public HassioClient(IConfiguration configuration, ISettingsService settingsService, ILogger<HassioClient> logger, ITelemetryManager telemetryManager)
    {
        _logger = logger;
        _telemetryManager = telemetryManager;
        _settingsService = settingsService;

        var token = configuration["SUPERVISOR_TOKEN"];
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("SUPERVISOR_TOKEN is not set — all Home Assistant API calls will fail with 401.");
            token = string.Empty;
        }

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<bool> DeleteBackupAsync(Backup backup)
    {
        try
        {
            var uri = new Uri(SupervisorBaseUri + $"/backups/{backup.Slug}");
            var response = await _httpClient.DeleteAsync(uri);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup {Slug}", backup.Slug);
            _telemetryManager.TrackException(ex);
            return false;
        }

        return true;
    }

    public async Task<List<Backup>> GetBackupsAsync(Predicate<Backup> filter)
    {
        var uri = new Uri(SupervisorBaseUri + "/backups");
        var response = await GetJsonResponseAsync<HassBackupsResponse>(uri);
        if (!response.Result.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Failed getting backups from Supervisor. Result: {response.Result}");
        }

        var backups = response.DataProperty.Backups;
        return filter != null ? backups.Where(b => filter(b)).ToList() : backups.ToList();
    }

    public async Task<string?> CreateBackupAsync(string backupName, DateTime timeStamp, bool appendTimestamp = true, bool compressed = true, string? password = null, IEnumerable<string>? folders = null, IEnumerable<string>? addons = null)
    {
        const string dtFormat = "yyyy-MM-dd-HH-mm";
        string finalBackupName = appendTimestamp ? $"{backupName}_{timeStamp.ToString(dtFormat, CultureInfo.CurrentCulture)}" : backupName;

        string payloadStr;
        Uri uri;

        // Full Backup
        if (folders == null && addons == null)
        {
            uri = new Uri(SupervisorBaseUri + "/backups/new/full");
            var fullPayload = new { name = finalBackupName, compressed, password, background = true };
            payloadStr = JsonConvert.SerializeObject(fullPayload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            _logger.LogInformation("Starting full local backup");
        }
        // Partial Backup
        else
        {
            uri = new Uri(SupervisorBaseUri + "/backups/new/partial");
            var partialPayload = new { name = finalBackupName, compressed, password, homeassistant = true, addons, folders, background = true };
            payloadStr = JsonConvert.SerializeObject(partialPayload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            _logger.LogInformation("Starting partial local backup");
        }

        try
        {
            var response = await _httpClient.PostAsync(uri, new StringContent(payloadStr, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            var jobResponse = JsonConvert.DeserializeObject<HassBackgroundJobResponse>(content);
            var jobId = jobResponse?.Data?.JobId;
            _logger.LogInformation("Backup started in background (job_id: {JobId})", jobId);
            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed creating new backup");
            _telemetryManager.TrackException(ex);
            return null;
        }
    }

    public async Task<(bool isDone, float progress)> GetJobStatusAsync(string jobId)
    {
        var uri = new Uri(SupervisorBaseUri + $"/jobs/{jobId}");
        var response = await GetJsonResponseAsync<HassJobStatusResponse>(uri);
        return (response?.Data?.Done ?? false, response?.Data?.Progress ?? 0f);
    }

    public async Task<bool> UploadBackupAsync(string filePath)
    {
        try
        {
            var uri = new Uri(SupervisorBaseUri + "/backups/new/upload");
            using var multiPartFormContent = new MultipartFormDataContent();
            var fsContent = new StreamContent(File.OpenRead(filePath));
            multiPartFormContent.Add(fsContent, name: "file", fileName: filePath);
            var response = await _httpClient.PostAsync(uri, multiPartFormContent);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading backup to Home Assistant");
            _telemetryManager.TrackException(ex);
            return false;
        }

        return true;
    }

    public async Task SendPersistentNotificationAsync(string message, string? notificationId = null)
    {
        try
        {
            var uri = new Uri(HassBaseUri + "/services/persistent_notification/create");
            var payload = new { message, title = "hassio-onedrive-backup", notification_id = notificationId };
            string payloadStr = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var response = await _httpClient.PostAsync(uri, new StringContent(payloadStr, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed sending persistent notification");
        }
    }

    public async Task<List<Addon>> GetAddonsAsync()
    {
        var uri = new Uri(SupervisorBaseUri + "/addons");
        var response = await GetJsonResponseAsync<HassAddonsResponse>(uri);
        return response.DataProperty.Addons.ToList();
    }

    public async Task UpdateHassEntityStateAsync(string entityId, string payload)
    {
        var uri = new Uri(HassBaseUri + $"/states/{entityId}");
        await _httpClient.PostAsync(uri, new StringContent(payload, Encoding.UTF8, "application/json"));
    }

    public async Task PublishEventAsync(OneDriveEvents eventType, string payload = "")
    {
        var uri = new Uri(HassBaseUri + $"/events/onedrive.{eventType}");
        await _httpClient.PostAsync(uri, new StringContent(payload, Encoding.UTF8, "application/json"));
    }

    public async Task<Backup> DownloadBackupAsync(string backupSlug)
    {
        _logger.LogInformation("Fetching local backup (Slug: {Slug})", backupSlug);
        var uri = new Uri(SupervisorBaseUri + $"/backups/{backupSlug}/download");
        var fileInfo = new FileInfo($"{LocalStorage.TempFolder}/{backupSlug}.tar");
        try
        {
            await using var stream = await _httpClient.GetStreamAsync(uri);
            using var fileStream = File.Create(fileInfo.FullName);
            await stream.CopyToAsync(fileStream);
        }
        catch
        {
            if (File.Exists(fileInfo.FullName))
                File.Delete(fileInfo.FullName);
            throw;
        }

        _logger.LogInformation("Backup {Slug} fetched successfully", backupSlug);

        var backups = await GetBackupsAsync(b => b.Slug == backupSlug);
        var backup = backups.FirstOrDefault() ?? new Backup { Slug = backupSlug };
        backup.LocalPath = fileInfo.FullName;
        return backup;
    }

    public async Task<string> GetTimeZoneAsync()
    {
        var uri = new Uri(SupervisorBaseUri + "/supervisor/info");
        var response = await GetJsonResponseAsync<HassSupervisorInfoResponse>(uri);
        return response.DataProperty.Timezone;
    }

    public async Task<HassAddonInfoResponse> GetAddonInfo(string slug)
    {
        var uri = new Uri(SupervisorBaseUri + $"/addons/{slug}/info");
        return await GetJsonResponseAsync<HassAddonInfoResponse>(uri);
    }

    public async Task RestartSelf()
    {
        var uri = new Uri(SupervisorBaseUri + "/addons/self/restart");
        await _httpClient.PostAsync(uri, null);
    }

    public async Task<bool> IsBackupManagerJobInProgressAsync()
    {
        try
        {
            var uri = new Uri(SupervisorBaseUri + "/jobs");
            var response = await GetJsonResponseAsync<HassJobsResponse>(uri);
            return response?.Data?.Jobs?.Any(j => !j.Done &&
                j.Name?.StartsWith("backup_manager", StringComparison.OrdinalIgnoreCase) == true) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check supervisor jobs — assuming no job in progress");
            return false;
        }
    }

    public async Task<HassBackupInfoResponse?> GetBackupInfoAsync(string slug)
    {
        try
        {
            var uri = new Uri(SupervisorBaseUri + $"/backups/{slug}/info");
            return await GetJsonResponseAsync<HassBackupInfoResponse>(uri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch backup info for slug {Slug}", slug);
            return null;
        }
    }

    private async Task<T> GetJsonResponseAsync<T>(Uri uri)
    {
        var response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(content)!;
    }
}
