using System.Text.Json.Serialization;

namespace HassioOneDriveBackup.Models;

public class HassJobsResponse
{
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("data")]
    public JobsData? Data { get; set; }

    public class JobsData
    {
        [JsonPropertyName("jobs")]
        public Job[]? Jobs { get; set; }
    }

    public class Job
    {
        [JsonPropertyName("job_id")]
        public string? JobId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("progress")]
        public float Progress { get; set; }
    }
}

public class HassBackgroundJobResponse
{
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("data")]
    public JobData? Data { get; set; }

    public class JobData
    {
        [JsonPropertyName("job_id")]
        public string? JobId { get; set; }
    }
}

public class HassJobStatusResponse
{
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("data")]
    public HassJobsResponse.Job? Data { get; set; }
}
