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
