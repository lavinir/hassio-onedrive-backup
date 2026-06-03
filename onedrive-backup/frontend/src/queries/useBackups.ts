import { useQuery } from '@tanstack/react-query';
import { fetchBackups } from '../api/backups';

export const useBackups = (enabled = true) => {
  return useQuery({
    queryKey: ['backups'],
    queryFn: fetchBackups,
    staleTime: 60000, // 1 minute
    refetchOnWindowFocus: true,
    refetchInterval: 300000, // 5 minutes
    enabled,
  });
};