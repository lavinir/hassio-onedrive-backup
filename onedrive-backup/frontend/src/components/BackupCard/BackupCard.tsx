import { FC, useState, useEffect } from 'react';
import { CardContent, CardActions, Button, Typography, IconButton, Menu, MenuItem, ListItemIcon, ListItemText, Divider, Tooltip, Alert } from '@mui/material';
import { 
  MoreVert as MoreVertIcon,
  PushPin as PinIcon,
  PushPinOutlined as UnpinIcon,
  Info as InfoIcon,
  CloudOutlined as CloudIcon,
  Storage as StorageIcon
} from '@mui/icons-material';

import { IBackupCardProps } from './BackupCard.types';
import { getStatusInfo } from './BackupCard.utils';
import { useDeleteBackup, useUploadBackup, useDownloadBackup, useUpdateBackupRetention } from '../../mutations/useBackupMutations';
import { useTransferProgress } from '../../hooks/useTransferProgress';
import ProgressIndicator from '../ProgressIndicator';
import BackupDetailsDialog from '../BackupDetailsDialog';
import { 
  StyledCard, 
  CardHeader, 
  CardTitle,
  StatusContainer,
  StatusAvatar,
  ChipsContainer,
  StyledChip,
  RetentionBadge
} from './BackupCard.style';
import { Box } from '@mui/material';

const BackupCard: FC<IBackupCardProps> = ({ backup }) => {
  const statusInfo = getStatusInfo(backup.status);
  const { mutate: deleteBackup } = useDeleteBackup();
  const { mutate: uploadBackup } = useUploadBackup();
  const { mutate: downloadBackup } = useDownloadBackup();
  const { mutate: updateRetention } = useUpdateBackupRetention();
  const [action, setAction] = useState<'upload' | 'download' | null>(null);
  const [operationId, setOperationId] = useState<string | null>(null);
  const { progress, isComplete, isFailed } = useTransferProgress(operationId);

  // Menu state
  const [menuAnchorEl, setMenuAnchorEl] = useState<null | HTMLElement>(null);
  const isMenuOpen = Boolean(menuAnchorEl);
  const [detailsOpen, setDetailsOpen] = useState(false);

  // Clear operation state once it completes successfully
  useEffect(() => {
    if (isComplete && operationId && !isFailed) {
      setOperationId(null);
      setAction(null);
    }
  }, [isComplete, operationId, isFailed]);

  const handleUpload = () => {
    if (backup.status === 'Local') {
      uploadBackup([backup.slug, {
        onOperationStart: (id) => {
          setAction('upload');
          setOperationId(id);
        },
        onError: () => {
          setOperationId(null);
          setAction(null);
        },
      }]);
    }
  };

  const handleDownload = () => {
    if (backup.status === 'OneDrive') {
      downloadBackup([backup.slug, {
        onOperationStart: (id) => {
          setAction('download');
          setOperationId(id);
        },
        onError: () => {
          setOperationId(null);
          setAction(null);
        },
      }]);
    }
  };

  const handleDelete = () => {
    deleteBackup(backup.slug);
  };

  // Reset the failed state
  const handleDismissError = () => {
    setOperationId(null);
    setAction(null);
  };

  // Menu handlers
  const handleMenuOpen = (event: React.MouseEvent<HTMLElement>) => {
    setMenuAnchorEl(event.currentTarget);
  };

  const handleMenuClose = () => {
    setMenuAnchorEl(null);
  };

  const handleRetainBackup = () => {
    updateRetention({ slugId: backup.slug, retain: !backup.retained });
    handleMenuClose();
  };

  const handleBackupDetails = () => {
    setDetailsOpen(true);
    handleMenuClose();
  };

  return (
    <StyledCard>
      <CardContent>
        <CardHeader>
          <Box display="flex" alignItems="center" sx={{ minWidth: 0, flex: 1, overflow: 'hidden' }}>
            {backup.retained && (
              <Tooltip title="This backup is retained and won't be automatically deleted">
                <RetentionBadge>
                  <PinIcon fontSize="small" />
                </RetentionBadge>
              </Tooltip>
            )}
            <CardTitle variant="h6">
              {backup.name}
            </CardTitle>
          </Box>
          <IconButton size="small" onClick={handleMenuOpen} aria-label="backup options" sx={{ flexShrink: 0, ml: 1 }}>
            <MoreVertIcon />
          </IconButton>
          <Menu
            anchorEl={menuAnchorEl}
            open={isMenuOpen}
            onClose={handleMenuClose}
            slotProps={{ list: { 'aria-labelledby': 'backup-options-button' } }}
            anchorOrigin={{
              vertical: 'bottom',
              horizontal: 'right',
            }}
            transformOrigin={{
              vertical: 'top',
              horizontal: 'right',
            }}
          >
            <MenuItem onClick={handleRetainBackup}>
              <ListItemIcon>
                {backup.retained ? <UnpinIcon fontSize="small" /> : <PinIcon fontSize="small" />}
              </ListItemIcon>
              <ListItemText primary={backup.retained ? 'Remove Retention' : 'Retain Backup'} />
            </MenuItem>
            <Divider />
            <MenuItem onClick={handleBackupDetails}>
              <ListItemIcon>
                <InfoIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText primary="Backup Details" />
            </MenuItem>
          </Menu>
        </CardHeader>
        <StatusContainer>
          <Box display="flex" alignItems="center" sx={{ flexGrow: 1 }}>
            <Tooltip title={statusInfo.tooltip}>
              <StatusAvatar sx={{ color: theme => {
                const color = statusInfo.color;
                if (color === 'text.primary') {
                  return theme.palette.text.primary;
                }
                return color;
              }}}>
                {statusInfo.icon}
              </StatusAvatar>
            </Tooltip>
            <Typography variant="body2" color="text.secondary">
              {backup.status}
            </Typography>
          </Box>
        </StatusContainer>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          {backup.date}
        </Typography>
        <ChipsContainer>
          <StyledChip
            label={backup.size}
            size="small"
            variant="outlined"
          />
          {backup.type && (
            <StyledChip
              label={backup.type}
              size="small"
              color="default"
              variant="outlined"
            />
          )}
          {backup.retained && (
            <StyledChip
              label="Retained"
              size="small"
              color="success"
              variant="outlined"
              icon={<PinIcon fontSize="small" />}
            />
          )}
        </ChipsContainer>
        {operationId && action && (
          <Box mt={2}>
            {isFailed ? (
              <Alert 
                severity="error" 
                onClose={handleDismissError}
                sx={{ mb: 1 }}
              >
                {action === 'upload' ? 'Upload' : 'Download'} failed. See logs for details.
              </Alert>
            ) : (
              <ProgressIndicator progress={progress} action={action} />
            )}
          </Box>
        )}
      </CardContent>
      <CardActions>
        {backup.status === 'Local' && (
          <Button 
            size="small" 
            onClick={handleUpload}
            disabled={operationId !== null}
            startIcon={<CloudIcon />}
          >
            Upload
          </Button>
        )}
        {backup.status === 'OneDrive' && (
          <Button 
            size="small" 
            onClick={handleDownload}
            disabled={operationId !== null}
            startIcon={<StorageIcon />}
          >
            Download
          </Button>
        )}
        <Button 
          size="small" 
          color="error" 
          onClick={handleDelete}
          disabled={operationId !== null}
        >
          Delete
        </Button>
      </CardActions>
      <BackupDetailsDialog
        backup={backup}
        open={detailsOpen}
        onClose={() => setDetailsOpen(false)}
      />
    </StyledCard>
  );
};

export default BackupCard;