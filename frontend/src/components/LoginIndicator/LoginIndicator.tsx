import { FC } from 'react';
import { Typography, Tooltip } from '@mui/material';
import { LoginStatusContainer, StatusDot } from './LoginIndicator.style';
import { ILoginIndicatorProps } from './LoginIndicator.types';
import { useLoginStatus } from '../../queries/useLoginStatus';

const LoginIndicator: FC<ILoginIndicatorProps> = ({ onNavigateToSettings }) => {
  const { data: loginStatus, isLoading } = useLoginStatus();
  const isLoggedIn = loginStatus?.authState === 'LoggedIn';
  const isLoggingIn = loginStatus?.authState === 'LoggingIn';
  const isNotConnected = !isLoggedIn && !isLoggingIn;
  const userEmail = loginStatus?.userEmail;

  const tooltipTitle = isLoading
    ? 'Checking login status...'
    : isLoggingIn
    ? 'OneDrive authorization in progress...'
    : isLoggedIn && userEmail
    ? `Connected as ${userEmail}`
    : isLoggedIn
    ? 'Connected to OneDrive'
    : 'Not connected to OneDrive - Click to go to Settings';

  return (
    <Tooltip title={tooltipTitle}>
      <LoginStatusContainer
        onClick={() => isNotConnected && onNavigateToSettings?.()}
        sx={{ cursor: isNotConnected ? 'pointer' : 'default' }}
      >
        <StatusDot data-isloggedin={isLoggedIn.toString()} data-loggingin={isLoggingIn.toString()} />
        <Typography
          variant="body2"
          color="inherit"
          sx={{ display: { xs: 'none', sm: 'block' } }}
        >
          {isLoggingIn
            ? 'Connecting...'
            : isLoggedIn && userEmail
            ? userEmail
            : isLoggedIn
            ? 'Connected'
            : 'Not Connected'
          }
        </Typography>
      </LoginStatusContainer>
    </Tooltip>
  );
};

export default LoginIndicator;