import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateSettings, testOneDriveConnection, resetOneDriveConnection } from '../api/settings';
import { ISettingsForm } from '../types/settings.types';

export const useUpdateSettings = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (settings: ISettingsForm) => updateSettings(settings),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] });
    },
  });
};

export const useTestConnection = () => {
  return useMutation({
    mutationFn: testOneDriveConnection,
  });
};

export const useResetConnection = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: resetOneDriveConnection,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] });
    },
  });
};