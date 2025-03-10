import { FC } from 'react';
import { CardContent, CardActions, Button, Typography, IconButton, Chip } from '@mui/material';
import { MoreVert as MoreVertIcon } from '@mui/icons-material';

import { IBackupCardProps } from './BackupCard.types';
import { getStatusInfo } from './BackupCard.utils';
import { 
  StyledCard, 
  CardHeader, 
  CardTitle,
  StatusContainer,
  StatusAvatar,
  ChipsContainer
} from './BackupCard.style';

const BackupCard: FC<IBackupCardProps> = ({ backup }) => {
  const statusInfo = getStatusInfo(backup.status);
  const StatusIcon = statusInfo.icon;

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
        <StatusContainer title={statusInfo.tooltip}>
          <StatusAvatar sx={{ bgcolor: statusInfo.color }}>
            <StatusIcon fontSize="small" />
          </StatusAvatar>
          <Typography variant="body2" color="text.secondary">
            {backup.status}
          </Typography>
        </StatusContainer>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          {backup.date}
        </Typography>
        <ChipsContainer>
          <Chip
            label={backup.size}
            size="small"
            variant="outlined"
          />
          <Chip
            label={backup.type}
            size="small"
            color={backup.type === 'Automated' ? 'primary' : 'secondary'}
          />
        </ChipsContainer>
      </CardContent>
      <CardActions>
        <Button size="small">Restore</Button>
        <Button size="small">Download</Button>
        <Button size="small" color="error">Delete</Button>
      </CardActions>
    </StyledCard>
  );
};

export default BackupCard;