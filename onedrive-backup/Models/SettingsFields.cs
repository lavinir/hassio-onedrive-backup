using YamlDotNet.Serialization;

namespace onedrive_backup.Models
{
    public class SettingsFields
    {
        public SettingsFields()
        {
            PopulateFieldMetadata();
        }

        public SettingsData Settings { get; private set; }

        private void PopulateFieldMetadata()
        {
#if DEBUG
			string fieldMetadataYaml = File.ReadAllText("./translations/en.yaml");
#else
            string fieldMetadataYaml = File.ReadAllText("/app/translations/en.yaml");
#endif
			var deserializer = new DeserializerBuilder()
                .Build();
            Settings = deserializer.Deserialize<Configuration>(fieldMetadataYaml).configuration;
        }

        public record class Configuration
        {
            public SettingsData configuration { get; set; }
        }

        public record class SettingsData
        {
            public FieldData recovery_mode { get; set; }
            public FieldData local_backup_num_to_keep { get; set; }
            public FieldData onedrive_backup_num_to_keep { get; set; }
            public FieldData backup_interval_days { get; set; }
            public FieldData generational_days { get; set; }
            public FieldData generational_weeks { get; set; }
            public FieldData generational_months { get; set; }
            public FieldData generational_years { get; set; }
            public FieldData backup_name { get; set; }
            public FieldData monitor_all_local_backups { get; set; }
            public FieldData ignore_upgrade_backups { get; set; }
            public FieldData backup_passwd { get; set; }
            public FieldData notify_on_error { get; set; }
            public FieldData hass_api_timeout_minutes { get; set; }
            public FieldData exclude_media_folder { get; set; }
            public FieldData exclude_ssl_folder { get; set; }
            public FieldData exclude_share_folder { get; set; }
            public FieldData exclude_local_addons_folder { get; set; }
            public FieldData backup_allowed_hours { get; set; }
            public FieldData backup_instance_name { get; set; }
            public FieldData sync_paths { get; set; }
            public FieldData file_sync_remove_deleted { get; set; }
            public FieldData excluded_addons { get; set; }
            public FieldData log_level { get; set; }
            public FieldData enable_anonymous_telemetry { get; set; }
            public FieldData ignore_allowed_hours_for_file_sync { get; set; }

            public FieldData dark_mode { get; set; }
        }

        public record class FieldData
        {
            public string name { get; set; }

            public string description { get; set; }
        }
    }
}
