using Newtonsoft.Json;

namespace HassioOneDriveBackup.Models
{
    public class HassBackupsResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("data")]
        public Data DataProperty { get; set; }

        public class Data
        {
            [JsonProperty("backups")]
            public Backup[] Backups { get; set; }
        }
    }
}