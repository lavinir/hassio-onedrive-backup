import styled from '@emotion/styled';
import { Box, Theme } from '@mui/material';

export const LoginStatusContainer = styled(Box)`
  display: flex;
  align-items: center;
  gap: 8px;
`;

interface StatusDotProps {
  theme?: Theme;
  'data-isloggedin'?: string;
  'data-loggingin'?: string;
}

export const StatusDot = styled(Box)<StatusDotProps>`
  width: 10px; /* Slightly larger */
  height: 10px; /* Slightly larger */
  border-radius: 50%;
  background-color: ${({ theme, 'data-isloggedin': isLoggedIn, 'data-loggingin': isLoggingIn }) =>
    isLoggingIn === 'true'
      ? theme?.palette?.warning?.main || '#ff9800'
      : isLoggedIn === 'true'
      ? theme?.palette?.mode === 'light' 
        ? '#4cff4c' /* Bright light green for blue app bar in light mode */
        : theme?.palette?.success?.main || '#4caf50' 
      : theme?.palette?.error?.main || '#f44336'};
  animation: ${({ 'data-isloggedin': isLoggedIn, 'data-loggingin': isLoggingIn }) =>
    isLoggingIn === 'true'
      ? 'blink 1s infinite'
      : isLoggedIn === 'true'
      ? 'none'
      : 'pulse 2s infinite'};
  border: ${({ 'data-isloggedin': isLoggedIn, theme }) =>
    isLoggedIn === 'true' && theme?.palette?.mode === 'light'
      ? '1px solid rgba(0, 0, 0, 0.3)'
      : 'none'};
  box-shadow: ${({ 'data-isloggedin': isLoggedIn, theme }) =>
    isLoggedIn === 'true' && theme?.palette?.mode === 'light'
      ? '0 0 2px rgba(0, 0, 0, 0.2)'
      : 'none'};

  @keyframes pulse {
    0% {
      transform: scale(0.95);
      box-shadow: 0 0 0 0 rgba(255, 82, 82, 0.7);
    }
    
    70% {
      transform: scale(1);
      box-shadow: 0 0 0 8px rgba(255, 82, 82, 0);
    }
    
    100% {
      transform: scale(0.95);
      box-shadow: 0 0 0 0 rgba(255, 82, 82, 0);
    }
  }

  @keyframes blink {
    0%, 100% {
      opacity: 1;
    }
    50% {
      opacity: 0.4;
    }
  }
`;