using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HassioOneDriveBackup.Services
{
    internal class LocalStorage
    {
        public const string TempFolder = "../tmp";
        private const string oldTempFolder = "./tmp";
        private const string defaultConfigFolder = "/config/";

        public static string ConfigFolder { get; private set; } = defaultConfigFolder;

        public static void InitializeStorage(IConfiguration configuration, ILogger<LocalStorage> logger)
        {
            ConfigFolder = configuration["ConfigFolder"] ?? defaultConfigFolder;
            logger.LogDebug("Using config folder: {ConfigFolder}", ConfigFolder);

            // Legacy cleanup
            if (Directory.Exists(oldTempFolder))
            {
                logger.LogDebug("Deleting deprecated temp storage folder");
                Directory.Delete(oldTempFolder, true);
            }

            // Clear temporary storage
            if (Directory.Exists(TempFolder))
            {
                if (Directory.EnumerateFiles(TempFolder).Any())
                    logger.LogDebug("Cleaning up temporary artifacts");

                Directory.Delete(TempFolder, true);
            }

            string onlineBackupsMetadataFolder = Path.Combine(ConfigFolder, "onlineBackupsData/");
            if (!Directory.Exists(onlineBackupsMetadataFolder))
                Directory.CreateDirectory(onlineBackupsMetadataFolder);

            // (Re)Create temporary directory
            Directory.CreateDirectory(TempFolder);
        }
    }
}