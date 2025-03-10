import styled from '@emotion/styled';
import { Box, Card, Paper, Divider as MuiDivider, TextField, Switch } from '@mui/material';

export const SettingsContainer = styled(Box)`
  display: flex;
  flex-direction: column;
  gap: 24px;
  width: 100%;
`;

export const SettingsCard = styled(Card)`
  border-radius: 12px;
  overflow: visible;
`;

export const SectionHeader = styled(Box)`
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 16px;
`;

export const SectionIcon = styled(Box)`
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background-color: ${({ theme }) => 
    theme.palette.mode === 'dark' 
      ? 'rgba(255, 255, 255, 0.08)' 
      : 'rgba(0, 0, 0, 0.04)'
  };
  color: ${({ theme }) => theme.palette.primary.main};
`;

export const FormSection = styled(Box)`
  display: flex;
  flex-direction: column;
  gap: 24px;
`;

export const FieldRow = styled(Box)`
  display: flex;
  flex-direction: column;
  gap: 8px;
  
  @media (min-width: 600px) {
    flex-direction: row;
    align-items: center;
  }
`;

export const FieldLabel = styled(Box)`
  font-weight: 500;
  min-width: 200px;
  flex-shrink: 0;
`;

export const FieldDescription = styled(Box)`
  color: ${({ theme }) => theme.palette.text.secondary};
  font-size: 0.875rem;
  margin-top: 4px;
`;

export const FieldInput = styled(Box)`
  flex-grow: 1;
  width: 100%;
  
  @media (min-width: 600px) {
    width: auto;
  }
`;

export const StyledTextField = styled(TextField)`
  width: 100%;
`;

export const StyledSwitch = styled(Switch)`
  .MuiSwitch-switchBase.Mui-checked {
    color: ${({ theme }) => theme.palette.primary.main};
  }
  
  .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track {
    background-color: ${({ theme }) => theme.palette.primary.main};
  }
`;

export const Divider = styled(MuiDivider)`
  margin: 24px 0;
`;

export const ButtonContainer = styled(Box)`
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  justify-content: flex-end;
  margin-top: 24px;
`;

export const ConnectionStatus = styled(Paper)`
  padding: 16px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 16px;
`;