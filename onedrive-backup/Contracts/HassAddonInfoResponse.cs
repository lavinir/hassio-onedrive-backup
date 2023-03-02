using Newtonsoft.Json;

namespace onedrive_backup.Contracts
{
    public class HassAddonInfoResponse
    {
        [JsonProperty("data")]
        public Data DataProperty { get; set; }

        public class Data
        {
            [JsonProperty("ingress_entry")]
            public string IngressEntry { get; set; }

            [JsonProperty("ingress_url")]
            public string IngressUrl { get; set; }
        }
    }
}
