using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace hassio_onedrive_backup.Contracts
{
    public class AddonOptions
    {
        [JsonProperty("local_backup_num_to_keep")]
        public int MaxLocalBackups { get; set; }

        [JsonProperty("onedrive_backup_num_to_keep")]
        public int MaxOnedriveBackups { get; set; }

        [JsonProperty("backup_interval_days")]
        public float BackupIntervalDays { get; set; }

        [JsonProperty("backup_passwd")]
        public string? BackupPassword { get; set; }

    }
}
