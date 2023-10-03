using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;
using System.Text.Json.Serialization;
using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;

namespace hassio_onedrive_backup.Contracts
{
    public class AddonOptions : IEqualityComparer<AddonOptions>
    {
        public const string AddonVersion = "2.2";

        public event Action OnOptionsChanged;

        [JsonProperty("local_backup_num_to_keep")]
        public int MaxLocalBackups { get; set; } = 10;

        [JsonProperty("onedrive_backup_num_to_keep")]
        public int MaxOnedriveBackups { get; set; } = 10;

		[JsonProperty("generational_days")]
		public int? GenerationalDays { get; set; }

		[JsonProperty("generational_weeks")]
		public int? GenerationalWeeks { get; set; }

		[JsonProperty("generational_months")]
		public int? GenerationalMonths { get; set; }

		[JsonProperty("generational_years")]
		public int? GenerationalYears { get; set; }

        [JsonProperty("backup_interval_days")]
        public float BackupIntervalDays { get; set; } = 1;

        [JsonProperty("backup_passwd")]
        public string? BackupPassword { get; set; }

        [JsonProperty("backup_name")]
        public string? BackupName { get; set; } = "hass_backup";

        [JsonProperty("monitor_all_local_backups")]
        public bool MonitorAllLocalBackups{ get; set; }

        [JsonProperty("notify_on_error")]
        public bool NotifyOnError { get; set; }

        [JsonProperty("hass_api_timeout_minutes")]
        public int HassAPITimeoutMinutes { get; set; } = 30;

        [JsonProperty("exclude_media_folder")]
        public bool ExcludeMediaFolder { get; set; }

        [JsonProperty("exclude_ssl_folder")]
        public bool ExcludeSSLFolder { get; set; }

        [JsonProperty("exclude_share_folder")]
        public bool ExcludeShareFolder { get; set; }

        [JsonProperty("exclude_local_addons_folder")]
        public bool ExcludeLocalAddonsFolder { get; set; }

        [JsonProperty("backup_allowed_hours")]
        public string? BackupAllowedHours { get; set; }

        [JsonProperty("backup_instance_name")]
        public string? InstanceName { get; set; }

        [JsonProperty("sync_paths")]
        public List<string> SyncPaths { get; set; } = new List<string>();

        [JsonProperty("file_sync_remove_deleted")]
        public bool FileSyncRemoveDeleted { get; set; } = false;

        [JsonProperty("excluded_addons")]
        public List<string> ExcludedAddons { get; set; } = new List<string>();

        [JsonProperty("log_level")]
        public string LogLevelStr { get; set; } = "info";

        [JsonProperty("ignore_upgrade_backups")]
        public bool IgnoreUpgradeBackups { get; set; }

        [JsonProperty("enable_anonymous_telemetry")]
        public bool EnableAnonymousTelemetry { get; set; } = false;

		[JsonProperty("ignore_allowed_hours_for_file_sync")]
		public bool IgnoreAllowedHoursForFileSync { get; set; } = false;

        [JsonProperty("dark_mode")]
        public bool DarkMode { get; set; } = false;

        [JsonIgnore]
		public ConsoleLogger.LogLevel LogLevel => LogLevelStr switch
        {
			"verbose" => ConsoleLogger.LogLevel.Verbose,
			"info" => ConsoleLogger.LogLevel.Info,
			"warning" => ConsoleLogger.LogLevel.Warning,
			"error" => ConsoleLogger.LogLevel.Error,
			_ => ConsoleLogger.LogLevel.Info
		};

		[JsonIgnore]
        public float BackupIntervalHours => BackupIntervalDays * 24;

        [JsonIgnore]
        public string BackupNameSafe => string.IsNullOrEmpty(BackupName) ? "hass_backup" : BackupName;

        [JsonIgnore]
        public bool IsPartialBackup => ExcludeLocalAddonsFolder || ExcludeMediaFolder || ExcludeShareFolder || ExcludeSSLFolder || ExcludedAddons.Where(addon => string.IsNullOrWhiteSpace(addon) == false).Any();

        [JsonIgnore]
        public bool FileSyncEnabled => SyncPaths != null && SyncPaths.Where(sp => string.IsNullOrWhiteSpace(sp) == false).Any();

        [JsonIgnore]
        public bool GenerationalBackups => GenerationalDays != null || GenerationalWeeks != null || GenerationalMonths != null || GenerationalYears != null;

        [JsonIgnore]
        public string UI_Display_Mode => DarkMode ? "dark-mode" : "light-mode";

