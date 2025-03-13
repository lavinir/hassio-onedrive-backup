import { useQuery } from '@tanstack/react-query';
import instance from '../api/instance';
import { ILoginStatus } from '../types/settings.types';

const getLoginStatus = async (): Promise<ILoginStatus> => {
  const response = await instance.get('/settings/login-status');
  return { 
    authState: response.data.authState,
    userEmail: response.data.userEmail
  };
};

export const useLoginStatus = () => {
  return useQuery({
    queryKey: ['loginStatus'],
    queryFn: getLoginStatus,
    refetchInterval: (query) => {
      // If we're in the LoggingIn state, poll every 2 seconds
      if (query.state.data?.authState === 'LoggingIn') {
        return 2000;
      }
      // Otherwise poll every 30 seconds to detect if token becomes invalid
      return 30000;
    },
  });
};