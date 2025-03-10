import instance from './instance';
import { ISettingsForm } from '../types/settings.types';

export const fetchSettings = async (): Promise<ISettingsForm> => {
  const response = await instance.get('/settings');
  return response.data;
};

export const updateSettings = async (settings: ISettingsForm): Promise<ISettingsForm> => {
  const response = await instance.put('/settings', settings);
  return response.data;
};

export const testOneDriveConnection = async (): Promise<{ success: boolean; message: string }> => {
  const response = await instance.post('/settings/test-connection');
  return response.data;
};

export const resetOneDriveConnection = async (): Promise<void> => {
  await instance.post('/settings/reset-connection');
};