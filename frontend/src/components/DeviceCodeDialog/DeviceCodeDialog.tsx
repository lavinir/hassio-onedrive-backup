import { FC } from 'react';
import { Dialog, DialogTitle, DialogContent, DialogActions, Button, Typography, Box } from '@mui/material';
import { ContentCopy as CopyIcon } from '@mui/icons-material';
import { StyledCode, StyledButton, StyledLink } from './DeviceCodeDialog.style';
import { IDeviceCodeDialogProps } from './DeviceCodeDialog.types';

const DeviceCodeDialog: FC<IDeviceCodeDialogProps> = ({ open, onClose, verificationUrl, userCode }) => {
  const handleCopyCode = () => {
    navigator.clipboard.writeText(userCode);
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Connect to OneDrive</DialogTitle>
      <DialogContent>
        <Typography variant="body1" gutterBottom>
          To connect your OneDrive account, follow these steps:
        </Typography>
        <Box component="ol" sx={{ pl: 2 }}>
          <li>
            <Typography variant="body1" gutterBottom>
              Visit <StyledLink href={verificationUrl} target="_blank" rel="noopener noreferrer">
                {verificationUrl}
              </StyledLink>
            </Typography>
          </li>
          <li>
            <Typography variant="body1" gutterBottom>
              Enter this code when prompted:
            </Typography>
            <StyledCode>
              {userCode}
              <StyledButton onClick={handleCopyCode} startIcon={<CopyIcon />}>
                Copy
              </StyledButton>
            </StyledCode>
          </li>
          <li>
            <Typography variant="body1">
              Sign in with your Microsoft Account and follow the instructions
            </Typography>
          </li>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
};

export default DeviceCodeDialog;