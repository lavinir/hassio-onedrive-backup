namespace hassio_onedrive_backup.Contracts
{
    internal class SyncFileData
    {
        public SyncFileData(string path, string hash, long size)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Size = size;
        }

        public string Path { get; }

        public string Hash { get; }

        public long Size { get; }
    }
}
