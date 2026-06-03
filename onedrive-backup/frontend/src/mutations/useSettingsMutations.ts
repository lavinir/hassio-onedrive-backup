import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateSettings, testOneDriveConnection, resetOneDriveConnection, initiateOneDriveAuth, disconnectOneDrive } from '../api/settings';
import { ISettingsForm } from '../types/settings.types';

export const useUpdateSettings = () => {
  const queryClient = useQueryClient();
  const initialData = queryClient.getQueryData<ISettingsForm>(['settings']);
  
  const mutation = useMutation({
    mutationFn: (data: ISettingsForm) => updateSettings(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] });
    },
  });

  return {
    ...mutation,
    data: mutation.data || initialData,
    setData: (updater: (prev: ISettingsForm | undefined) => ISettingsForm | undefined) => {
      const currentData = mutation.data || initialData;
      const newData = updater(currentData);
      if (newData) {
        queryClient.setQueryData(['settings'], newData);
      }
    }
  };
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

export const useOneDriveAuth = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: initiateOneDriveAuth,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['loginStatus'] });
    },
  });
};

export const useDisconnectOneDrive = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: disconnectOneDrive,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['loginStatus'] });
    },
  });
};