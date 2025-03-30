using System.Text.Json.Serialization;

namespace HassioOneDriveBackup.Models;

public class Addon
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}