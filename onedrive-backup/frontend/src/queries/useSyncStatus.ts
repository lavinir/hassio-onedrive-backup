import { useQuery } from '@tanstack/react-query';
import { fetchSyncStatus } from '../api/syncStatus';

export const useSyncStatus = () => {
  return useQuery({
    queryKey: ['sync-status'],
    queryFn: fetchSyncStatus,
    refetchInterval: (query) => {
      const data = query.state.data;
      return (data?.isSyncing || data?.fileSyncState?.state === 'Syncing') ? 3000 : 30000;
    },
  });
};
