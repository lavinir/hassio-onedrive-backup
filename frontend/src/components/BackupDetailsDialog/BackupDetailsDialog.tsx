import { FC } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  CircularProgress,
  Alert,
  Divider,
  Box,
  Chip,
  List,
  ListItem,
  ListItemText,
} from '@mui/material';
import { Backup } from '../../types/backup.types';
import { useBackupInfo } from '../../queries/useBackupInfo';

interface BackupDetailsDialogProps {
  backup: Backup;
  open: boolean;
  onClose: () => void;
}

const BackupDetailsDialog: FC<BackupDetailsDialogProps> = ({ backup, open, onClose }) => {
  const { data: info, isLoading, isError } = useBackupInfo(backup.slug, open);

  const renderContent = () => {
    if (isLoading) {
      return (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress />
        </Box>
      );
    }

    if (isError) {
      return <Alert severity="error">Failed to load backup details.</Alert>;
    }

    if (!info) return null;

    if (!info.isAvailableLocally) {
      return (
        <>
          <Alert severity="info" sx={{ mb: 2 }}>
            Full details are only available for backups present in Home Assistant. Download this backup to see complete information.
          </Alert>
          <DetailRow label="Slug" value={backup.slug} />
          <DetailRow label="Name" value={backup.name} />
          <DetailRow label="Date" value={new Date(backup.date).toLocaleString()} />
          <DetailRow label="Size" value={backup.size} />
          <DetailRow label="Type" value={backup.backup_type} />
        </>
      );
    }

    return (
      <>
        <DetailRow label="Slug" value={info.slug} />
        <DetailRow label="Name" value={info.name} />
        <DetailRow label="Date" value={info.date ? new Date(info.date).toLocaleString() : undefined} />
        <DetailRow label="Size" value={info.size !== undefined ? `${info.size.toFixed(2)} MB` : undefined} />
        <DetailRow label="Type" value={info.type} />
        <DetailRow label="Home Assistant" value={info.homeAssistantVersion} />
        <DetailRow label="Supervisor" value={info.supervisorVersion} />
        <Box sx={{ display: 'flex', gap: 1, mt: 1, mb: 1, flexWrap: 'wrap' }}>
          {info.compressed !== undefined && (
            <Chip label={info.compressed ? 'Compressed' : 'Uncompressed'} size="small" variant="outlined" />
          )}
          {info.isProtected !== undefined && (
            <Chip label={info.isProtected ? 'Password Protected' : 'No Password'} size="small" variant="outlined" />
          )}
          {info.homeAssistantExcludeDatabase !== undefined && info.homeAssistantExcludeDatabase && (
            <Chip label="Database Excluded" size="small" variant="outlined" color="warning" />
          )}
        </Box>

        {info.folders && info.folders.length > 0 && (
          <>
            <Divider sx={{ my: 1.5 }} />
            <Typography variant="subtitle2" gutterBottom>Folders</Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
              {info.folders.map(f => (
                <Chip key={f} label={f} size="small" />
              ))}
            </Box>
          </>
        )}

        {info.addons && info.addons.length > 0 && (
          <>
            <Divider sx={{ my: 1.5 }} />
            <Typography variant="subtitle2" gutterBottom>
              Addons ({info.addons.length})
            </Typography>
            <List dense disablePadding>
              {info.addons.map(addon => (
                <ListItem key={addon.slug} disableGutters sx={{ py: 0.25 }}>
                  <ListItemText
                    primary={addon.name}
                    secondary={addon.version}
                    primaryTypographyProps={{ variant: 'body2' }}
                    secondaryTypographyProps={{ variant: 'caption' }}
                  />
                </ListItem>
              ))}
            </List>
          </>
        )}
      </>
    );
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Backup Details</DialogTitle>
      <DialogContent dividers>{renderContent()}</DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
};

const DetailRow: FC<{ label: string; value?: string }> = ({ label, value }) => {
  if (value === undefined) return null;
  return (
    <Box sx={{ display: 'flex', gap: 1, mb: 0.5 }}>
      <Typography variant="body2" color="text.secondary" sx={{ minWidth: 130, flexShrink: 0 }}>
        {label}
      </Typography>
      <Typography variant="body2">{value}</Typography>
    </Box>
  );
};

export default BackupDetailsDialog;
