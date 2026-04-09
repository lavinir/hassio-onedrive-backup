using Newtonsoft.Json;

namespace hassio_onedrive_backup.Contracts
{
    public class HassJobsResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("data")]
        public JobsData DataProperty { get; set; }

        public class JobsData
        {
            [JsonProperty("jobs")]
            public Job[] Jobs { get; set; }
        }

        public class Job
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("done")]
            public bool Done { get; set; }
        }
    }
}
