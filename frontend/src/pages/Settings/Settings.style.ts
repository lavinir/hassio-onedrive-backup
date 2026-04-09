import styled from '@emotion/styled';
import { Box, Card, Paper, Divider as MuiDivider, TextField, Switch, Theme as MuiTheme, Button } from '@mui/material';

// Type for styled components that need theme
interface ThemeProps {
  theme?: MuiTheme;
}

export const SettingsContainer = styled(Box)`
  display: flex;
  flex-direction: column;
  gap: 24px;
  width: 100%;
  max-width: 1000px;
  margin: 0 auto;
  padding: 16px;
`;

export const SettingsCard = styled(Card)`
  border-radius: 12px;
  overflow: visible;
  margin-bottom: 32px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.06);
`;

export const SectionHeader = styled(Box)`
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 28px;
  border-bottom: 1px solid ${({ theme }: ThemeProps) => 
    theme?.palette?.mode === 'dark' 
      ? 'rgba(255, 255, 255, 0.12)' 
      : 'rgba(0, 0, 0, 0.08)'
  };
  padding-bottom: 16px;
`;

export const SectionIcon = styled(Box)`
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  border-radius: 10px;
  background-color: ${({ theme }: ThemeProps) => 
    theme?.palette?.mode === 'dark' 
      ? 'rgba(255, 255, 255, 0.08)' 
      : 'rgba(0, 0, 0, 0.04)'
  };
  color: ${({ theme }: ThemeProps) => theme?.palette?.primary?.main};
`;

export const FormSection = styled(Box)`
  display: flex;
  flex-direction: column;
  gap: 24px;
`;

export const FieldRow = styled(Box)`
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px 0;
  border-bottom: 1px solid ${({ theme }: ThemeProps) => 
    theme?.palette?.mode === 'dark' 
      ? 'rgba(255, 255, 255, 0.05)' 
      : 'rgba(0, 0, 0, 0.03)'
  };
  
  &:last-child {
    border-bottom: none;
    padding-bottom: 0;
  }
  
  @media (min-width: 768px) {
    flex-direction: row;
    align-items: flex-start;
    padding: 16px 0;
  }
`;

export const FieldLabel = styled(Box)`
  font-weight: 500;
  min-width: 240px;
  flex-shrink: 0;
  
  @media (min-width: 768px) {
    padding-top: 8px;
  }
`;

export const FieldDescription = styled(Box)`
  color: ${({ theme }: ThemeProps) => theme?.palette?.text?.secondary};
  font-size: 0.875rem;
  margin-top: 4px;
  max-width: 600px;
`;

export const FieldInput = styled(Box)`
  flex-grow: 1;
  width: 100%;
  
  @media (min-width: 768px) {
    width: auto;
    max-width: calc(100% - 260px);
    padding-left: 16px;
  }
  
  /* Add consistent spacing to form controls */
  .MuiFormControlLabel-root {
    margin-left: -9px;
    display: block;
    margin-bottom: 8px;
  }
  
  .MuiList-root {
    padding: 0;
    background-color: ${({ theme }: ThemeProps) => 
      theme?.palette?.mode === 'dark' 
        ? 'rgba(255, 255, 255, 0.03)' 
        : 'rgba(0, 0, 0, 0.02)'
    };
    border-radius: 8px;
    margin-top: 8px;
  }
  
  .MuiListItem-root {
    padding: 8px 16px;
    border-bottom: 1px solid ${({ theme }: ThemeProps) => 
      theme?.palette?.mode === 'dark' 
        ? 'rgba(255, 255, 255, 0.05)' 
        : 'rgba(0, 0, 0, 0.03)'
    };
    
    &:last-child {
      border-bottom: none;
    }
  }
`;

export const FieldGroup = styled(Box)`
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  width: 100%;
  
  & > * {
    min-width: 140px;
    flex: 1;
  }
`;

export const StyledTextField = styled(TextField)`
  width: 100%;
  
  .MuiOutlinedInput-root {
    background-color: ${({ theme }: ThemeProps) => 
      theme?.palette?.mode === 'dark' 
        ? 'rgba(255, 255, 255, 0.05)' 
        : 'rgba(0, 0, 0, 0.02)'
    };
    border-radius: 8px;
  }
`;

export const StyledSwitch = styled(Switch)`
  .MuiSwitch-switchBase.Mui-checked {
    color: ${({ theme }: ThemeProps) => theme?.palette?.primary?.main};
  }
  
  .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track {
    background-color: ${({ theme }: ThemeProps) => theme?.palette?.primary?.main};
  }
`;

export const Divider = styled(MuiDivider)`
  margin: 40px 0;
  opacity: 0.6;
`;

export const ButtonContainer = styled(Box)`
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  justify-content: flex-end;
  margin-top: 40px;
  padding: 0 16px 16px;
`;

export const ConnectionStatus = styled(Paper)`
  padding: 16px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 16px;
`;

export const LoadingContainer = styled(Box)`
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 400px;
  width: 100%;
`;

export const ConnectButton = styled(Button)`
  margin-top: 16px;
  min-width: 200px;
`;

export const ConnectionStatusBox = styled(Box)<ThemeProps>`
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  border-radius: 8px;
  background-color: ${({ theme }) =>
    theme?.palette?.mode === 'dark'
      ? 'rgba(255, 255, 255, 0.05)'
      : 'rgba(0, 0, 0, 0.03)'
  };
  margin-top: 16px;
`;