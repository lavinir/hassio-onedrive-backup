using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hassio_onedrive_backup.Hass
{
    internal class HassOnedriveFileSyncEntityState
    {
        private const string OneDrive_FileSync_Entity_ID = "sensor.onedrivefilesync";

        private IHassioClient? _hassioClient;
        private static HassOnedriveFileSyncEntityState? _instance;
        private FileState state;
        private List<JsonConverter> entityStateConverters = new List<JsonConverter>
        {
            new StringEnumConverter()
        };

        private HassOnedriveFileSyncEntityState()
        {
            State = FileState.Unknown;
        }

        public static HassOnedriveFileSyncEntityState Instance
        {
            get
            {
                _instance = _instance ?? new HassOnedriveFileSyncEntityState();
                return _instance;
            }
        }

        public static HassOnedriveFileSyncEntityState Initialize(IHassioClient hassioClient)
        {
            Instance._hassioClient = hassioClient;
            return _instance!;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public FileState State
        {
            get => state;
            set
            {
                state = value;
                if (state != FileState.Synced)
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
                    { FileStateAttribute.UploadPercentage, UploadPercentage == null ? null : $"{UploadPercentage}%" },
                }
            };

            string payloadStr = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = entityStateConverters
            });

            await _hassioClient.UpdateHassEntityStateAsync(OneDrive_FileSync_Entity_ID, payloadStr);
        }

        public class FileStateAttribute
        {
            public const string UploadPercentage = "Current backup upload percentage";
        }
        
        public enum FileState
        {
            Syncing,
            Synced,
            Unknown,
        }
    }
}
