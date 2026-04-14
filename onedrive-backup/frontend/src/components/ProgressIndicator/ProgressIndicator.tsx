import { FC } from 'react';
import { Typography } from '@mui/material';
import { IProgressIndicatorProps } from './ProgressIndicator.types';
import { ProgressContainer, StyledProgress } from './ProgressIndicator.style';

const ProgressIndicator: FC<IProgressIndicatorProps> = ({ progress, action }) => {
  return (
    <ProgressContainer>
      <Typography variant="body2" color="text.secondary">
        {action === 'upload' ? 'Uploading' : 'Downloading'}: {progress}%
      </Typography>
      <StyledProgress variant="determinate" value={progress} />
    </ProgressContainer>
  );
};

export default ProgressIndicator;