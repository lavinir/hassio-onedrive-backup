using Newtonsoft.Json;

namespace HassioOneDriveBackup.Models
{
    public class HassAddonsResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("data")]
        public Data DataProperty { get; set; }

        public class Data
        {
            [JsonProperty("addons")]
            public Addon[] Addons { get; set; }
        }
    }
}
