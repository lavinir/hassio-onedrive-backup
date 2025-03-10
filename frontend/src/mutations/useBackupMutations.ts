import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteBackup, uploadBackup, downloadBackup, triggerBackup, updateBackupRetention } from '../api/backups';

export const useDeleteBackup = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (slugId: string) => deleteBackup(slugId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['backups'] });
    },
  });
};

export interface TransferOptions {
  onOperationStart?: (operationId: string) => void;
  onError?: () => void;
}

export const useUploadBackup = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (args: [string, TransferOptions?]) => {
      const [slugId, options] = args;
      const operationId = await uploadBackup(slugId);
      options?.onOperationStart?.(operationId);
      return operationId;
    },
    onError: (_, [, options]) => {
      options?.onError?.();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['backups'] });
    },
  });
};

export const useDownloadBackup = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (args: [string, TransferOptions?]) => {
      const [slugId, options] = args;
      const operationId = await downloadBackup(slugId);
      options?.onOperationStart?.(operationId);
      return operationId;
    },
    onError: (_, [, options]) => {
      options?.onError?.();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['backups'] });
    },
  });
};

export const useTriggerBackup = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: (isPartial: boolean) => triggerBackup(isPartial),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['backups'] });
    },
  });
};

export const useUpdateBackupRetention = () => {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ slugId, retain }: { slugId: string; retain: boolean }) => 
      updateBackupRetention(slugId, retain),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['backups'] });
    },
  });
};