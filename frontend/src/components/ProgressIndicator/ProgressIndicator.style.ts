import styled from '@emotion/styled';
import { Box, LinearProgress } from '@mui/material';

export const ProgressContainer = styled(Box)`
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
`;

export const StyledProgress = styled(LinearProgress)`
  height: 8px;
  border-radius: 4px;
`;