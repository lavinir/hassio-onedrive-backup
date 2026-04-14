import { FC, useState } from 'react';
import {
  DialogTitle,
  DialogActions,
  Button
} from '@mui/material';
import { ICreateBackupModalProps } from './CreateBackupModal.types';
import { 
  StyledDialog, 
  StyledDialogContent, 
  StyledTextField 
} from './CreateBackupModal.style';

const CreateBackupModal: FC<ICreateBackupModalProps> = ({ 
  open, 
  onClose, 
  onConfirm 
}) => {
  const [backupName, setBackupName] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = () => {
    if (!backupName.trim()) {
      setError('Backup name is required');
      return;
    }
    onConfirm(backupName);
    setBackupName('');
    setError('');
    onClose();
  };

  const handleClose = () => {
    setBackupName('');
    setError('');
    onClose();
  };

  return (
    <StyledDialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle>Create Backup</DialogTitle>
      <StyledDialogContent>
        <StyledTextField
          autoFocus
          label="Backup Name"
          fullWidth
          value={backupName}
          onChange={(e) => {
            setBackupName(e.target.value);
            if (e.target.value.trim()) setError('');
          }}
          error={!!error}
          helperText={error}
          margin="dense"
          placeholder="Enter a name for your backup"
        />
      </StyledDialogContent>
      <DialogActions>
        <Button onClick={handleClose} color="primary">
          Cancel
        </Button>
        <Button onClick={handleSubmit} color="primary" variant="contained">
          Create
        </Button>
      </DialogActions>
    </StyledDialog>
  );
};

export default CreateBackupModal;
