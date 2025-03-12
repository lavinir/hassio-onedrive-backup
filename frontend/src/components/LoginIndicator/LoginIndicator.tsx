import { FC } from 'react';
import { Typography, Tooltip } from '@mui/material';
import { LoginStatusContainer, StatusDot } from './LoginIndicator.style';
import { ILoginIndicatorProps } from './LoginIndicator.types';
import { useLoginStatus } from '../../queries/useLoginStatus';

const LoginIndicator: FC<ILoginIndicatorProps> = () => {
  const { data: loginStatus, isLoading } = useLoginStatus();
  const isLoggedIn = loginStatus?.isLoggedIn ?? false;

  const tooltipTitle = isLoading 
    ? 'Checking login status...'
    : isLoggedIn 
    ? 'Connected to OneDrive'
    : 'Not connected to OneDrive - Click Settings to connect';

  return (
    <Tooltip title={tooltipTitle}>
      <LoginStatusContainer>
        <StatusDot data-isloggedin={isLoggedIn.toString()} />
        <Typography 
          variant="body2" 
          color="inherit" 
          sx={{ display: { xs: 'none', sm: 'block' } }}
        >
          {isLoggedIn ? 'Connected' : 'Not Connected'}
        </Typography>
      </LoginStatusContainer>
    </Tooltip>
  );
};

export default LoginIndicator;