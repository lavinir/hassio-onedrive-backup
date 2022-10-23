using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using Newtonsoft.Json;
using System;

namespace hassio_onedrive_backup
{
    internal class Program
    {
        private const string clientId = "b8a647cf-eccf-4c7f-a0a6-2cbec5d0b94d";
        private static readonly List<string> scopes = new() { "Files.ReadWrite.AppFolder" };

        static async Task Main(string[] args)
        {
            Directory.SetCurrentDirectory("/data");
            // Console.WriteLine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".IdentityService"));
            //Console.WriteLine($"Current Path : {AppDomain.CurrentDomain.BaseDirectory}");
            //string rootDirs = String.Join(';', Directory.EnumerateDirectories("/"));
            //Console.WriteLine($"Root Dirs: {rootDirs}");
            //string dataFiles = string.Join(',', Directory.EnumerateFiles("/data"));
            //Console.WriteLine($"/data Files : {dataFiles}");
            //if (File.Exists("/data/record.auth"))
            //{
            //    Console.WriteLine(File.ReadAllText("/data/record.auth"));
            //}

            var graphHelper = new GraphHelper(scopes, clientId, (info, cancel) =>
            {
                Console.WriteLine(info.Message);
                return Task.FromResult(0);
            });

            string supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN")!;
            Console.WriteLine($"Supervisor Token : {supervisorToken}");
            var hassIoClient = new HassioClient(supervisorToken);

            await hassIoClient.GetAuth();

            //string token = await graphHelper.GetUserTokenAsync();
            //Console.WriteLine($"Got Token: {token}");

            //var backups = await hassIoClient.GetBackupsAsync();
            //Console.WriteLine($"Got {backups.Length} Backups");
            //string backupFileToUpload = hassIoClient.GetBackupFilePath(backups.First());
            //await graphHelper.UploadFileAsync(backupFileToUpload, $"{backups.First().Name}_{Guid.NewGuid()}.tar");
            //foreach (var backup in backups)
            //{
            //    string backupStr = JsonConvert.SerializeObject(backup);
            //    Console.WriteLine(backupStr);
            //}


            // await graphHelper.UploadFile(@"C:\Users\nirlavi\Downloads\ms.web.azuresynapse.net.har");
        }
    }
}