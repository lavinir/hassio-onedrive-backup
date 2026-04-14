using Newtonsoft.Json;

namespace HassioOneDriveBackup.Models;

public class HassJobsResponse
{
    [JsonProperty("result")]
    public string? Result { get; set; }

    [JsonProperty("data")]
    public JobsData? Data { get; set; }

    public class JobsData
    {
        [JsonProperty("jobs")]
        public Job[]? Jobs { get; set; }
    }

    public class Job
    {
        [JsonProperty("job_id")]
        public string? JobId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("done")]
        public bool Done { get; set; }

        [JsonProperty("progress")]
        public float Progress { get; set; }
    }
}

public class HassBackgroundJobResponse
{
    [JsonProperty("result")]
    public string? Result { get; set; }

    [JsonProperty("data")]
    public JobData? Data { get; set; }

    public class JobData
    {
        [JsonProperty("job_id")]
        public string? JobId { get; set; }
    }
}

public class HassJobStatusResponse
{
    [JsonProperty("result")]
    public string? Result { get; set; }

    [JsonProperty("data")]
    public HassJobsResponse.Job? Data { get; set; }
}
