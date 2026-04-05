namespace HassioOneDriveBackup.Services;

public class DateTimeProvider : IDateTimeProvider
{
    private readonly IHassioClient _hassioClient;
    private readonly ILogger<DateTimeProvider> _logger;
    private TimeZoneInfo? _timeZone;

    public DateTimeProvider(IHassioClient hassioClient, ILogger<DateTimeProvider> logger)
    {
        _hassioClient = hassioClient;
        _logger = logger;
    }

    public DateTime Now
    {
        get
        {
            _timeZone ??= FetchTimeZone();
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
        }
    }

    private TimeZoneInfo FetchTimeZone()
    {
        try
        {
            // Task.Run ensures execution on a thread-pool thread with no SynchronizationContext,
            // preventing the deadlock that .GetAwaiter().GetResult() would cause in ASP.NET Core.
            var timeZoneId = Task.Run(() => _hassioClient.GetTimeZoneAsync()).GetAwaiter().GetResult();
            _logger.LogInformation("Using Home Assistant timezone: {TimeZone}", timeZoneId);
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get timezone from Home Assistant supervisor, falling back to UTC");
            return TimeZoneInfo.Utc;
        }
    }
}
