using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using hassio_onedrive_backup.Storage;

namespace hassio_onedrive_backup
{
    internal class Program
    {
        private const string clientId = "b8a647cf-eccf-4c7f-a0a6-2cbec5d0b94d";
        private const string addonDirectory = "/data";
        private static readonly List<string> scopes = new() { "Files.ReadWrite.AppFolder" };

        static async Task Main(string[] args)
        {
            var addonOptions = AddonOptionsReader.ReadOptions();

#if DEBUG
            IHassioClient hassIoClient = new HassioClientMock();
#else
            Directory.SetCurrentDirectory(addonDirectory);
            string supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN")!;
            IHassioClient hassIoClient = new HassioClient(supervisorToken, TimeSpan.FromMinutes(addonOptions.HassAPITimeoutMinutes));
#endif

            IGraphHelper graphHelper = new GraphHelper(scopes, clientId, (info, cancel) =>
            {
                ConsoleLogger.LogInfo(info.Message);
                return Task.FromResult(0);
            });

            var backupManager = new BackupManager(addonOptions, graphHelper, hassIoClient);
            int syncIntervalHours = addonOptions.SyncIntervalHours > addonOptions.BackupIntervalHours ? 1 : addonOptions.SyncIntervalHours;
            TimeSpan intervalDelay = TimeSpan.FromHours(syncIntervalHours);

            if (addonOptions.RecoveryMode)
            {
                ConsoleLogger.LogInfo($"Addon Started in Recovery Mode! Any existing backups in OneDrive will be synced locally and no addiitonal local backups will be created");
            }
            else
            {
                ConsoleLogger.LogInfo($"Backup interval configured to every {addonOptions.BackupIntervalHours} hours");
            }

            while (true)
            {
                try
                {
                    // Refresh Graph Token
                    await graphHelper.GetAndCacheUserTokenAsync();

                    // Update OneDrive Freespace Sensor
                    int? freeSpaceGB = await graphHelper.GetFreeSpaceInGB();
                    await HassOnedriveFreeSpaceEntityState.UpdateOneDriveFreespaceSensorInHass(freeSpaceGB, hassIoClient);

                    if (addonOptions.RecoveryMode)
                    {
                        await backupManager.DownloadCloudBackupsAsync();
                        Console.WriteLine();
                        //string? fileName = await graphHelper.DownloadFileAsync("Ferdinand.2017.720p.BluRay.x264-DRONES-HebDub-WWW.MoriDim.tv.mkv", (prog) =>
                        //{
                        //    Console.WriteLine($"{prog}%");
                        //});
                        // todo: start recovery mode
                    }
                    else
                    {
                        ConsoleLogger.LogInfo("Checking backups");
                        await backupManager.PerformBackupsAsync();
                    }

                    ConsoleLogger.LogInfo("Interval Completed.");
                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Unexpected error. {ex}");
                }

                await Task.Delay(intervalDelay);
            }
        }
    }
}
