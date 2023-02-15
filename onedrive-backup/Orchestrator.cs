using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using hassio_onedrive_backup.Storage;
using hassio_onedrive_backup.Sync;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup
{
    public class Orchestrator
    {
        private readonly IHassioClient _hassIoClient;
        private readonly IGraphHelper _graphHelper;
        private readonly AddonOptions _addonOptions;

        private bool _enabled = false;

        public Orchestrator(IHassioClient hassIoClient, IGraphHelper graphHelper, AddonOptions addonOptions)
        {
            _hassIoClient = hassIoClient;
            _graphHelper = graphHelper;
            _addonOptions = addonOptions;
        }
        
        public async Task Start()
        {
            _enabled = true;
            string timeZoneId = await _hassIoClient.GetTimeZoneAsync();
            DateTimeHelper.Initialize(timeZoneId);
            TimeSpan intervalDelay = TimeSpan.FromMinutes(5);

            BitArray allowedBackupHours = TimeRangeHelper.GetAllowedHours(_addonOptions.BackupAllowedHours);
            var backupManager = new BackupManager(_addonOptions, _graphHelper, _hassIoClient, allowedBackupHours);

            if (_addonOptions.RecoveryMode)
            {
                ConsoleLogger.LogInfo($"Addon Started in Recovery Mode! Any existing backups in OneDrive will be synced locally. No additional local backups will be created. No other syncing will occur.");
            }
            else
            {
                ConsoleLogger.LogInfo($"Backup interval configured to every {_addonOptions.BackupIntervalHours} hours");
                if (string.IsNullOrWhiteSpace(_addonOptions.BackupAllowedHours) == false)
                {
                    ConsoleLogger.LogInfo($"Backups / Syncs will only run during these hours: {allowedBackupHours.ToAllowedHoursText()}");
                }

                // Initialize File Sync Manager
                if (_addonOptions.FileSyncEnabled)
                {
                    var syncManager = new SyncManager(_addonOptions, _graphHelper, _hassIoClient, allowedBackupHours);
                    var tokenSource = new CancellationTokenSource();
                    await _graphHelper.GetAndCacheUserTokenAsync();
                    var fileSyncTask = Task.Run(() => syncManager.SyncLoop(tokenSource.Token), tokenSource.Token);
                }
            }

            while (_enabled)
            {
                try
                {
                    // Refresh Graph Token
                    await _graphHelper.GetAndCacheUserTokenAsync();

                    // Update OneDrive Freespace Sensor
                    double? freeSpaceGB = await _graphHelper.GetFreeSpaceInGB();
                    await HassOnedriveFreeSpaceEntityState.UpdateOneDriveFreespaceSensorInHass(freeSpaceGB, _hassIoClient);
                    ConsoleLogger.LogInfo("Checking backups");

                    if (_addonOptions.RecoveryMode)
                    {
                        await backupManager.DownloadCloudBackupsAsync();
                        Console.WriteLine();
                    }
                    else
                    {
                        backupManager.PerformBackupsAsync();
                    }

                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Unexpected error. {ex}");
                }

                if (_addonOptions.RecoveryMode)
                {
                    ConsoleLogger.LogInfo("Recovery run done. New scan will begin in 10 minutes");
                    ConsoleLogger.LogInfo($"To switch back to Normal backup mode please stop the addon, disable Recovery_Mode in the configuration and restart");
                    await Task.Delay(TimeSpan.FromMinutes(10));
                }
                else
                {
                    ConsoleLogger.LogInfo("Backup Interval Completed.");
                    await Task.Delay(intervalDelay);
                }
            }
        }

        public void Stop()
        {
            _enabled = false;
        }
    }
}
