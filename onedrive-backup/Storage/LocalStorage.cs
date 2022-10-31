namespace hassio_onedrive_backup.Storage
{
    internal class LocalStorage
    {
        public const string TempFolder = "./tmp";

        public static void InitializeTempStorage()
        {
            // Clear temporary storage
            if (Directory.Exists(TempFolder))
            {
                if (Directory.EnumerateFiles($"{TempFolder}").Any())
                {
                    ConsoleLogger.LogInfo("Cleaning up temporary artifcats");
                }

                Directory.Delete(TempFolder, true); 
            }

            // (Re)Create temporary directory
            Directory.CreateDirectory(TempFolder);
        }
    }
}
