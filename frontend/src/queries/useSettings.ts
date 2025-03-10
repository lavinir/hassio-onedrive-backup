import { useQuery } from '@tanstack/react-query';
import { mockSettings } from '../mocks/settingsData';

export const useSettings = () => {
  // TODO: Replace with real API call when backend is ready
  return useQuery({
    queryKey: ['settings'],
    queryFn: () => Promise.resolve(mockSettings),
    // Uncomment when backend is ready:
    // queryFn: fetchSettings,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
};