        public List<string> IncludedFolderList
        {
            get
            {
                List<string> folders = new List<string>();
                if (!ExcludeLocalAddonsFolder)
                {
                    folders.Add("addons/local");
                }

                if (!ExcludeMediaFolder)
                {
                    folders.Add("media");
                }

                if (!ExcludeShareFolder)
                {
                    folders.Add("share");
                }

                if (!ExcludeSSLFolder)
                {
                    folders.Add("ssl");
                }

                return folders;
            }
        }

        public void RaiseOptionsChanged()
        {
            var handler = OnOptionsChanged;
            handler?.Invoke();
        }

        public void CopyValuesFromInstance(AddonOptions newOptions)
        {
            MaxLocalBackups = newOptions.MaxLocalBackups;
            MaxOnedriveBackups = newOptions.MaxOnedriveBackups;
            GenerationalDays = newOptions.GenerationalDays;
            GenerationalWeeks = newOptions.GenerationalWeeks;
            GenerationalMonths = newOptions.GenerationalMonths;
            GenerationalYears = newOptions.GenerationalYears;
            BackupIntervalDays = newOptions.BackupIntervalDays;
            BackupPassword = newOptions.BackupPassword;
            BackupName = newOptions.BackupName;
            MonitorAllLocalBackups = newOptions.MonitorAllLocalBackups;
            NotifyOnError = newOptions.NotifyOnError;
            HassAPITimeoutMinutes = newOptions.HassAPITimeoutMinutes;
            ExcludeMediaFolder = newOptions.ExcludeMediaFolder;
            ExcludeSSLFolder = newOptions.ExcludeSSLFolder;
            ExcludeShareFolder = newOptions.ExcludeShareFolder;
            ExcludeLocalAddonsFolder = newOptions.ExcludeLocalAddonsFolder;
            BackupAllowedHours = newOptions.BackupAllowedHours;
            InstanceName = newOptions.InstanceName;
            SyncPaths = newOptions.SyncPaths;
            FileSyncRemoveDeleted = newOptions.FileSyncRemoveDeleted;
            ExcludedAddons = newOptions.ExcludedAddons;
            LogLevelStr = newOptions.LogLevelStr;
            IgnoreUpgradeBackups = newOptions.IgnoreUpgradeBackups;
            EnableAnonymousTelemetry = newOptions.EnableAnonymousTelemetry;
            IgnoreAllowedHoursForFileSync = newOptions.IgnoreAllowedHoursForFileSync;
            DarkMode = newOptions.DarkMode;
        }

		public bool Equals(AddonOptions? options1, AddonOptions? options2)
		{
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

			bool check = 
                options1.MaxLocalBackups == options2.MaxLocalBackups &&
                options1.MaxOnedriveBackups == options2.MaxOnedriveBackups &&
                options1.GenerationalDays == options2.GenerationalDays &&
                options1.GenerationalWeeks == options2.GenerationalWeeks &&
                options1.GenerationalMonths == options2.GenerationalMonths &&
                options1.GenerationalYears == options2.GenerationalYears &&
                options1.BackupIntervalDays == options2.BackupIntervalDays &&
                options1.BackupPassword == options2.BackupPassword &&
                options1.BackupName == options2.BackupName &&
                options1.MonitorAllLocalBackups == options2.MonitorAllLocalBackups &&
                options1.NotifyOnError == options2.NotifyOnError &&
                options1.HassAPITimeoutMinutes == options2.HassAPITimeoutMinutes &&
                options1.ExcludeMediaFolder == options2.ExcludeMediaFolder &&
                options1.ExcludeSSLFolder == options2.ExcludeSSLFolder &&
                options1.ExcludeShareFolder == options2.ExcludeShareFolder &&
                options1.ExcludeLocalAddonsFolder == options2.ExcludeLocalAddonsFolder &&
                options1.BackupAllowedHours == options2.BackupAllowedHours &&
                options1.InstanceName == options2.InstanceName &&
                options1.SyncPaths.SequenceEqual(options2.SyncPaths) &&
                options1.FileSyncRemoveDeleted == options2.FileSyncRemoveDeleted &&
                options1.ExcludedAddons.SequenceEqual(options2.ExcludedAddons) &&
                options1.LogLevelStr == options2.LogLevelStr &&
                options1.IgnoreUpgradeBackups == options2.IgnoreUpgradeBackups &&
                options1.EnableAnonymousTelemetry == options2.EnableAnonymousTelemetry &&
                options1.IgnoreAllowedHoursForFileSync == options2.IgnoreAllowedHoursForFileSync;

            return check;
		}

