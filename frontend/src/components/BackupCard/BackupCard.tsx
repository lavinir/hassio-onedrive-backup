import { FC, useState } from 'react';
import { CardContent, CardActions, Button, Typography, IconButton } from '@mui/material';
import { MoreVert as MoreVertIcon } from '@mui/icons-material';

import { IBackupCardProps } from './BackupCard.types';
import { getStatusInfo } from './BackupCard.utils';
import { useDeleteBackup, useUploadBackup, useDownloadBackup } from '../../mutations/useBackupMutations';
import { useTransferProgress } from '../../hooks/useTransferProgress';
import ProgressIndicator from '../ProgressIndicator';
import { 
  StyledCard, 
  CardHeader, 
  CardTitle,
  StatusContainer,
  StatusAvatar,
  ChipsContainer,
  StyledChip
} from './BackupCard.style';
import { Box } from '@mui/material';

const BackupCard: FC<IBackupCardProps> = ({ backup }) => {
  const statusInfo = getStatusInfo(backup.status);
  const { mutate: deleteBackup } = useDeleteBackup();
  const { mutate: uploadBackup } = useUploadBackup();
  const { mutate: downloadBackup } = useDownloadBackup();
  const [action, setAction] = useState<'upload' | 'download' | null>(null);
  const [operationId, setOperationId] = useState<string | null>(null);
  const { progress, isComplete } = useTransferProgress(operationId);

  // Reset state when operation completes
  if (isComplete && operationId) {
    setOperationId(null);
    setAction(null);
  }

  const handleUpload = () => {
    if (backup.status === 'Local') {
      setAction('upload');
      uploadBackup([backup.slug, {
        onOperationStart: (id) => {
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
      setAction('download');
      downloadBackup([backup.slug, {
        onOperationStart: (id) => {
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

  return (
    <StyledCard>
      <CardContent>
        <CardHeader>
          <CardTitle variant="h6">
            {backup.name}
          </CardTitle>
          <IconButton size="small">
            <MoreVertIcon />
          </IconButton>
        </CardHeader>
        <StatusContainer>
          <Box display="flex" alignItems="center" sx={{ flexGrow: 1 }}>
            <StatusAvatar sx={{ color: theme => {
              const color = statusInfo.color;
              if (color === 'text.primary') {
                return theme.palette.text.primary;
              }
              return color;
            }}}>
              {statusInfo.icon}
            </StatusAvatar>
            <Typography variant="body2" color="text.secondary">
              {backup.status}
            </Typography>
          </Box>
          <Box>
            <StyledChip
              label={backup.source_type}
              size="small"
              color={
                backup.source_type === 'Automated' ? 'primary' 
                : backup.source_type === 'Manual' ? 'secondary'
                : 'warning'
              }
            />
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
          <StyledChip
            label={backup.backup_type}
            size="small"
            color="default"
            variant="outlined"
          />
        </ChipsContainer>
        {operationId && action && (
          <Box mt={2}>
            <ProgressIndicator progress={progress} action={action} />
          </Box>
        )}
      </CardContent>
      <CardActions>
        {backup.status === 'Local' && (
          <Button 
            size="small" 
            onClick={handleUpload}
            disabled={operationId !== null}
          >
            Upload
          </Button>
        )}
        {backup.status === 'OneDrive' && (
          <Button 
            size="small" 
            onClick={handleDownload}
            disabled={operationId !== null}
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
    </StyledCard>
  );
};

export default BackupCard;