import styled from '@emotion/styled';
import { Card, Box, Avatar, Typography } from '@mui/material';

export const StyledCard = styled(Card)`
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  transition: transform 0.3s ease-in-out, box-shadow 0.3s ease-in-out;

  &:hover {
    transform: translateY(-4px);
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
  }
`;

export const CardHeader = styled(Box)`
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
`;

export const CardTitle = styled(Typography)`
  font-weight: bold;
`;

export const StatusContainer = styled(Box)`
  display: flex;
  align-items: center;
  margin-bottom: 8px;
`;

export const StatusAvatar = styled(Avatar)`
  width: 24px;
  height: 24px;
  margin-right: 8px;
`;

export const ChipsContainer = styled(Box)`
  display: flex;
  justify-content: space-between;
  margin-top: 16px;
`;