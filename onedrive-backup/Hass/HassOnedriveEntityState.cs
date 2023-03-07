using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace hassio_onedrive_backup.Hass
{
    internal class HassOnedriveEntityState : INotifyPropertyChanged
    {
        private const string OneDrive_Backup_Entity_ID = "sensor.onedrivebackup";
        private IHassioClient? _hassioClient;
        private static HassOnedriveEntityState? _instance;
        private BackupState state;
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

        //public static HassOnedriveEntityState Instance
        //{
        //    get
        //    {
        //        _instance = _instance ?? new HassOnedriveEntityState();
        //        return _instance;
        //    }
        //}

        public static HassOnedriveEntityState Initialize(IHassioClient hassioClient)
        {
            //Instance._hassioClient = hassioClient;
            return _instance!;
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
                }
            }
        }

        public DateTime? LastLocalBackupDate { get; set; }

        public DateTime? LastOnedriveBackupDate { get; set; }

        public int BackupsInHomeAssistant { get; set; }

        public int BackupsInOnedrive { get; set; }

        public int? UploadPercentage { get; set; }

        public int? DownloadPercentage { get; set; }

        public async Task UpdateBackupEntityInHass()
        {
            var payload = new
            {
                state = State,
                attributes = new Dictionary<string, string?>
                {
                    { BackupStateAttribute.LastLocalBackupDate, LastLocalBackupDate?.ToString(DateTimeHelper.DateTimeFormat) },
                    { BackupStateAttribute.LastOnedriveBackupDate, LastOnedriveBackupDate?.ToString(DateTimeHelper.DateTimeFormat) },
                    { BackupStateAttribute.BackupsInHomeAssistant, BackupsInHomeAssistant.ToString() },
                    { BackupStateAttribute.BackupsInOnedrive, BackupsInOnedrive.ToString() },
                    { BackupStateAttribute.UploadPercentage, UploadPercentage == null ? null : $"{UploadPercentage}%" },
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
