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

## v1.41 [January 26th 2023]
### Added
 * [**Monitor *all* backups**](https://github.com/lavinir/hassio-onedrive-backup#monitor-all-local-backups) - You can now optionally choose to have the addon monitor **any** created local backup, treating it as any other and backing them up to OneDrive.
 * [**FileSync 1**] - Added option to set paths as [recursive](https://github.com/lavinir/hassio-onedrive-backup#file-sync-paths-optional) enabling syncing all contained subfolders
 * [**FileSync 2**] - Optional [deletions in OneDrive](https://github.com/lavinir/hassio-onedrive-backup#remove-deleted-files-during-file-sync) for Synced Files that no longer exist / no longer monitored.

### Fixed
* [**Allowed Hours**] - Fixed an issue where backup uploads / deletions would still occur outside of allowed hours
* [**Redundant Uploads**] - Fixed an issue that occured when the configured local backup maximum exceeded the configured OneDrive backup maximum where remaining local uploads would be uploaded and immediately deleted from OneDrive
* [**Backup Sensor**] - DateTime attributes now in standard international format
## v1.4 [January 15th 2023]
### Added
* [**First release of File Syncs!** 🎉](https://github.com/lavinir/hassio-onedrive-backup#sync_paths-optional) This allows file syncing to OneDrive outside of Backups. 
* [**New sensor added for File Syncs**](https://github.com/lavinir/hassio-onedrive-backup#home-assistant-file-sync-sensor)

### Updated
* **Password protection for backups now optional** - Due to multiple requests you can now choose to not protect your backups with a password.
## v1.3 [December 13 2022]

### Added
* [**Multiple instance support!**](https://github.com/lavinir/hassio-onedrive-backup#backup_instance_name-optional) You can now have multiple installations of Home Assistant backing up to the same OneDrive account without overriding each other.
* [**Event support for backup failures.**](https://github.com/lavinir/hassio-onedrive-backup#events) Native Home Assistant events will now fire to notify you of backup failures.
* **Configuration Translations** added for English and German (Thank you [@Kreisverkehr](https://github.com/Kreisverkehr)) 

## v1.22 [December 6 2022]

### Updated
* Sensor will now only change to "Syncing" during actual OneDrive backup taking place and not whenever the add-on is checking whether a backup is needed. (This was done to remove state change noise)

## v1.21 [December 2 2022]

### Fixed
* Fixed ArmV7 image issue with timezone support

## v1.2 [December 1 2022]
See updates below. Also I've enabled **[Discussions](https://github.com/lavinir/hassio-onedrive-backup/discussions)** on the official repo to make it easier to get suggestions and feedback.
### Added
* **Allowed backup hours** - You can now specify specific hours during the day that backups will be perfomed. See [allowed_backup_hours](../README.md#backup_allowed_hours-optional)

### Fixed
* Timestamps are now using the set TimeZone instead of UTC
### Removed
* **sync_interval_hours** setting has been removed. This is now handled internally by the add-on.

## v1.11 [November 1 2022]
### Fixed
* **Incorrect Hassio Role** - Role was not actually changed to manager (required for partial backup support)
* **Small fixes**


## v1.1 [October 31 2022]

### Added
* **Recovery Mode** - Allow loading backups back from OneDrive to Home 
Assistant
* **Partial backups** - Added support for partial folder backups
* **Added configuration** - Allow setting timeout on Home Assistant API calls (to accomodate large backup files)
* **Timestamp suffix** - Added Timestamp suffix to local backups
* **OneDrive free space sensor** - Added new sensor showing amount of free space left in OneDrive

### Fixed
* **Large backup files** - Fixed failing backups when backup size is over 2GB

### Security
Add-on role changed to *manager* to enumerate add-ons to support partial backups. This knocked 1 security point from the addon's previous security rating.

The only additional call being made now is enumerating the installed add-ons so they can be passed on to Home Assistant when creating a partial bakcup.

## v1.0 [October 27th 2022]
### Added
- First Release :)
