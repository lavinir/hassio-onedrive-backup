using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HassioOneDriveBackup.Services
{
    internal class LocalStorage
    {
        public const string TempFolder = "../tmp";
        private const string oldTempFolder = "./tmp";
        private const string configFolder = "/config/";

        public static void InitializeStorage(ILogger<LocalStorage> logger)
        {
            // Legacy cleanup
            if (Directory.Exists(oldTempFolder))
            {
                logger.LogDebug($"Deleting deprecated temp storage folder");
                Directory.Delete(oldTempFolder, true);
            }

            // Clear temporary storage
            if (Directory.Exists(TempFolder))
            {
                if (Directory.EnumerateFiles($"{TempFolder}").Any())
                {
                    logger.LogDebug("Cleaning up temporary artifcats");
                }

                Directory.Delete(TempFolder, true); 
            }

            string onlineBackupsMetadataFolder = Path.Combine(configFolder, "onlineBackupsData/");
            if (Directory.Exists(onlineBackupsMetadataFolder) == false)
            {
                Directory.CreateDirectory(onlineBackupsMetadataFolder);
            }

            // (Re)Create temporary directory
            Directory.CreateDirectory(TempFolder);
        }
    }
}