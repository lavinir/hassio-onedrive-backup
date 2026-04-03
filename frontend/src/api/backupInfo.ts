import instance from './instance';

export interface BackupAddonInfo {
  slug: string;
  name: string;
  version: string;
  size: number;
}

export interface BackupInfo {
  isAvailableLocally: boolean;
  slug: string;
  name?: string;
  date?: string;
  size?: number;
  type?: string;
  compressed?: boolean;
  isProtected?: boolean;
  supervisorVersion?: string;
  homeAssistantVersion?: string;
  addons?: BackupAddonInfo[];
  folders?: string[];
  homeAssistantExcludeDatabase?: boolean;
}

export const fetchBackupInfo = async (slug: string): Promise<BackupInfo> => {
  const response = await instance.get(`/backup/${slug}/info`);
  return response.data;
};
