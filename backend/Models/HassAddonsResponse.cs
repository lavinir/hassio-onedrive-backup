using System.Text.Json.Serialization;

namespace HassioOneDriveBackup.Models
{
    public class HassAddonsResponse
    {
        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("data")]
        public Data DataProperty { get; set; }

        public class Data
        {
            [JsonPropertyName("addons")]
            public Addon[] Addons { get; set; }
        }
    }
}