using hassio_onedrive_backup.Contracts;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using static hassio_onedrive_backup.Contracts.HassBackupsResponse;

namespace hassio_onedrive_backup.Hass
{
    internal class HassioClient
    {
        private const string Base_Uri_Str = "http://supervisor";
        private readonly HttpClient _httpClient;

        public HassioClient(string token)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Add("X-Supervisor-Token", token);
        }

        public async Task<string> GetAuth()
        {
            Uri uri = new Uri(Base_Uri_Str + "/auth");
            string rawResponse = await _httpClient.GetStringAsync(uri);
            Console.WriteLine($"Raw Auth Response : {rawResponse}");
            return rawResponse;
        }

        public async Task<Backup[]> GetBackupsAsync()
        {
            Uri uri = new Uri(Base_Uri_Str + "/backups");
            string rawResponse = await _httpClient.GetStringAsync(uri);
            Console.WriteLine($"Raw Response : { rawResponse}");
            var response = await GetJsonResponseAsync<HassBackupsResponse>(new Uri(Base_Uri_Str + "/backups"));
            if (response.Result.Equals("ok", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new InvalidOperationException($"Failed getting Backups from Supervisor. Result: {response.Result}");
            }

            return response.DataProperty.Backups;
        }


        private async Task<T> GetJsonResponseAsync<T>(Uri uri) 
        { 
            string response = await _httpClient.GetStringAsync(uri);
            T ret = JsonConvert.DeserializeObject<T>(response)!;
            return ret;
        }
    }
}
