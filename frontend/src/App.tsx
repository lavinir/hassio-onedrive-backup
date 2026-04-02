import { useState, useMemo } from 'react'
import './App.css'
import { ThemeProvider, createTheme } from '@mui/material/styles'
import {
  Box,
  Container,
  Typography,
  Grid,
  AppBar,
  Toolbar,
  IconButton,
  CssBaseline,
  useMediaQuery,
  Button,
  CircularProgress
} from '@mui/material'
import {
  Backup as BackupIcon,
  CloudSync as CloudSyncIcon,
  Refresh as RefreshIcon,
  Settings as SettingsIcon,
  DarkMode as DarkModeIcon,
  LightMode as LightModeIcon
} from '@mui/icons-material'
import BackupCard from './components/BackupCard'
import { useBackups } from './queries/useBackups'
import { useSyncStatus } from './queries/useSyncStatus'
import Settings from './pages/Settings'
import LoginIndicator from './components/LoginIndicator';
import { useTriggerBackup } from './mutations/useBackupMutations';
import CreateBackupModal from './components/CreateBackupModal';

function App() {
  const [refreshing, setRefreshing] = useState(false);
  const prefersDarkMode = useMediaQuery('(prefers-color-scheme: dark)');
  const [mode, setMode] = useState<'light' | 'dark'>(prefersDarkMode ? 'dark' : 'light');
  const [currentPage, setCurrentPage] = useState<'dashboard' | 'settings'>('dashboard');
  const { data: backups = [], isLoading, refetch } = useBackups();
  const { data: syncStatus } = useSyncStatus();
  const createBackupMutation = useTriggerBackup();
  const [createModalOpen, setCreateModalOpen] = useState(false);

  console.log('App render:', { backups, isLoading, mode });  // Debug log

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

  const handleRefresh = async () => {
    setRefreshing(true);
    await refetch();
    setRefreshing(false);
  };

  const toggleColorMode = () => {
    setMode((prevMode) => (prevMode === 'light' ? 'dark' : 'light'));
  };

  const navigateToSettings = () => {
    setCurrentPage('settings');
  };

  const navigateToDashboard = () => {
    setCurrentPage('dashboard');
  };

  const handleCreateBackup = (backupName: string) => {
    createBackupMutation.mutate(backupName);
  };

  const renderContent = () => {
    if (currentPage === 'settings') {
      return <Settings />;
    }

    return (
      <>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 3, alignItems: 'center' }}>
          <Typography variant="h4" component="h1" gutterBottom>
            Backups
          </Typography>
          <Button
            variant="contained"
            startIcon={<BackupIcon />}
            onClick={() => setCreateModalOpen(true)}
          >
            Create Backup
          </Button>
        </Box>

        {isLoading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
            <CircularProgress />
          </Box>
        ) : (
          <Grid container spacing={3}>
            {backups?.map((backup) => (
              <Grid item xs={12} sm={6} md={4} key={backup.slug}>
                <BackupCard backup={backup} />
              </Grid>
            ))}
          </Grid>
        )}

        <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 1, mt: 4, color: 'text.secondary' }}>
          {syncStatus?.isSyncing && <CircularProgress size={14} color="inherit" />}
          <Typography variant="body2">
            {syncStatus?.isSyncing
              ? syncStatus.activeUpload
                ? `Uploading ${syncStatus.activeUpload.slug}... ${syncStatus.activeUpload.progress}%`
                : 'Syncing with OneDrive...'
              : syncStatus?.lastSyncTime
                ? `Last sync: ${new Date(syncStatus.lastSyncTime).toLocaleString()}`
                : 'Never synced'}
          </Typography>
        </Box>
      </>
    );
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box sx={{ flexGrow: 1 }}>
        <AppBar position="static" elevation={0}>
          <Toolbar>
            <IconButton color="inherit" onClick={navigateToDashboard} sx={{ p: 0 }}>
              <CloudSyncIcon sx={{ mr: 2 }} />
            </IconButton>
            <Typography variant="h6" component="div" sx={{ flexGrow: 1, cursor: 'pointer' }} onClick={navigateToDashboard}>
              OneDrive Backup Dashboard
            </Typography>
            <LoginIndicator />
            <IconButton
              color="inherit"
              onClick={toggleColorMode}
              title={mode === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}
              sx={{ ml: 2, mr: 1 }}
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
            <IconButton 
              color="inherit" 
              title="Settings"
              onClick={navigateToSettings}
            >
              <SettingsIcon />
            </IconButton>
          </Toolbar>
        </AppBar>

        <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
          {renderContent()}
        </Container>
      </Box>
      <CreateBackupModal 
        open={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        onConfirm={handleCreateBackup}
      />
    </ThemeProvider>
  )
}

export default App
