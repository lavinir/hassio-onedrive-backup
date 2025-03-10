import { useState, useMemo } from 'react'
import './App.css'
import { ThemeProvider, createTheme } from '@mui/material/styles'
import {
  Box,
  Container,
  Typography,
  Grid,
  Card,
  CardContent,
  CardActions,
  Button,
  Chip,
  Avatar,
  AppBar,
  Toolbar,
  IconButton,
  CssBaseline,
  useMediaQuery
} from '@mui/material'
import {
  Backup as BackupIcon,
  CloudSync as CloudSyncIcon,
  MoreVert as MoreVertIcon,
  Refresh as RefreshIcon,
  Settings as SettingsIcon,
  Storage as StorageIcon,
  Cloud as CloudIcon,
  SyncAlt as SyncAltIcon,
  DarkMode as DarkModeIcon,
  LightMode as LightModeIcon
} from '@mui/icons-material'

// Mock data for backup tiles
const backups = [
  {
    id: 1,
    name: 'Daily Backup',
    date: '2023-10-15 08:00 AM',
    size: '1.2 GB',
    status: 'Synced',
    type: 'Automated',
    path: '/backups/daily'
  },
  {
    id: 2,
    name: 'Weekly Backup',
    date: '2023-10-14 07:30 AM',
    size: '4.5 GB',
    status: 'Local',
    type: 'Automated',
    path: '/backups/weekly'
  },
  {
    id: 3,
    name: 'Monthly Backup',
    date: '2023-10-01 06:00 AM',
    size: '10.8 GB',
    status: 'OneDrive',
    type: 'Automated',
    path: '/backups/monthly'
  },
  {
    id: 4,
    name: 'Manual Backup',
    date: '2023-10-10 03:45 PM',
    size: '2.3 GB',
    status: 'Synced',
    type: 'Manual',
    path: '/backups/manual/oct10'
  },
  {
    id: 5,
    name: 'Config Backup',
    date: '2023-10-12 11:30 AM',
    size: '0.5 GB',
    status: 'In Progress',
    type: 'Manual',
    path: '/backups/config'
  }
];

// Helper function to get status icon and color
const getStatusInfo = (status: string) => {
  switch (status) {
    case 'In Progress':
      return {
        icon: <CloudSyncIcon fontSize="small" />,
        color: 'warning.main',
        tooltip: 'Currently syncing to OneDrive'
      };
    case 'Local':
      return {
        icon: <StorageIcon fontSize="small" />,
        color: 'info.main',
        tooltip: 'Exists locally only'
      };
    case 'OneDrive':
      return {
        icon: <CloudIcon fontSize="small" />,
        color: '#0078d4',
        tooltip: 'Exists on OneDrive only'
      };
    case 'Synced':
      return {
        icon: <SyncAltIcon fontSize="small" />,
        color: 'success.main',
        tooltip: 'Synced to both local and OneDrive'
      };
    default:
      return {
        icon: <CloudSyncIcon fontSize="small" />,
        color: 'text.secondary',
        tooltip: 'Unknown status'
      };
  }
};

function App() {
  const [refreshing, setRefreshing] = useState(false);
  const prefersDarkMode = useMediaQuery('(prefers-color-scheme: dark)');
  const [mode, setMode] = useState<'light' | 'dark'>(prefersDarkMode ? 'dark' : 'light');

  const theme = useMemo(() =>
    createTheme({
      palette: {
        mode,
        primary: {
          main: '#0078d4', // OneDrive blue
        },
        secondary: {
          main: '#2b579a', // Slightly darker blue
        },
        background: {
          default: mode === 'light' ? '#f5f5f5' : '#121212',
          paper: mode === 'light' ? '#ffffff' : '#1e1e1e',
        },
      },
      typography: {
        fontFamily: '"Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
        h4: {
          fontWeight: 600,
        },
      },
      components: {
        MuiCard: {
          styleOverrides: {
            root: {
              borderRadius: 12,
              boxShadow: '0 4px 12px rgba(0,0,0,0.08)',
              transition: 'transform 0.3s ease-in-out, box-shadow 0.3s ease-in-out',
              '&:hover': {
                transform: 'translateY(-4px)',
                boxShadow: '0 8px 24px rgba(0,0,0,0.12)',
              },
            },
          },
        },
      },
    }),
    [mode]);

  const handleRefresh = () => {
    setRefreshing(true);
    // Simulate refresh
    setTimeout(() => {
      setRefreshing(false);
    }, 2000);
  };

  const toggleColorMode = () => {
    setMode((prevMode) => (prevMode === 'light' ? 'dark' : 'light'));
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box sx={{ flexGrow: 1 }}>
        <AppBar position="static" elevation={0}>
          <Toolbar>
            <CloudSyncIcon sx={{ mr: 2 }} />
            <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
              OneDrive Backup Dashboard
            </Typography>
            <IconButton
              color="inherit"
              onClick={toggleColorMode}
              title={mode === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}
              sx={{ mr: 1 }}
            >
              {mode === 'light' ? <DarkModeIcon /> : <LightModeIcon />}
            </IconButton>
            <IconButton
              color="inherit"
              onClick={handleRefresh}
              disabled={refreshing}
              title="Refresh"
              sx={{ mr: 1 }}
            >
              <RefreshIcon className={refreshing ? 'spin' : ''} />
            </IconButton>
            <IconButton color="inherit" title="Settings">
              <SettingsIcon />
            </IconButton>
          </Toolbar>
        </AppBar>

        <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 3, alignItems: 'center' }}>
            <Typography variant="h4" component="h1" gutterBottom>
              Your Backups
            </Typography>
            <Button
              variant="contained"
              startIcon={<BackupIcon />}
            >
              Create Backup
            </Button>
          </Box>

          <Grid container spacing={3}>
            {backups.map((backup) => {
              const statusInfo = getStatusInfo(backup.status);

              return (
                <Grid item xs={12} sm={6} md={4} key={backup.id}>
                  <Card>
                    <CardContent>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 2 }}>
                        <Typography variant="h6" component="div" sx={{ fontWeight: 'bold' }}>
                          {backup.name}
                        </Typography>
                        <IconButton size="small">
                          <MoreVertIcon />
                        </IconButton>
                      </Box>

                      <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }} title={statusInfo.tooltip}>
                        <Avatar sx={{ width: 24, height: 24, mr: 1, bgcolor: statusInfo.color }}>
                          {statusInfo.icon}
                        </Avatar>
                        <Typography variant="body2" color="text.secondary">
                          {backup.status}
                        </Typography>
                      </Box>

                      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                        {backup.date}
                      </Typography>

                      <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 2 }}>
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
                      </Box>
                    </CardContent>
                    <CardActions>
                      <Button size="small">Restore</Button>
                      <Button size="small">Download</Button>
                      <Button size="small" color="error">Delete</Button>
                    </CardActions>
                  </Card>
                </Grid>
              );
            })}
          </Grid>

          <Box sx={{ textAlign: 'center', mt: 4, color: 'text.secondary' }}>
            <Typography variant="body2">
              Last sync: Today at 12:45 PM
            </Typography>
          </Box>
        </Container>
      </Box>
    </ThemeProvider>
  )
}

export default App
