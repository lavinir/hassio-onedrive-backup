using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace hassio_onedrive_backup.Hass
{
    public class HassOnedriveEntityState : INotifyPropertyChanged
    {
        private const string OneDrive_Backup_Entity_ID = "sensor.onedrivebackup";
        private IHassioClient? _hassioClient;
        private BackupState state;
        private bool _isSyncing = false;

        private List<JsonConverter> entityStateConverters = new List<JsonConverter>
        {
            new StringEnumConverter()
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        public HassOnedriveEntityState(IHassioClient hassioClient)
        {
            _hassioClient = hassioClient;
            State = BackupState.Unknown;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public BackupState State
        {
            get => state;
            set
            {
                state = value;
                if (state != BackupState.Syncing)
                {
                    UploadPercentage = null;
                    UploadSpeed = null;
                }
            }
        }

        public DateTime? LastLocalBackupDate { get; set; }

        public DateTime? LastOnedriveBackupDate { get; set; }

        public int BackupsInHomeAssistant { get; set; }

        public int BackupsInOnedrive { get; set; }

        public int? UploadPercentage { get; set; }

        public int? DownloadPercentage { get; set; }

        public int RetainedLocalBackups { get; set; }

        public int RetainedOneDriveBackups { get; set; }

        // KB/s
        public int? UploadSpeed { get; set; }

        public async Task SyncStart()
        {
            _isSyncing = true;
            await UpdateBackupEntityInHass();
        }

        public async Task SyncEnd()
        {
            _isSyncing = false;
            await UpdateBackupEntityInHass();
        }

        public async Task UpdateBackupEntityInHass()
        {
            var payload = new
            {
                state = _isSyncing ? BackupState.Syncing : State,
                attributes = new Dictionary<string, string?>
                {
                    { BackupStateAttribute.LastLocalBackupDate, LastLocalBackupDate?.ToString(DateTimeHelper.DateTimeFormat) },
                    { BackupStateAttribute.LastOnedriveBackupDate, LastOnedriveBackupDate?.ToString(DateTimeHelper.DateTimeFormat) },
                    { BackupStateAttribute.BackupsInHomeAssistant, BackupsInHomeAssistant.ToString() },
                    { BackupStateAttribute.BackupsInOnedrive, BackupsInOnedrive.ToString() },
					{ BackupStateAttribute.UploadPercentage, UploadPercentage == null ? null : $"{UploadPercentage}%" },
					{ BackupStateAttribute.UploadSpeed, UploadSpeed== null ? null : $"{UploadSpeed} KB/s" },
                    { BackupStateAttribute.DownloadPercentage, DownloadPercentage == null ? null : $"{DownloadPercentage}%" }
                }
            };

            string payloadStr = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = entityStateConverters
            });

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            await _hassioClient.UpdateHassEntityStateAsync(OneDrive_Backup_Entity_ID, payloadStr);
        }

        public class BackupStateAttribute
        {
            public const string LastLocalBackupDate = "Last Local backup date";

            public const string LastOnedriveBackupDate = "Last OneDrive backup date";

            public const string BackupsInHomeAssistant = "Backups in Home Assistant";

            public const string BackupsInOnedrive = "Backups in OneDrive";

            public const string UploadPercentage = "Current backup upload percentage";

            public const string DownloadPercentage = "Backup download percentage";

            public const string UploadSpeed = "Current backup upload speed (KB/s)";
        }
        public enum BackupState
        {
            Syncing,
            Backed_Up_Local,
            Backed_Up_Onedrive,
            Backed_Up,
            Stale,
            RecoveryMode,
            Unknown,
        }
    }
}
