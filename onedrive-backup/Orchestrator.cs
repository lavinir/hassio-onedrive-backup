using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using hassio_onedrive_backup.Sync;
using onedrive_backup;
using onedrive_backup.Graph;
using onedrive_backup.Telemetry;
using System.Collections;

namespace hassio_onedrive_backup
{
    public class Orchestrator
    {
        private readonly IHassioClient _hassIoClient;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly HassOnedriveFreeSpaceEntityState? _hassOnedriveFreeSpaceEntityState;
        private readonly TelemetryManager? _telemetryManager;
        private readonly ConsoleLogger _logger;
        private readonly IGraphHelper _graphHelper;
        private readonly IServiceProvider _serviceProvider;
        private readonly AddonOptions _addonOptions;
        private readonly BitArray _allowedBackupHours;
        private SyncManager _syncManager;
        private bool _enabled = false;

        public Orchestrator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _addonOptions = serviceProvider.GetService<AddonOptions>();
            _graphHelper = serviceProvider.GetService<IGraphHelper>();
            _hassIoClient = serviceProvider.GetService<IHassioClient>();
            _dateTimeProvider = serviceProvider.GetService<IDateTimeProvider>();
            _hassOnedriveFreeSpaceEntityState = serviceProvider.GetService<HassOnedriveFreeSpaceEntityState>();
            _telemetryManager = serviceProvider.GetService<TelemetryManager>();
            _logger = serviceProvider.GetService<ConsoleLogger>();
            _allowedBackupHours = TimeRangeHelper.GetAllowedHours(_addonOptions.BackupAllowedHours);
            BackupManager = new BackupManager(_serviceProvider, new TransferSpeedHelper(null));
            _addonOptions.OnOptionsChanged += OnOptionsChanged;
        }

        public BackupManager BackupManager { get; set; }

        public async Task Start()
        {
            _enabled = true;
            TimeSpan intervalDelay = TimeSpan.FromMinutes(5);
            var lastTelemetrySend = DateTime.MinValue;

            _logger.LogInfo($"Anonymous Telemetry {(_addonOptions.EnableAnonymousTelemetry ? "Enabled" : "Disabled")}");
            _logger.LogInfo($"Backup interval configured to every {_addonOptions.BackupIntervalHours} hours");
            if (_addonOptions.GenerationalBackups)
            {
                _logger.LogInfo($"Generational backups enabled");
            }

            if (string.IsNullOrWhiteSpace(_addonOptions.BackupAllowedHours) == false)
            {
                _logger.LogInfo($"Backups / Syncs will only run during these hours: {_allowedBackupHours.ToAllowedHoursText()}");
            }

            // Initialize File Sync Manager
            var transferSpeedHelper = new TransferSpeedHelper(null);
            _syncManager = new SyncManager(_serviceProvider, _allowedBackupHours, transferSpeedHelper, _logger, _dateTimeProvider);
            var tokenSource = new CancellationTokenSource();
            await _graphHelper.GetAndCacheUserTokenAsync();
            var fileSyncTask = Task.Run(() => _syncManager.SyncLoop(tokenSource.Token), tokenSource.Token);

            while (_enabled)
            {
                try
                {
                    // Telemetry
                    if (_addonOptions.EnableAnonymousTelemetry && DateTime.UtcNow - lastTelemetrySend > TimeSpan.FromHours(24))
                    {
                        _logger.LogVerbose($"Sending Telemetry");
                        await _telemetryManager.SendConfig(_addonOptions);
                        lastTelemetrySend = DateTime.UtcNow;
                    }

                    // Refresh Graph Token
                    await _graphHelper.GetAndCacheUserTokenAsync();

                    // Update OneDrive Freespace Sensor
                    var oneDriveSpace = await _graphHelper.GetFreeSpaceInGB();
                    if (oneDriveSpace != null)
                    {
                        await _hassOnedriveFreeSpaceEntityState.UpdateOneDriveFreespaceSensorInHass(oneDriveSpace);
                    }

                    _logger.LogVerbose("Checking backups");

                    BackupManager.PerformBackupsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Unexpected error. {ex}");
                }

                _logger.LogVerbose("Backup Interval Completed.");
                await Task.Delay(intervalDelay);
            }
        }

        public void Stop()
        {
            _enabled = false;
        }

        private void OnOptionsChanged()
        {
            _hassIoClient.UpdateTimeoutValue(_addonOptions.HassAPITimeoutMinutes);
            _logger.SetLogLevel(_addonOptions.LogLevel);
            _syncManager?.UpdateFileMatcherPaths();
            BackupManager.UpdateAllowedHours(_addonOptions.BackupAllowedHours);
            _addonOptions.SyncPaths.RemoveAll(path => string.IsNullOrWhiteSpace(path));
            _addonOptions.ExcludedAddons.RemoveAll(addon => string.IsNullOrWhiteSpace(addon));
        }
    }
}
