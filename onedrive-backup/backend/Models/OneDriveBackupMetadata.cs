using System.Text;
using System.Text.Json;

namespace HassioOneDriveBackup.Models;

public class OneDriveBackupMetadata
{
    public string? Name { get; set; }
    public DateTime? Date { get; set; }
    public long? Size { get; set; }

    private static readonly JsonSerializerOptions _compact = new() { WriteIndented = false };

    public string Encode()
    {
        var json = JsonSerializer.Serialize(this, _compact);
        return Base64UrlEncode(Encoding.UTF8.GetBytes(json));
    }

    public static OneDriveBackupMetadata? TryDecode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        try
        {
            var bytes = Base64UrlDecode(encoded);
            return JsonSerializer.Deserialize<OneDriveBackupMetadata>(bytes);
        }
        catch
        {
            return null;
        }
    }

    // RFC 4648 §5 Base64URL — no padding, uses - and _ instead of + and /
    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string encoded)
    {
        var base64 = encoded
            .Replace('-', '+')
            .Replace('_', '/');

        // Re-add padding
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64
        };

        return Convert.FromBase64String(base64);
    }
}
