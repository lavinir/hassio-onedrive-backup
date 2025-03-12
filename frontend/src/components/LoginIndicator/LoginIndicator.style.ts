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
}

export const StatusDot = styled(Box)<StatusDotProps>`
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background-color: ${({ theme, 'data-isloggedin': isLoggedIn }) =>
    isLoggedIn === 'true'
      ? theme?.palette?.success?.main || '#4caf50' 
      : theme?.palette?.error?.main || '#f44336'};
  animation: ${({ 'data-isloggedin': isLoggedIn }) =>
    isLoggedIn === 'true' ? 'none' : 'pulse 2s infinite'};

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
`;