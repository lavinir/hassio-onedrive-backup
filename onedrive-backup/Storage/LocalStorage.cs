using hassio_onedrive_backup.Contracts;
using Microsoft.Extensions.Logging;
using onedrive_backup.Contracts;
using System.Text.Json;

namespace hassio_onedrive_backup.Storage
{
    internal class LocalStorage
    {
        public const string TempFolder = "../tmp";
        private const string oldTempFolder = "./tmp";
        private const string onlineBackupsDataFolder = "onlineBackupsData";

#if DEBUG
        private const string configFolder = "./";
        private static string configFilePath = Path.Combine(configFolder, "additionalBackupData.json");
#else
        private const string configFolder = "/config/";
        private static string configFilePath = Path.Combine(configFolder, "additionalBackupData.json");
#endif

        private static HashSet<Flag> setFlags = new();

        public static void InitializeStorage(ConsoleLogger logger)
        {
            // Legacy cleanup
            if (Directory.Exists(oldTempFolder))
            {
                logger.LogVerbose($"Deleting deprecated temp storage folder");
                Directory.Delete(oldTempFolder, true);
            }

            // Clear temporary storage
            if (Directory.Exists(TempFolder))
            {
                if (Directory.EnumerateFiles($"{TempFolder}").Any())
                {
                    logger.LogVerbose("Cleaning up temporary artifcats");
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

        public async static Task StoreBackupData(BackupAdditionalData additionalData)
        {
            if (additionalData == null)
            {
                return;
            }

            await File.WriteAllTextAsync(configFilePath, JsonSerializer.Serialize(additionalData));
        }

        public async static Task<BackupAdditionalData?> LoadBackupAdditionalData()
        {
            try
            {
                if (File.Exists(configFilePath) == false)
                {
                    return null;
                }

                var rawData = await File.ReadAllTextAsync(configFilePath);
                BackupAdditionalData backupAdditionalData = JsonSerializer.Deserialize<BackupAdditionalData>(rawData)!;
                return backupAdditionalData;

            }
            catch (Exception)
            {
                return null;
            }
        }

        public async static Task<OnedriveBackup?> GetOneDriveBackup(string fileName, ConsoleLogger logger)
        {
            try
            {
<<<<<<< HEAD
=======
                logger.LogVerbose($"Checking metadata for backup file: {fileName}");
>>>>>>> dev
                string filePath = Path.Combine(configFolder, onlineBackupsDataFolder, ConvertBackFileNameToMetadataFileName(fileName));
                string serializedData = await File.ReadAllTextAsync(filePath);
                OnedriveBackup onedriveBackup = JsonSerializer.Deserialize<OnedriveBackup>(serializedData);
                return onedriveBackup;
            }
            catch (FileNotFoundException)
            {
                logger.LogVerbose($"Metadata file for {fileName} not found");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error loading metadata for file: {fileName}", ex);
                return null;
            }
        }

        public async static Task<bool> AddOneDriveBackup(OnedriveBackup onedriveBackup)
        {
            string filePath = Path.Combine(configFolder, onlineBackupsDataFolder, ConvertBackFileNameToMetadataFileName(onedriveBackup.FileName));
            try
            {
                string serializedData = JsonSerializer.Serialize<OnedriveBackup>(onedriveBackup);
                await File.WriteAllTextAsync(filePath, serializedData);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool DeleteOneDriveBackup(OnedriveBackup onedriveBackup)
        {
            string filePath = Path.Combine(configFolder, onlineBackupsDataFolder, ConvertBackFileNameToMetadataFileName(onedriveBackup.FileName));
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool CheckAndMarkFlag(Flag flag)
        {
            string fileName = $"./.{flag}";
            if (setFlags.Contains(flag) || File.Exists(fileName))
            {
                return true;
            }

            File.Create(fileName);
            setFlags.Add(flag);
            return false;        
        }

        public enum Flag
        {
<<<<<<< HEAD
            ReleaseNotes_2_3_7,
=======
            ReleaseNotes_2_3_8,
>>>>>>> dev
        }

        private static string ConvertBackFileNameToMetadataFileName(string fileName)
        {
            return Path.ChangeExtension(fileName, ".aux");
        }
    }
}
