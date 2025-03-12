using Microsoft.Identity.Client;
using System.Text.Json;

namespace HassioOneDriveBackup.Services;

public class MsalTokenCache
{
    private readonly string _cacheFilePath;
    private readonly ILogger<MsalTokenCache> _logger;
    private readonly object _fileLock = new object();

    public MsalTokenCache(string cacheFilePath, ILogger<MsalTokenCache> logger)
    {
        _cacheFilePath = cacheFilePath;
        _logger = logger;
    }

    public void Initialize(ITokenCache tokenCache)
    {
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        lock (_fileLock)
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    var cacheData = File.ReadAllBytes(_cacheFilePath);
                    args.TokenCache.DeserializeMsalV3(cacheData);
                    _logger.LogDebug("Token cache loaded from file");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading token cache from file");
            }
        }
    }

    private void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        // if the access operation resulted in a cache update
        if (args.HasStateChanged)
        {
            lock (_fileLock)
            {
                try
                {
                    // reflect changes in the persistent store
                    var cacheData = args.TokenCache.SerializeMsalV3();
                    Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
                    File.WriteAllBytes(_cacheFilePath, cacheData);
                    _logger.LogDebug("Token cache saved to file");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing token cache to file");
                }
            }
        }
    }
}