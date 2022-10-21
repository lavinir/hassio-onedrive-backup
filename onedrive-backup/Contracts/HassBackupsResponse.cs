using Newtonsoft.Json;

namespace hassio_onedrive_backup.Contracts
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

        public class Backup
        {
            [JsonProperty("slug")]
            public string Slug { get; set; }

            [JsonProperty("date")]
            public DateTime Date { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("size")]
            public float Size { get; set; }

            [JsonProperty("protected")]
            public bool Protected { get; set; }

            [JsonProperty("compressed")]
            public bool Compressed { get; set; }

            [JsonProperty("content")]
            public Content Content { get; set; }
        }

        public class Content
        {
            [JsonProperty("homeassistant")]
            public bool Homeassistant { get; set; }

            [JsonProperty("addons")]
            public string[] Addons { get; set; }

            [JsonProperty("folders")]
            public string[] Folders { get; set; }
        }
    }

}
