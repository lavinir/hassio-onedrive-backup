import instance from './instance';
import { Backup } from '../types/backup.types';

export const fetchBackups = async (): Promise<Backup[]> => {
  const response = await instance.get('/backups');
  return response.data;
};

export const getTransferProgress = async (operationId: string): Promise<number> => {
  const response = await instance.get(`/backups/progress/${operationId}`);
  return response.data.progress;
};

export const downloadBackup = async (slugId: string): Promise<string> => {
  // Returns operation ID instead of blob now
  const response = await instance.post(`/backups/${slugId}/download`);
  return response.data.operationId;
};

export const deleteBackup = async (slugId: string): Promise<void> => {
  await instance.delete(`/backups/${slugId}`);
};

export const uploadBackup = async (slugId: string): Promise<string> => {
  // Returns operation ID instead of waiting for completion
  const response = await instance.post(`/backups/${slugId}/upload`);
  return response.data.operationId;
};

export const triggerBackup = async (): Promise<Backup> => {
  const response = await instance.post('/backups/create');
  return response.data;
};

export const updateBackupRetention = async (slugId: string, retain: boolean): Promise<Backup> => {
  const response = await instance.post(`/backups/${slugId}/retention`, { retain });
  return response.data;
};