# Overview
This App is a OneDrive Backup Addon for Home Assistant. 

# Components

## Frontend
The frontend is written in React. The UI consists a main dashboard page showing the existing backups and their status. The frontend communicates with a REST API (details in the API section below)

## Backend
The backend exposes a REST API for the frontend use. It is written in dot net (C#) and exposes the API to the frontend. 

## API
The API should support the following actions:

#### Get Backups
Gets the list of backups

#### Download Backup
Downloads a backup from OneDrive locally

#### Delete Backup
Deletes a backup

#### Upload Backup
Uploads a local backup to OneDrive

#### Trigger Backup
Creates a new local backup

#### Retention Update
Allows 'pinning' a backup so that it doesn not get removed in the automated retention policy. This takes a backup id and a boolean whether to retain indefintely or not.

#### Check Login Status
Gets the current OneDrive connection status, returns `{ isLoggedIn: boolean }`

#### Initiate OneDrive Authentication
Initiates the OAuth flow with OneDrive, returns `{ authUrl: string }` which the frontend uses to redirect the user to OneDrive's login

#### Disconnect OneDrive
Removes the stored OneDrive credentials and disconnects the app from OneDrive

## Backup Properties
slug: the backup id as a string
name: the backup name
date: the backup date
size: the backup size
status: The status of the backup
source_type: whether it's automated or manual backup
backup_type: partial or full


