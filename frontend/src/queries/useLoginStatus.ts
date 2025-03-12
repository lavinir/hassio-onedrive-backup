import { useQuery } from '@tanstack/react-query';
import { checkLoginStatus } from '../api/settings';

export const useLoginStatus = () => {
  return useQuery({
    queryKey: ['loginStatus'],
    queryFn: checkLoginStatus,
    refetchInterval: 30000, // Check every 30 seconds
    staleTime: 25000, // Consider data stale after 25 seconds
  });
};