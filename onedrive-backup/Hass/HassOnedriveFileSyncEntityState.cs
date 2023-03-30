using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace hassio_onedrive_backup.Hass
{
    internal class HassOnedriveFileSyncEntityState : INotifyPropertyChanged
    {
        private const string OneDrive_FileSync_Entity_ID = "sensor.onedrivefilesync";

        private IHassioClient? _hassioClient;
        private static HassOnedriveFileSyncEntityState? _instance;
        private FileState state;
        private List<JsonConverter> entityStateConverters = new List<JsonConverter>
        {
            new StringEnumConverter()
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        public HassOnedriveFileSyncEntityState(IHassioClient hassioClient)
        {
            _hassioClient = hassioClient;
            State = FileState.Unknown;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public FileState State
        {
            get => state;
            set
            {
                state = value;
                if (state != FileState.Syncing)
                {
                    UploadPercentage = null;
                    UploadSpeed = null;
                }
            }
        }

		public int? UploadPercentage { get; set; }

		public int? UploadSpeed { get; set; }


		public async Task UpdateBackupEntityInHass()
        {
            var payload = new
            {
                state = State,
                attributes = new Dictionary<string, string?>
                {
					{ FileStateAttribute.UploadPercentage, UploadPercentage == null ? null : $"{UploadPercentage}%" },
					{ FileStateAttribute.UploadSpeed, UploadSpeed == null ? null : $"{UploadSpeed} KB/s" },
}
			};

            string payloadStr = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = entityStateConverters
            });

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            await _hassioClient.UpdateHassEntityStateAsync(OneDrive_FileSync_Entity_ID, payloadStr);
        }

        public class FileStateAttribute
        {
			public const string UploadPercentage = "Current file upload percentage";
			public const string UploadSpeed = "Current file upload speed (KB/s)";
		}

		public enum FileState
        {
            Syncing,
            Synced,
            Unknown,
        }
    }
}
