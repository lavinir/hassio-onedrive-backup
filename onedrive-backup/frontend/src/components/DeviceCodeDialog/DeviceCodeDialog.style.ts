import styled from '@emotion/styled';
import { Button, Link, Theme } from '@mui/material';

export const StyledCode = styled.div`
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 12px 16px;
  background-color: ${({ theme }) =>
    (theme as Theme).palette.mode === 'dark'
      ? 'rgba(255, 255, 255, 0.05)'
      : 'rgba(0, 0, 0, 0.03)'
  };
  border-radius: 8px;
  font-family: monospace;
  font-size: 1.2em;
  margin: 8px 0;
`;

export const StyledButton = styled(Button)`
  min-width: 100px;
`;

export const StyledLink = styled(Link)`
  word-break: break-all;
  color: ${({ theme }) => (theme as Theme).palette.primary.main};
  
  &:hover {
    text-decoration: underline;
  }
`;