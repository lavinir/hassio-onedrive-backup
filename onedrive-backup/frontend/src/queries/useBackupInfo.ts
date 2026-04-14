import { useQuery } from '@tanstack/react-query';
import { fetchBackupInfo } from '../api/backupInfo';

export const useBackupInfo = (slug: string, enabled: boolean) => {
  return useQuery({
    queryKey: ['backupInfo', slug],
    queryFn: () => fetchBackupInfo(slug),
    enabled,
    staleTime: Infinity,
  });
};
