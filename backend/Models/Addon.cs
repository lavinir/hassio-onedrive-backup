using Newtonsoft.Json;

namespace HassioOneDriveBackup.Models;

public class Addon
{
    [JsonProperty("slug")]
    public string Slug { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}
