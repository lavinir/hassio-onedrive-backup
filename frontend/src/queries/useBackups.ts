import { useQuery } from '@tanstack/react-query';
import { mockBackups } from '../mocks/backupData';

export const useBackups = () => {
  // TODO: Replace with real API call when backend is ready
  return useQuery({
    queryKey: ['backups'],
    queryFn: () => Promise.resolve(mockBackups),
    staleTime: 60000, // 1 minute
    refetchOnWindowFocus: true,
    refetchInterval: 300000, // 5 minutes
  });
};