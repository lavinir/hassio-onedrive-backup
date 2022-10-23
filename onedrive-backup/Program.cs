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
            Directory.SetCurrentDirectory(addonDirectory);
            var graphHelper = new GraphHelper(scopes, clientId, (info, cancel) =>
            {
                Console.WriteLine(info.Message);
                return Task.FromResult(0);
            });

            string supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN")!;
            var hassIoClient = new HassioClient(supervisorToken);
            var addonOptions = AddonOptionsReader.ReadOptions();
            var backupManager = new BackupManager(addonOptions, graphHelper, hassIoClient);
            await hassIoClient.SendPersistentNotificationAsync("Test Notification");

            while (true)
            {
                try
                {
                    // Refresh Graph Token
                    await graphHelper.GetUserTokenAsync();

                    ConsoleLogger.LogInfo("Checking backups");
                    await backupManager.PerformBackups();
                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Unexpected Error. {ex}");
                }

            }
        }
    }
}