using Newtonsoft.Json;

namespace hassio_onedrive_backup.Contracts
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

        public class Addon
        {
            [JsonProperty("slug")]
            public string Slug { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }

    }
}
