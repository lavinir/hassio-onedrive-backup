using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hassio_onedrive_backup.Hass
{
    internal class HassOnedriveEntityState
    {
        private const string Entity_ID = "sensor.onedrivebackup";
        private IHassioClient? _hassioClient;
        private static HassOnedriveEntityState? _instance;

        private HassOnedriveEntityState()
        {
            State = BackupState.Unknown;
        }

        public static HassOnedriveEntityState Instance()
        {
            _instance = _instance ?? new HassOnedriveEntityState();
            return _instance;
        }

        public static HassOnedriveEntityState Initialize(IHassioClient hassioClient)
        {
            Instance()._hassioClient = hassioClient;
            return _instance!;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public BackupState State { get; set; }

        public DateTime? LastLocalBackupDate { get; set; }

        public DateTime? LastOnedriveBackupDate { get; set; }

        public int BackupsInHomeAssistant { get; set; }

        public int BackupsInOnedrive { get; set; }

        public async Task UpdateEntityInHass()
        {
            var payload = new
            {
                state = State,
                attributes = new Dictionary<string, string>
                {
                    { BackupStateAttribute.LastLocalBackupDate, LastLocalBackupDate?.ToString() },
                    { BackupStateAttribute.LastOnedriveBackupDate, LastOnedriveBackupDate?.ToString() },
                    { BackupStateAttribute.BackupsInHomeAssistant, BackupsInHomeAssistant.ToString() },
                    { BackupStateAttribute.BackupsInOnedrive, BackupsInOnedrive.ToString() },
                }
            };

            string payloadStr = JsonConvert.SerializeObject(payload);
            await _hassioClient.UpdateHassEntityState(Entity_ID, payloadStr);
        }

        public class BackupStateAttribute
        {
            public const string LastLocalBackupDate = "Last Local backup date";

            public const string LastOnedriveBackupDate = "Last Onedrive backup date";

            public const string BackupsInHomeAssistant = "Backups in Home Assistant";

            public const string BackupsInOnedrive = "Backups in Onedrive";
        }
        public enum BackupState
        {
            Syncing,
            Backed_Up_Local,
            Backed_Up_Onedrive,
            Backed_Up,
            Stale,
            Unknown,
        }
    }
}
