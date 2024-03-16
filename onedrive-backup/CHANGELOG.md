## v.2.3.1 []
### 🆕 Added  
* Added more persistent notifications for alerting on errors and improved overall behavior of persistant notifications
### 🐞 Fixed
* Issue downloading a backup from OneDrive to HomeAssistant


## v2.3 [Febuary 7th 2024]
### 🆕 Added  
* Added ability to retain backups indefinitely
* New details in overview page - Total backup sizes, next backup date
* Updated folder support for File Sync - /homeassistant, /addon_configs
* New optional Error reporting (opt in)
* Upgraded core libraries (Azure.Identity, Microsoft.Graph, Bootstrap)
### 🐞 Fixed
* If not enough space is available in OneDrive, the addon will no longer attempt to upload the backup and fail repeatedly. 
* Addons in partial backups were only refreshed on addon start (if another addon was installed after the addon has started it would not appear in a partial backup)

## v2.2.4 [November 10th 2023]
### 🐞 Fixed
* Allowed hours requires restart - Fixed a bug where changing the allowed hours wouldn't take effect until addon was restarted.
* Dark Mode - Fixed some display issues with dark mode
* Small UI changes


## v2.2.3 [October 18th 2023]
### 🐞 Fixed
* Settings Binding Issues - Fixed bugs where settings were displayed / saved incorrectly

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

