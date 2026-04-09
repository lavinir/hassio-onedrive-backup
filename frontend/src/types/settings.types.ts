import { Theme } from '@mui/material';
import { ReactNode } from 'react';

export interface IGeneralSettings {
  instanceName: string;
  hassAPITimeoutMinutes: number;
  logLevelStr: 'error' | 'warning' | 'info' | 'verbose';
  notifyOnError: boolean;
  enableAnonymousErrorReporting: boolean;
  enableAnonymousTelemetry: boolean;
}

export interface IBackupSettings {
  // Core backup settings
  backupName: string;
  backupPassword?: string;
  backupIntervalDays: number;
  backupAllowedHours: string;

  // Retention settings
  maxLocalBackups: number;
  maxOnedriveBackups: number;
  generationalDays: number;
  generationalWeeks: number;
  generationalMonths: number;
  generationalYears: number;

  // Exclusion settings
  excludedAddons: string[];
  excludeMediaFolder: boolean;
  excludeSSLFolder: boolean;
  excludeShareFolder: boolean;
  excludeLocalAddonsFolder: boolean;

  // Behavioral settings
  monitorAllLocalBackups: boolean;
  ignoreUpgradeBackups: boolean;
}

export interface IFileSyncSettings {
  syncPaths: string[];
  fileSyncRemoveDeleted: boolean;
  ignoreAllowedHoursForFileSync: boolean;
}

export interface ISettingsForm {
  general: IGeneralSettings;
  backup: IBackupSettings;
  fileSync: IFileSyncSettings;
  theme?: Theme;
}

export interface ISettingsSection {
  title: string;
  description?: string;
  icon: ReactNode;
}

export type OneDriveAuthState = 'NotLoggedIn' | 'LoggingIn' | 'LoggedIn';

export interface ILoginStatus {
    authState: OneDriveAuthState;
    userEmail: string | null;
}