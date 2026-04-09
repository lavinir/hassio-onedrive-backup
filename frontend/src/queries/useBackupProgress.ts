import { useQuery } from '@tanstack/react-query';
import { getTransferProgress } from '../api/backups';

export const useBackupProgress = (operationId: string | null) => {
  return useQuery({
    queryKey: ['backupProgress', operationId],
    queryFn: () => {
      if (!operationId) return 0;
      return getTransferProgress(operationId);
    },
    enabled: !!operationId,
    refetchInterval: 1000, // Poll every second to update progress
    staleTime: 0, // Always fetch fresh progress data
  });
};
