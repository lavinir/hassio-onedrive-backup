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
  CircularProgress,
  Paper,
  Link,
  LinearProgress,
  Chip,
  Tooltip
} from '@mui/material'
import {
  Backup as BackupIcon,
  CloudSync as CloudSyncIcon,
  Refresh as RefreshIcon,
  Settings as SettingsIcon,
  DarkMode as DarkModeIcon,
  LightMode as LightModeIcon,
  FolderOpen as FolderOpenIcon,
  ArrowBack as ArrowBackIcon
} from '@mui/icons-material'
import BackupCard from './components/BackupCard'
import { useBackups } from './queries/useBackups'
import { useSyncStatus } from './queries/useSyncStatus'
import Settings from './pages/Settings'
import LoginIndicator from './components/LoginIndicator';
import { useLoginStatus } from './queries/useLoginStatus';
import { useTriggerBackup } from './mutations/useBackupMutations';
import CreateBackupModal from './components/CreateBackupModal';
import { useBuildInfo } from './queries/useBuildInfo';

function App() {
  const [refreshing, setRefreshing] = useState(false);
  const prefersDarkMode = useMediaQuery('(prefers-color-scheme: dark)');
  const [mode, setMode] = useState<'light' | 'dark'>(prefersDarkMode ? 'dark' : 'light');
  const [currentPage, setCurrentPage] = useState<'dashboard' | 'settings'>('dashboard');
  const { data: loginStatus } = useLoginStatus();
  const isLoggedIn = loginStatus?.authState === 'LoggedIn';
  const { data: backups = [], isLoading, refetch } = useBackups(isLoggedIn);
  const { data: syncStatus } = useSyncStatus();
  const { data: buildInfo } = useBuildInfo();
  const createBackupMutation = useTriggerBackup();
  const [createModalOpen, setCreateModalOpen] = useState(false);

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

        {isLoading && isLoggedIn ? (
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

      </>
    );
  };

  const syncStatusText = () => {
    if (!syncStatus) return null;
    if (syncStatus.activeBackupCreation) {
      return `Creating backup... ${Math.round(syncStatus.activeBackupCreation.progress * 100)}%`;
    }
    if (syncStatus.isSyncing) {
      if (syncStatus.activeUpload) return `Uploading ${syncStatus.activeUpload.slug}... ${syncStatus.activeUpload.progress}%`;
      if (syncStatus.activeDownload) return `Downloading ${syncStatus.activeDownload.slug}... ${syncStatus.activeDownload.progress}%`;
      return 'Syncing with OneDrive...';
    }
    if (syncStatus.lastSyncTime) return `Last sync: ${new Date(syncStatus.lastSyncTime).toLocaleString()}`;
    return 'Never synced';
  };

  const fileSyncStatusText = () => {
    const fs = syncStatus?.fileSyncState;
    if (!fs || (fs.state === 'Idle' && !fs.lastSyncTime)) return null;
    if (fs.state === 'Syncing') return 'Syncing files...';
    if (fs.lastSyncTime) return `Last file sync: ${new Date(fs.lastSyncTime).toLocaleString()}`;
    return null;
  };

  const isActiveProgress = !!(syncStatus?.isSyncing || syncStatus?.activeBackupCreation);

  const activeProgressValue = () => {
    if (syncStatus?.activeBackupCreation) return Math.round(syncStatus.activeBackupCreation.progress * 100);
    if (syncStatus?.activeUpload) return syncStatus.activeUpload.progress;
    if (syncStatus?.activeDownload) return syncStatus.activeDownload.progress;
    return null;
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
        <AppBar position="static" elevation={0}>
          <Toolbar>
            {currentPage === 'settings' && (
              <IconButton color="inherit" onClick={navigateToDashboard} sx={{ mr: 1 }}>
                <ArrowBackIcon />
              </IconButton>
            )}
            <IconButton color="inherit" onClick={navigateToDashboard} sx={{ p: 0 }}>
              <CloudSyncIcon sx={{ mr: 2 }} />
            </IconButton>
            <Box sx={{ flexGrow: 1, display: 'flex', alignItems: 'center', gap: 1, cursor: 'pointer' }} onClick={navigateToDashboard}>
              <Typography variant="h6" component="div">
                OneDrive Backup Dashboard
              </Typography>
              {buildInfo?.branch && buildInfo.branch !== 'main' && (
                <Chip
                  label={buildInfo.branch}
                  size="small"
                  sx={{
                    backgroundColor: buildInfo.branch === 'preview' ? '#ed6c02' : '#d32f2f',
                    color: '#fff',
                    fontWeight: 600,
                    fontSize: '0.7rem',
                    height: 20,
                  }}
                />
              )}
            </Box>
            <LoginIndicator onNavigateToSettings={navigateToSettings} />
            <Tooltip title={mode === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}>
              <IconButton
                color="inherit"
                onClick={toggleColorMode}
                sx={{ ml: 2, mr: 1 }}
              >
                {mode === 'light' ? <DarkModeIcon /> : <LightModeIcon />}
              </IconButton>
            </Tooltip>
            <Tooltip title="Refresh">
              <span>
                <IconButton
                  color="inherit"
                  onClick={handleRefresh}
                  disabled={refreshing}
                  sx={{ mr: 1 }}
                >
                  <RefreshIcon className={refreshing ? 'spin' : ''} />
                </IconButton>
              </span>
            </Tooltip>
            <Tooltip title="Settings">
              <IconButton
                color="inherit"
                onClick={navigateToSettings}
              >
                <SettingsIcon />
              </IconButton>
            </Tooltip>
          </Toolbar>
        </AppBar>

        <Paper
          elevation={1}
          square
          sx={{
            position: 'sticky',
            top: 0,
            zIndex: 1099,
            px: 3,
            py: 0.75,
            display: 'flex',
            flexDirection: 'column',
            gap: 0.5,
            borderBottom: 1,
            borderColor: 'divider',
          }}
        >
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            {isActiveProgress && <CircularProgress size={14} color="primary" />}
            <Typography variant="body2" color="text.secondary" sx={{ flexGrow: 1 }}>
              {syncStatusText()}
            </Typography>
            {isActiveProgress && activeProgressValue() !== null && (
              <LinearProgress
                variant="determinate"
                value={activeProgressValue()!}
                sx={{ width: 120, height: 6, borderRadius: 3 }}
              />
            )}
          </Box>
          {fileSyncStatusText() && (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
              {syncStatus?.fileSyncState?.state === 'Syncing' && <CircularProgress size={14} color="primary" />}
              {syncStatus?.fileSyncState?.state !== 'Syncing' && <FolderOpenIcon sx={{ fontSize: 14, color: 'text.disabled' }} />}
              <Typography variant="body2" color="text.secondary">
                {fileSyncStatusText()}
              </Typography>
            </Box>
          )}
        </Paper>

        <Container maxWidth="lg" sx={{ mt: 4, mb: 4, flex: 1 }}>
          {renderContent()}
        </Container>

        <Box
          component="footer"
          sx={{
            position: 'sticky',
            bottom: 0,
            zIndex: 100,
            py: 1.5,
            textAlign: 'center',
            borderTop: 1,
            borderColor: 'divider',
            color: 'text.disabled',
            backgroundColor: 'background.paper',
          }}
        >
          <Typography variant="caption">
            {'☕ '}
            <Link
              href="https://www.buymeacoffee.com/snirlavis"
              target="_blank"
              rel="noopener noreferrer"
              underline="hover"
              color="primary"
            >
              Support this project
            </Link>
          </Typography>
        </Box>
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
