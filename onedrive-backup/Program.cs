using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using hassio_onedrive_backup.Storage;
using System.Collections;

namespace hassio_onedrive_backup
{
    internal class Program
    {
        private const string clientId = "b8a647cf-eccf-4c7f-a0a6-2cbec5d0b94d";
        private const string addonDirectory = "/data";
        private static readonly List<string> scopes = new() { "Files.ReadWrite.AppFolder" };

        static async Task Main(string[] args)
        {

#if DEBUG
            IHassioClient hassIoClient = new HassioClientMock();
            var addonOptions = AddonOptionsReader.ReadOptions();
#else
            Directory.SetCurrentDirectory(addonDirectory);
            var addonOptions = AddonOptionsReader.ReadOptions();
            string supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN")!;
            IHassioClient hassIoClient = new HassioClient(supervisorToken, TimeSpan.FromMinutes(addonOptions.HassAPITimeoutMinutes));
#endif
            LocalStorage.InitializeTempStorage();
            IGraphHelper graphHelper = new GraphHelper(scopes, clientId, (info, cancel) =>
            {
                ConsoleLogger.LogInfo(info.Message);
                return Task.FromResult(0);
            });

            string timeZoneId = await hassIoClient.GetTimeZoneAsync();
            DateTimeHelper.Initialize(timeZoneId);

            BitArray allowedBackupHours = TimeRangeHelper.GetAllowedHours(addonOptions.BackupAllowedHours);
            var backupManager = new BackupManager(addonOptions, graphHelper, hassIoClient, allowedBackupHours);
            
            TimeSpan intervalDelay = TimeSpan.FromMinutes(5);

            if (addonOptions.RecoveryMode)
            {
                ConsoleLogger.LogInfo($"Addon Started in Recovery Mode! Any existing backups in OneDrive will be synced locally. No additional local backups will be created. No other syncing will occur.");
            }
            else
            {
                ConsoleLogger.LogInfo($"Backup interval configured to every {addonOptions.BackupIntervalHours} hours");
                if (string.IsNullOrWhiteSpace(addonOptions.BackupAllowedHours) == false)
                {
                    ConsoleLogger.LogInfo($"Backups will only run during these hours: {allowedBackupHours.ToAllowedHoursText()}");
                }
            }

            while (true)
            {
                try
                {
                    // Refresh Graph Token
                    await graphHelper.GetAndCacheUserTokenAsync();

                    // Update OneDrive Freespace Sensor
                    double? freeSpaceGB = await graphHelper.GetFreeSpaceInGB();
                    await HassOnedriveFreeSpaceEntityState.UpdateOneDriveFreespaceSensorInHass(freeSpaceGB, hassIoClient);
                    ConsoleLogger.LogInfo("Checking backups");

                    if (addonOptions.RecoveryMode)
                    {
                        await backupManager.DownloadCloudBackupsAsync();
                        Console.WriteLine();
                    }
                    else
                    {
                        await backupManager.PerformBackupsAsync();
                    }

                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Unexpected error. {ex}");
                }

                if (addonOptions.RecoveryMode)
                {
                    ConsoleLogger.LogInfo("Recovery run done. New scan will begin in 10 minutes");
                    ConsoleLogger.LogInfo($"To switch back to Normal backup mode please stop the addon, disable Recovery_Mode in the configuration and restart");
                    await Task.Delay(TimeSpan.FromMinutes(10));
                }
                else
                {
                    ConsoleLogger.LogInfo("Interval Completed.");
                    await Task.Delay(intervalDelay);
                }
            }
        }
    }
}