		public override bool Equals(object? obj)
		{
			if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if ((obj.GetType() != GetType()))
            {
                return false;
            }

            var options = obj as AddonOptions;
			bool check =
	            options!.MaxLocalBackups == MaxLocalBackups &&
	            options.MaxOnedriveBackups == MaxOnedriveBackups &&
	            options.GenerationalDays == GenerationalDays &&
	            options.GenerationalWeeks == GenerationalWeeks &&
	            options.GenerationalMonths == GenerationalMonths &&
	            options.GenerationalYears == GenerationalYears &&
	            options.BackupIntervalDays == BackupIntervalDays &&
	            options.BackupPassword == BackupPassword &&
	            options.BackupName == BackupName &&
	            options.MonitorAllLocalBackups == MonitorAllLocalBackups &&
	            options.NotifyOnError == NotifyOnError &&
	            options.HassAPITimeoutMinutes == HassAPITimeoutMinutes &&
	            options.ExcludeMediaFolder == ExcludeMediaFolder &&
	            options.ExcludeSSLFolder == ExcludeSSLFolder &&
	            options.ExcludeShareFolder == ExcludeShareFolder &&
	            options.ExcludeLocalAddonsFolder == ExcludeLocalAddonsFolder &&
	            options.BackupAllowedHours == BackupAllowedHours &&
	            options.InstanceName == InstanceName &&
	            options.SyncPaths.SequenceEqual(SyncPaths) &&
	            options.FileSyncRemoveDeleted == FileSyncRemoveDeleted &&
	            options.ExcludedAddons.SequenceEqual(ExcludedAddons) &&
	            options.LogLevelStr == LogLevelStr &&
	            options.IgnoreUpgradeBackups == IgnoreUpgradeBackups &&
	            options.EnableAnonymousTelemetry == EnableAnonymousTelemetry &&
	            options.IgnoreAllowedHoursForFileSync == IgnoreAllowedHoursForFileSync;


            return check;
		}

		public int GetHashCode([DisallowNull] AddonOptions obj)
		{
			unchecked
			{
				int hashCode = 17;

				hashCode = (hashCode * 23) + MaxLocalBackups.GetHashCode();
				hashCode = (hashCode * 23) + MaxOnedriveBackups.GetHashCode();
				hashCode = (hashCode * 23) + GenerationalDays.GetHashCode();
				hashCode = (hashCode * 23) + GenerationalWeeks.GetHashCode();
				hashCode = (hashCode * 23) + GenerationalMonths.GetHashCode();
				hashCode = (hashCode * 23) + GenerationalYears.GetHashCode();
				hashCode = (hashCode * 23) + BackupIntervalDays.GetHashCode();
				hashCode = (hashCode * 23) + (BackupPassword?.GetHashCode() ?? 0);
				hashCode = (hashCode * 23) + (BackupName?.GetHashCode() ?? 0);
				hashCode = (hashCode * 23) + MonitorAllLocalBackups.GetHashCode();
				hashCode = (hashCode * 23) + NotifyOnError.GetHashCode();
				hashCode = (hashCode * 23) + HassAPITimeoutMinutes.GetHashCode();
				hashCode = (hashCode * 23) + ExcludeMediaFolder.GetHashCode();
				hashCode = (hashCode * 23) + ExcludeSSLFolder.GetHashCode();
				hashCode = (hashCode * 23) + ExcludeShareFolder.GetHashCode();
				hashCode = (hashCode * 23) + ExcludeLocalAddonsFolder.GetHashCode();
				hashCode = (hashCode * 23) + BackupAllowedHours.GetHashCode();
				hashCode = (hashCode * 23) + (InstanceName?.GetHashCode() ?? 0);
				hashCode = (hashCode * 23) + FileSyncRemoveDeleted.GetHashCode();
				hashCode = (hashCode * 23) + LogLevelStr.GetHashCode();
				hashCode = (hashCode * 23) + IgnoreUpgradeBackups.GetHashCode();
				hashCode = (hashCode * 23) + EnableAnonymousTelemetry.GetHashCode();
				hashCode = (hashCode * 23) + IgnoreAllowedHoursForFileSync.GetHashCode();

				hashCode = SyncPaths.Aggregate(hashCode, (current, path) => current ^ path.GetHashCode());
				hashCode = ExcludedAddons.Aggregate(hashCode, (current, addon) => current ^ addon.GetHashCode());

				return hashCode;
			}
		}
	}
}
