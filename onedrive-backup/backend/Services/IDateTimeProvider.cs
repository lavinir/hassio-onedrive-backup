namespace HassioOneDriveBackup.Services;

public interface IDateTimeProvider
{
    /// <summary>
    /// Current time in the Home Assistant configured timezone (falls back to local system time).
    /// Use this for all scheduling and allowed-hours decisions.
    /// </summary>
    DateTime Now { get; }
}
