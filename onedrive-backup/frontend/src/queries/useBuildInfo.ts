import { useQuery } from '@tanstack/react-query';
import { fetchBuildInfo } from '../api/buildInfo';

export const useBuildInfo = () => {
  return useQuery({
    queryKey: ['buildInfo'],
    queryFn: fetchBuildInfo,
    staleTime: Infinity,
  });
};
