using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Graph;
using hassio_onedrive_backup.Hass;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup.Sync
{
    internal class SyncManager
    {
        private AddonOptions _addonOptions;
        private IGraphHelper _graphHelper;
        private IHassioClient _hassIoClient;
        private BitArray _allowedHours;
        private ManualResetEventSlim _syncRestEvent = new ManualResetEventSlim(false);

        public SyncManager(AddonOptions addonOptions, IGraphHelper graphHelper, IHassioClient hassIoClient, BitArray allowedHours)
        {
            _addonOptions = addonOptions;
            _graphHelper = graphHelper;
            _hassIoClient = hassIoClient;
            _allowedHours = allowedHours;
        }

        public void StartSync()
        {
            if (_syncRestEvent.IsSet == false)
            {
                _syncRestEvent.Set();
            }
        }

        public void StopSync()
        {
            if (_syncRestEvent.IsSet)
            {
                _syncRestEvent.Reset();
            }
        }

        private async Task SyncLoop()
        {
            while (true)
            {
                try
                {

                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogError($"Error Syncing: {ex}");
                    //Todo: Fire Event
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }
    }
}
