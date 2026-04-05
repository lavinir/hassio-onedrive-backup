using System.Text.Json.Serialization;

namespace HassioOneDriveBackup.Models
{
    public class HassBackupsResponse
    {
        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("data")]
        public Data DataProperty { get; set; }

        public class Data
        {
            [JsonPropertyName("backups")]
            public Backup[] Backups { get; set; }
        }
    }
}