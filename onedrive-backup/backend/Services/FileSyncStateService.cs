namespace HassioOneDriveBackup.Services;

public class FileSyncStateService
{
    private readonly object _lock = new();
    private string _state = "Idle";
    private DateTime? _lastSyncTime;

    public void SetSyncing()
    {
        lock (_lock) _state = "Syncing";
    }

    public void SetSynced()
    {
        lock (_lock)
        {
            _state = "Synced";
            _lastSyncTime = DateTime.UtcNow;
        }
    }

    public (string State, DateTime? LastSyncTime) GetStatus()
    {
        lock (_lock) return (_state, _lastSyncTime);
    }
}
