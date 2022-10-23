using hassio_onedrive_backup.Contracts;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class HassioClient
    {
        private const string Supervisor_Base_Uri_Str = "http://supervisor";
        private const string Hass_Base_Uri_Str = "http://supervisor/core/api";
        private readonly HttpClient _httpClient;

        public HassioClient(string token)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
        }

        public async Task<Backup[]> GetBackupsAsync()
        {
            Uri uri = new Uri(Supervisor_Base_Uri_Str + "/backups");
            string rawResponse = await _httpClient.GetStringAsync(uri);
            Console.WriteLine($"Raw Response : { rawResponse}");
            var response = await GetJsonResponseAsync<HassBackupsResponse>(new Uri(Supervisor_Base_Uri_Str + "/backups"));
            if (response.Result.Equals("ok", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new InvalidOperationException($"Failed getting Backups from Supervisor. Result: {response.Result}");
            }

            return response.DataProperty.Backups;
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
                ConsoleLogger.LogInfo("Starting Full Local Backup");
                await _httpClient.PostAsync(uri, new StringContent(payloadStr, Encoding.UTF8, "application/json"));
                ConsoleLogger.LogInfo("Backup Complete");
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Failed Creating New Backup. {ex}");
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
                    data = new
                    {
                        message = message,
                        title = "hassio-onedrive-backup"
                    }
                };

                string payloadStr = JsonConvert.SerializeObject(payload);
                await _httpClient.PostAsync(uri, new StringContent(payloadStr, Encoding.UTF8, "application/json"));
            }
            catch (Exception ex)
            {
                ConsoleLogger.LogError($"Failed Sending Persistent Notification. {ex}");
            }
        }

        private async Task<T> GetJsonResponseAsync<T>(Uri uri) 
        { 
            string response = await _httpClient.GetStringAsync(uri);
            T ret = JsonConvert.DeserializeObject<T>(response)!;
            return ret;
        }
    }
}
