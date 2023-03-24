namespace hassio_onedrive_backup.Storage
{
    internal class LocalStorage
    {
        public const string TempFolder = "../tmp";
        private const string oldTempFolder = "./tmp";

        public static void InitializeTempStorage()
        {
            // Legacy cleanup
            if (Directory.Exists(oldTempFolder))
            {
                ConsoleLogger.LogVerbose($"Deleting deprecated temp storage folder");
                Directory.Delete(oldTempFolder, true);
            }

            // Clear temporary storage
            if (Directory.Exists(TempFolder))
            {
                if (Directory.EnumerateFiles($"{TempFolder}").Any())
                {
                    ConsoleLogger.LogVerbose("Cleaning up temporary artifcats");
                }

                Directory.Delete(TempFolder, true); 
            }

            // (Re)Create temporary directory
            Directory.CreateDirectory(TempFolder);
        }
    }
}
