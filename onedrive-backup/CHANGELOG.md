## v2.2.2 [October 4th 2023]

### 🐞 Fixed
* Dark Mode - Fixed issue where dark mode styles were not properly applied across all UI

## v2.2.1 [October 4th 2023]
### 🆕 Added  
* Dark Mode support!

### 🐞 Fixed
* Uploading backups with unsupported chars in name - The addon will now replace unsupported OneDrive chars with an "_" instead of failing
* Full backups treated as partial backups by addon
* Save confirmation fix for invalid settings input

## v2.2 [October 2nd 2023]
Been a while, this is a pretty big release 🎉. Please make sure you read the Breaking Changes before upgrade.

### ❗Breaking 
* All configuration options have been moved from Home Assistant to the addon itself. The addon will attempt to migrate the old settings but if this fails, settings will be reverted to defaults! Make sure you go over all settings after upgrading.

### 🆕 Added  
* Generational Backups! You can now set backup retention policy to Generational (days, weeks, months, years). See README for full details.
* As mentioned above, all settings are now done through the Addon UI instead of Home Assistant. No need for restarting the addon when modifying settings.
* Ignore Allowed Hours for File Sync - Added option to allow File Syncing to occur all day and only have backups follow the Allowed Hours settings.
* Optional Telemetry - I've added an optional Opt-In option that sends anonymous configuration telemetry for which features are enabled to better help me focus on most used features. Full details in the README. (disabled by default)

### 🐞 Fixed
* File Sync Deletions - Fixed a bug where files synced would be deleted immediately after they were uploaded.

## v2.1.2 [May  18th 2023]

### Fixed
* Fixed permissions issue when running the add-on in a Supervised (Non HassOS) deployment
* Improved Exception Handling

## v2.1.1 [April 11th 2023]
### Fixed
* Fixed a bug where subsequent backup iterations would begin before the previous cycle has completed

## v2.1 [March 30th 2023]
### Added
* Option to [ignore Home Assistant Upgrade Backups](https://github.com/lavinir/hassio-onedrive-backup#ignore-home-assistant-upgrade-backups)
* Show Upload Transfer Speed in UI

### Fixed
* UI bugfixes

### Updated
* More log verbosity updates (Info -> Verbose)

## v2.02 [Martch 28th 2023]
### Fixed
* Fixed a scenario where potentially temporary backup files would be uploaded as part of the addon's own backup

### Updated
* Log verbosity for INFO level has been decreased. Those logs will still be available at the VERBOSE level.

## v2.01 [March 21st 2023]
### Fixed
* Removed addon list from backup details in webui to workaround onedrive size limitation causing backup uplaods to fail when a lot of addons are backed up.

## v2.0 [March 21st 2023]
### Summary
This is a very big release 🎉 with many new features and some breaking changes, please see details below.

### Added
* **Web Interface!** - There is now a dedicated Web UI for the addon, where you can easily see an overview of all your backups, trigger manual backups, retrieve backups and more. **CloudFlared** users please see [relevant prerequisites](https://github.com/lavinir/hassio-onedrive-backup#html-content-filtering-error)
* **On Demand Backups** - Available through the new Web Interface
* **Log Verbosity Config** - You can now specify the [verbosity](https://github.com/lavinir/hassio-onedrive-backup#log-level) of the addon logs from the config.
* **Exclude Addons from Backup** - It is now possible to [exclude addons](https://github.com/lavinir/hassio-onedrive-backup#excluded-addons-optional) from your backups

### Updated
* <kbd>[Breaking change]</kbd> **File Sync Engine** - The File Syncing engine has been rewritten. Along with it the relevant configuration for specifying the [sync paths](https://github.com/lavinir/hassio-onedrive-backup#file-sync-paths-optional) has changed. Please see the [documentation]((https://github.com/lavinir/hassio-onedrive-backup#file-sync-paths-optional)) for proper configuration.

### Fixed
* **Sync Drift** - Updating backup interval logic to avoid an issue where backup times could slowly drift and if you were using a short **Allowed Hours** window you could reach an issue where an update would be skipped because a window was missed.

### Removed
* <kbd>[Breaking change]</kbd> **Recovery Mode** - With the introduction of the dedicated Web UI, Recovery mode is no longer needed and has been removed. You can now load a Backup from OneDrive to Home Assistant with a single button from the Web UI. See [Restoring from Backup](https://github.com/lavinir/hassio-onedrive-backup#restoring-from-backup)
