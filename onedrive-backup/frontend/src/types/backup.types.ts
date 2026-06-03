import { ReactElement } from 'react';

export type BackupStatus = 'In Progress' | 'Local' | 'OneDrive' | 'Synced';
export type SourceType = 'Automated' | 'Manual' | 'External';
export type BackupType = 'Partial' | 'Full';

export interface Backup {
  slug: string;
  name: string;
  date: string;
  size: string;
  status: BackupStatus;
  source_type: SourceType;
  type: BackupType;
  local_path?: string;
  retained?: boolean;
}

export interface IStatusInfo {
  icon: ReactElement;
  color: string;
  tooltip: string;
}

export interface IThemeMode {
  mode: 'light' | 'dark';
}