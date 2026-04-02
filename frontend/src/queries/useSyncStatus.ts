import { useQuery } from '@tanstack/react-query';
import { fetchSyncStatus } from '../api/syncStatus';

export const useSyncStatus = () => {
  return useQuery({
    queryKey: ['sync-status'],
    queryFn: fetchSyncStatus,
    refetchInterval: (query) => (query.state.data?.isSyncing ? 3000 : 30000),
  });
};
