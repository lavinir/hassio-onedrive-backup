import { useState, useEffect } from 'react';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import TextField from '@mui/material/TextField';
import AddIcon from '@mui/icons-material/Add';
import { useTriggerBackup } from '../../mutations/useBackupMutations';

// Adding a random comment to ensure code change is detected: RANDOM_CODE_1234

const BackupCreateButton = () => {
  const [open, setOpen] = useState(false);
  const [backupName, setBackupName] = useState('');
  const [error, setError] = useState('');
  const triggerBackupMutation = useTriggerBackup();

  // Let's try a different approach for click handling using an inline function
  return (
    <>
      <Button
        variant="contained"
        color="error" // Changed to error (red) to make it obvious if the change applies
        startIcon={<AddIcon />}
        onClick={() => {
          console.log("Button clicked directly from onClick");
          window.alert("Button clicked via inline handler!");
          setOpen(true);
        }}
      >
        RED TEST BUTTON
      </Button>
      
      <Dialog 
        open={open} 
        onClose={() => setOpen(false)}
      >
        <DialogTitle>Create New Backup</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Enter a name for your new backup:
          </DialogContentText>
          <TextField
            autoFocus
            margin="dense"
            id="name"
            label="Backup Name"
            type="text"
            fullWidth
            variant="outlined"
            value={backupName}
            onChange={(e) => setBackupName(e.target.value)}
            error={!!error}
            helperText={error}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)}>Cancel</Button>
          <Button 
            onClick={() => {
              if (!backupName.trim()) {
                setError('Backup name is required');
                return;
              }
              
              console.log('Creating backup with name:', backupName);
              triggerBackupMutation.mutate(backupName);
              setOpen(false);
            }} 
            color="primary"
            variant="contained"
          >
            Create
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
};

export default BackupCreateButton;
