import { ISettingsForm } from '../types/settings.types';

export const mockSettings: ISettingsForm = {
  general: {
    instanceName: 'Home Assistant',
    hassAPITimeoutMinutes: 5,
    logLevelStr: 'info',
    notifyOnError: true,
    enableAnonymousErrorReporting: false,
    enableAnonymousTelemetry: false
  },
  backup: {
    // Core backup settings
    backupName: 'hassBackup',
    backupIntervalDays: 3,
    backupAllowedHours: '*',

    // Retention settings
    maxLocalBackups: 10,
    maxOnedriveBackups: 20,
    generationalDays: 7,
    generationalWeeks: 4,
    generationalMonths: 6,
    generationalYears: 1,

    // Exclusion settings
    excludedAddons: [],
    excludeMediaFolder: false,
    excludeSSLFolder: false,
    excludeShareFolder: false,
    excludeLocalAddonsFolder: false,

    // Behavioral settings
    monitorAllLocalBackups: true,
    ignoreUpgradeBackups: false
  },
  fileSync: {
    syncPaths: [],
    fileSyncRemoveDeleted: true,
    ignoreAllowedHoursForFileSync: false
  }
};