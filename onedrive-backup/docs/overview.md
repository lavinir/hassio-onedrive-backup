# Summary
This App is a Home Assistant Add-on that for integrating Home Assistant backups with the user's OneDrive.
It was written in dot net with a blazor frontend.

# Main Features
Dedicated Web UI
Set backup creation schedule
Syncs backups to OneDrive (Personal Account only. OneDrive for business not currently supported)
Additional File Sync to OneDrive
Supports standard backup retention / Generational backup mode
Retain backups indefinitely
Supports multiple Home Assistant instances
Supports Home Assistant Persistent Notifications
Supports Home Assistant Events
Includes Sensor Entities for Dashboards / Automations

# Dev Deployment
Used a powershell script to build the images and publish them to ghcr