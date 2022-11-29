using Newtonsoft.Json;

namespace hassio_onedrive_backup.Contracts
{
    public class HassSupervisorInfoResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("data")]
        public Data DataProperty { get; set; }

        public class Data
        {
            [JsonProperty("timezone")]
            public string Timezone{ get; set; }
        }

    }
}
