using hassio_onedrive_backup.Contracts;
using Microsoft.Graph;
using Newtonsoft.Json;
using System.Formats.Asn1;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
            var response = await GetJsonResponseAsync<HassBackupsResponse>(new Uri(Supervisor_Base_Uri_Str + "/backups"));
            if (response.Result.Equals("ok", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new InvalidOperationException($"Failed getting Backups from Supervisor. Result: {response.Result}");
            }

            var backups = response.DataProperty.Backups;

            return filter != null ? backups.Where(backup => filter(backup)).ToList() : backups.ToList();
        }

        public async Task<bool> CreateBackupAsync(string backupName, bool compressed = true, string? password = null)
        {
            Uri uri = new Uri(Supervisor_Base_Uri_Str + "/backups/new/full");
            var payload = new
            {
                name = backupName,
                compressed = compressed,
                password = password
            };

            string payloadStr = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            try
            {
                ConsoleLogger.LogInfo("Starting full local backup");
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

        public async Task UpdateHassEntityState(string entityId, string payload)
        {
            Uri uri = new Uri(Hass_Base_Uri_Str + $"/states/{entityId}");
            await _httpClient.PostAsync(uri, new StringContent(payload, Encoding.UTF8, "application/json"));
        }

        public async Task<string> DownloadBackup(string backupSlug)
        {            
            ConsoleLogger.LogInfo($"Fetching Local Backup (Slug:{backupSlug})");
            Uri uri = new Uri(Supervisor_Base_Uri_Str + $"/backups/{backupSlug}/download");
            var fileInfo = new FileInfo($"{backupSlug}.tar");
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
