using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Storage;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class HassioClient : IHassioClient
    {
        private const string Supervisor_Base_Uri_Str = "http://supervisor";
        private const string Hass_Base_Uri_Str = "http://supervisor/core/api";
        private readonly HttpClient _httpClient;

        public HassioClient(string token, TimeSpan clientTimeout) 
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.Timeout = clientTimeout;
        }

        public async Task<bool> DeleteBackupAsync(Backup backup)
        {
            try
            {
                Uri uri = new Uri(Supervisor_Base_Uri_Str + $"/backups/{backup.Slug}");
                await _httpClient.DeleteAsync(uri);

            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Error deleting backup {backup.Slug}. {ex}");
                return false;
            }

            return true;
        }

        public async Task<List<Backup>> GetBackupsAsync(Predicate<Backup> filter)
        {
            Uri uri = new Uri(Supervisor_Base_Uri_Str + "/backups");
            var response = await GetJsonResponseAsync<HassBackupsResponse>(uri);
            if (response.Result.Equals("ok", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new InvalidOperationException($"Failed getting Backups from Supervisor. Result: {response.Result}");
            }

            var backups = response.DataProperty.Backups;

            return filter != null ? backups.Where(backup => filter(backup)).ToList() : backups.ToList();
        }

        public async Task<bool> CreateBackupAsync(string backupName, bool appendTimestamp = true, bool compressed = true, string? password = null, IEnumerable<string>? folders = null, IEnumerable<string>? addons = null)
        {
            DateTime timeStamp = DateTime.Now;
            string? payloadStr = null;
            Uri? uri = null;

            // Full Backup
            if (folders == null && addons == null)
            {
                uri = new Uri(Supervisor_Base_Uri_Str + "/backups/new/full");
                string finalBackupName = appendTimestamp ? $"{backupName}_{timeStamp.ToString("yyyy-MM-dd-HH-mm")}" : backupName;
                var fullPayload = new
                {
                    name = finalBackupName,
                    compressed = compressed,
                    password = password
                };

                payloadStr = JsonConvert.SerializeObject(fullPayload, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                ConsoleLogger.LogInfo("Starting full local backup");
            }
            // Partial Backup
            else
            {
                uri = new Uri(Supervisor_Base_Uri_Str + "/backups/new/partial");
                string finalBackupName = appendTimestamp ? $"{backupName}_{timeStamp.ToString("yyyy-MM-dd-HH-mm")}" : backupName;
                var partialPayload = new
                {
                    name = finalBackupName,
                    compressed = compressed,
                    password = password,
                    homeassistant = true,
                    addons = addons,
                    folders = folders
                };

                payloadStr = JsonConvert.SerializeObject(partialPayload, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                ConsoleLogger.LogInfo("Starting partial local backup");
            }

            try
            {
                await _httpClient.PostAsync(uri, new StringContent(payloadStr, Encoding.UTF8, "application/json"));
                ConsoleLogger.LogInfo("Backup complete");
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Failed creating new backup. {ex}");
                return false;
            }
                     
            return true;
        }

        public async Task<bool> UploadBackupAsync(string filePath)
        {
            try
            {
                Uri uri = new Uri(Supervisor_Base_Uri_Str + "/backups/new/upload");
                using var multiPartFormContent = new MultipartFormDataContent();
                var fsContent = new StreamContent(System.IO.File.OpenRead(filePath));
                multiPartFormContent.Add(fsContent, name: "file", fileName: filePath);
                var response = await _httpClient.PostAsync(uri, multiPartFormContent);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Error uploading backup to Home Assistant. {ex}");
                return false;
            }

            return true;
        }

        public async Task SendPersistentNotificationAsync(string message)
        {
            try
            {
                Uri uri = new Uri(Hass_Base_Uri_Str + "/services/notify/persistent_notification");
                var payload = new
                {
                    message = message,
                    title = "hassio-onedrive-backup"
                };

                string payloadStr = JsonConvert.SerializeObject(payload);
                await _httpClient.PostAsync(uri, new StringContent(payloadStr, Encoding.UTF8, "application/json"));
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Failed sending persistent notification. {ex}");
            }
        }

        public async Task<List<string>> GetAddons()
        {
            Uri uri = new Uri(Supervisor_Base_Uri_Str + "/addons");
            var response = await GetJsonResponseAsync<HassAddonsResponse>(uri);
            var ret = response.DataProperty.Addons.Select(addon => addon.Slug).ToList();
            return ret;
        }

        public async Task UpdateHassEntityState(string entityId, string payload)
        {
            Uri uri = new Uri(Hass_Base_Uri_Str + $"/states/{entityId}");
            await _httpClient.PostAsync(uri, new StringContent(payload, Encoding.UTF8, "application/json"));
        }

        public async Task<string> DownloadBackup(string backupSlug)
        {            
            ConsoleLogger.LogInfo($"Fetching Local Backup (Slug:{backupSlug})");
            Uri uri = new Uri(Supervisor_Base_Uri_Str + $"/backups/{backupSlug}/download");
            var fileInfo = new FileInfo($"{LocalStorage.TempFolder}/{backupSlug}.tar");
            await using var memStream =  await _httpClient.GetStreamAsync(uri);
            using var fileStream = System.IO.File.Create(fileInfo.FullName);
            await memStream.CopyToAsync(fileStream);
            ConsoleLogger.LogInfo($"Backup ({backupSlug}) fetched successfully");
            return fileInfo.FullName;
        }

        private async Task<T> GetJsonResponseAsync<T>(Uri uri) 
        { 
            string response = await _httpClient.GetStringAsync(uri);
            T ret = JsonConvert.DeserializeObject<T>(response)!;
            return ret;
        }

    }
}
