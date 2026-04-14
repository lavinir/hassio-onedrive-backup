import { useQuery } from '@tanstack/react-query';
import instance from '../api/instance';
import { ILoginStatus } from '../types/settings.types';

const getLoginStatus = async (): Promise<ILoginStatus> => {
  try {
    const response = await instance.get('/settings/login-status');
    return { 
      authState: response.data.authState,
      userEmail: response.data.userEmail
    };
  } catch (error) {
    // If there's any error (including 500), treat it as not logged in
    console.error('Failed to get login status:', error);
    return {
      authState: 'NotLoggedIn',
      userEmail: null
    };
  }
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
    // Retry up to 3 times with exponential backoff
    retry: 3,
    retryDelay: (attemptIndex) => Math.min(1000 * Math.pow(2, attemptIndex), 30000),
  });
};