import instance from './instance';

export interface ActiveTransfer {
  slug: string;
  progress: number;
}

export interface FileSyncState {
  state: 'Idle' | 'Syncing' | 'Synced';
  lastSyncTime: string | null;
}

export interface SyncStatus {
  isSyncing: boolean;
  lastSyncTime: string | null;
  activeUpload: ActiveTransfer | null;
  activeDownload: ActiveTransfer | null;
  activeBackupCreation: { progress: number } | null;
  fileSyncState: FileSyncState | null;
}

export const fetchSyncStatus = async (): Promise<SyncStatus> => {
  const response = await instance.get('/backup/sync-status');
  return response.data;
};
