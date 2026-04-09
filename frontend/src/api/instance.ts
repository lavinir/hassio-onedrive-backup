import axios from 'axios';

const instance = axios.create({
  baseURL: '/api',
  timeout: 30000, // Increased timeout for auth operations
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true, // Required for maintaining session cookies
});

// Add response interceptor to handle auth errors
instance.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      // Redirect to settings page if unauthorized
      window.location.href = '/settings';
    }
    return Promise.reject(error);
  }
);

export default instance;