import styled from '@emotion/styled';
import { Card, Box, Avatar, Typography, Chip, Theme } from '@mui/material';

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
  margin-bottom: 12px;
  flex-wrap: wrap;
  gap: 8px;
`;

export const StatusAvatar = styled(Avatar)`
  width: 32px;
  height: 32px;
  margin-right: 12px;
  background-color: ${({ theme }: { theme: any }) => 
    theme.palette?.mode === 'dark' 
      ? 'rgba(255, 255, 255, 0.05)' 
      : 'rgba(0, 0, 0, 0.05)'
  };
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  
  & > svg {
    width: 18px;
    height: 18px;
    color: inherit;
  }
`;

export const ChipsContainer = styled(Box)`
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 16px;
`;

export const StyledChip = styled(Chip)`
  border-radius: 16px;
  font-size: 0.75rem;
  height: 24px;
  
  &.MuiChip-outlined {
    border-width: 1px;
  }

  &.MuiChip-sizeSmall {
    height: 20px;
  }
`;

export const RetentionBadge = styled(Box)`
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 8px;
  color: ${({ theme }: { theme: any }) => theme.palette?.success?.main || 'green'};
  
  & > svg {
    width: 18px;
    height: 18px;
  }
`;