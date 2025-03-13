import { FC } from 'react';
import { Typography, Tooltip } from '@mui/material';
import { LoginStatusContainer, StatusDot } from './LoginIndicator.style';
import { ILoginIndicatorProps } from './LoginIndicator.types';
import { useLoginStatus } from '../../queries/useLoginStatus';

const LoginIndicator: FC<ILoginIndicatorProps> = () => {
  const { data: loginStatus, isLoading } = useLoginStatus();
  const isLoggedIn = loginStatus?.authState === 'LoggedIn';
  const isLoggingIn = loginStatus?.authState === 'LoggingIn';

  const tooltipTitle = isLoading 
    ? 'Checking login status...'
    : isLoggingIn
    ? 'OneDrive authorization in progress...'
    : isLoggedIn 
    ? 'Connected to OneDrive'
    : 'Not connected to OneDrive - Click Settings to connect';

  return (
    <Tooltip title={tooltipTitle}>
      <LoginStatusContainer>
        <StatusDot data-isloggedin={isLoggedIn.toString()} data-loggingIn={isLoggingIn.toString()} />
        <Typography 
          variant="body2" 
          color="inherit" 
          sx={{ display: { xs: 'none', sm: 'block' } }}
        >
          {isLoggingIn ? 'Connecting...' : isLoggedIn ? 'Connected' : 'Not Connected'}
        </Typography>
      </LoginStatusContainer>
    </Tooltip>
  );
};

export default LoginIndicator;