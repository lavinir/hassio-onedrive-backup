import { FC, useState, useEffect } from 'react';
import {
  Typography,
  CardContent,
  FormControlLabel,
  Button,
  Slider,
  Select,
  MenuItem,
  TextField,
  InputAdornment,
  IconButton,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  CircularProgress,
  Alert,
} from '@mui/material';
import {
  Backup as BackupIcon,
  FolderOpen as FolderIcon,
  Settings as GeneralIcon,
  Save as SaveIcon,
  Add as AddIcon,
  Delete as DeleteIcon,
  Visibility as VisibilityIcon,
  VisibilityOff as VisibilityOffIcon,
  Cloud as CloudIcon,
} from '@mui/icons-material';

import { ISettingsProps } from './Settings.types';
import { useSettings } from '../../queries/useSettings';
import { useLoginStatus } from '../../queries/useLoginStatus';
import { useUpdateSettings, useOneDriveAuth, useDisconnectOneDrive } from '../../mutations/useSettingsMutations';
import {
  SettingsContainer,
  SettingsCard,
  SectionHeader,
  SectionIcon,
  FormSection,
  FieldRow,
  FieldLabel,
  FieldDescription,
  FieldInput,
  FieldGroup,
  StyledTextField,
  StyledSwitch,
  Divider,
  ButtonContainer,
  LoadingContainer,
  ConnectButton,
} from './Settings.style';
import { ISettingsForm } from '../../types/settings.types';
import DeviceCodeDialog from '../../components/DeviceCodeDialog';

const Settings: FC<ISettingsProps> = () => {
  const { data: settings, isLoading: isSettingsLoading } = useSettings();
  const { data: loginStatus, isLoading: isLoginLoading } = useLoginStatus();
  const updateSettings = useUpdateSettings();
  const oneDriveAuth = useOneDriveAuth();
  const disconnectOneDrive = useDisconnectOneDrive();
  
  const [showPassword, setShowPassword] = useState(false);
  const [newSyncPath, setNewSyncPath] = useState('');
  const [newExcludedAddon, setNewExcludedAddon] = useState('');
  const [deviceCodeData, setDeviceCodeData] = useState<{ verificationUrl: string; userCode: string } | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  type SettingsSectionKey = keyof ISettingsForm;

  const handleInputChange = (section: SettingsSectionKey, field: string) => (
    e: React.ChangeEvent<HTMLInputElement>
  ) => {
    const { value, type, checked } = e.target;
    updateSettings.setData(prev => {
      if (!prev) return prev;
      
      return {
        ...prev,
        [section]: {
          ...prev[section],
          [field]: type === 'checkbox' ? checked : value
        }
      };
    });
  };

  const handleSliderChange = (section: SettingsSectionKey, field: string) => (
    _: Event | React.SyntheticEvent,
    newValue: number | number[]
  ) => {
    updateSettings.setData(prev => {
      if (!prev) return prev;
      
      return {
        ...prev,
        [section]: {
          ...prev[section],
          [field]: newValue
        }
      };
    });
  };

  const handleAddSyncPath = () => {
    if (!settings || !newSyncPath.trim()) return;
    
    updateSettings.setData(prev => {
      if (!prev) return prev;
      
      return {
        ...prev,
        fileSync: {
          ...prev.fileSync,
          syncPaths: [...prev.fileSync.syncPaths, newSyncPath.trim()]
        }
      };
    });
    setNewSyncPath('');
  };

  const handleRemoveSyncPath = (index: number) => {
    if (!settings) return;
    
    updateSettings.setData(prev => {
      if (!prev) return prev;
      
      return {
        ...prev,
        fileSync: {
          ...prev.fileSync,
          syncPaths: prev.fileSync.syncPaths.filter((_, i) => i !== index)
        }
      };
    });
  };

  const handleAddExcludedAddon = () => {
    if (!settings || !newExcludedAddon.trim()) return;
    
    updateSettings.setData(prev => {
      if (!prev) return prev;
      
      return {
        ...prev,
        backup: {
          ...prev.backup,
          excludedAddons: [...prev.backup.excludedAddons, newExcludedAddon.trim()]
        }
      };
    });
    setNewExcludedAddon('');
  };

  const handleRemoveExcludedAddon = (index: number) => {
    if (!settings) return;
    
    updateSettings.setData(prev => {
      if (!prev) return prev;
      
      return {
        ...prev,
        backup: {
          ...prev.backup,
          excludedAddons: prev.backup.excludedAddons.filter((_, i) => i !== index)
        }
      };
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaveError(null);
    setSaveSuccess(false);
    try {
      if (updateSettings.data) {
        await updateSettings.mutateAsync(updateSettings.data);
        setSaveSuccess(true);
        setTimeout(() => setSaveSuccess(false), 4000);
      }
    } catch (error) {
      setSaveError('Failed to save settings. Please try again.');
    }
  };

  const handleConnect = async () => {
    try {
      const { verificationUrl, userCode } = await oneDriveAuth.mutateAsync();
      setDeviceCodeData({ verificationUrl, userCode });
    } catch (error) {
      console.error('Failed to initiate OneDrive authentication:', error);
    }
  };

  const handleDisconnect = async () => {
    try {
      await disconnectOneDrive.mutateAsync();
    } catch (error) {
      console.error('Failed to disconnect from OneDrive:', error);
    }
  };

  const handleCloseDeviceCodeDialog = () => {
    setDeviceCodeData(null);
  };

  useEffect(() => {
    if (loginStatus?.authState === 'LoggedIn' && deviceCodeData !== null) {
      setDeviceCodeData(null);
    }
  }, [loginStatus?.authState]);

  if (isSettingsLoading || !settings) {
    return (
      <LoadingContainer>
        <CircularProgress />
        <Typography variant="h6" sx={{ mt: 2 }}>
          Loading settings...
        </Typography>
      </LoadingContainer>
    );
  }

  return (
    <SettingsContainer theme={settings.theme}>
      <Typography variant="h4" component="h1" gutterBottom>
        Settings
      </Typography>
      
      <form onSubmit={handleSubmit}>
        {/* OneDrive Connection Section */}
        <SettingsCard>
          <CardContent>
            <SectionHeader theme={settings.theme}>
              <SectionIcon theme={settings.theme}>
                <CloudIcon />
              </SectionIcon>
              <div>
                <Typography variant="h6">OneDrive Connection</Typography>
                <Typography variant="body2" color="text.secondary">
                  Manage your OneDrive account connection
                </Typography>
              </div>
            </SectionHeader>

            <FormSection>
              <FieldRow theme={settings.theme}>
                <FieldLabel theme={settings.theme}>
                  <Typography>Connection Status</Typography>
                  <FieldDescription theme={settings.theme}>
                    Your OneDrive connection status and controls
                  </FieldDescription>
                </FieldLabel>
                <FieldInput theme={settings.theme}>
                  {isLoginLoading ? (
                    <CircularProgress size={20} />
                  ) : loginStatus?.authState === 'LoggingIn' ? (
                    <Alert severity="info" icon={<CircularProgress size={20} />}>
                      Waiting for OneDrive authorization... Please complete the process in the opened browser window.
                    </Alert>
                  ) : loginStatus?.authState === 'LoggedIn' ? (
                    <>
                      <Alert severity="success">
                        {loginStatus.userEmail ? 
                          `Connected to OneDrive as ${loginStatus.userEmail}` : 
                          'Connected to OneDrive'}
                      </Alert>
                      <ConnectButton
                        variant="outlined"
                        color="primary"
                        onClick={handleDisconnect}
                        disabled={disconnectOneDrive.isPending}
                      >
                        {disconnectOneDrive.isPending ? (
                          <>
                            <CircularProgress size={20} sx={{ mr: 1 }} />
                            Disconnecting...
                          </>
                        ) : (
                          'Disconnect from OneDrive'
                        )}
                      </ConnectButton>
                    </>
                  ) : (
                    <>
                      <Alert severity="info">
                        Not connected to OneDrive. Connect your account to enable backup synchronization.
                      </Alert>
                      <ConnectButton
                        variant="contained"
                        color="primary"
                        onClick={handleConnect}
                        disabled={oneDriveAuth.isPending}
                      >
                        {oneDriveAuth.isPending ? (
                          <>
                            <CircularProgress size={20} sx={{ mr: 1 }} />
                            Connecting...
                          </>
                        ) : (
                          'Connect OneDrive Account'
                        )}
                      </ConnectButton>
                    </>
                  )}
                </FieldInput>
              </FieldRow>
            </FormSection>
          </CardContent>
        </SettingsCard>

        <Divider />

        {/* General Settings */}
        <SettingsCard>
          <CardContent>
            <SectionHeader theme={settings.theme}>
              <SectionIcon theme={settings.theme}>
                <GeneralIcon />
              </SectionIcon>
              <div>
                <Typography variant="h6">General Settings</Typography>
                <Typography variant="body2" color="text.secondary">
                  Configure general application settings
                </Typography>
              </div>
            </SectionHeader>

            <FormSection>
              <FieldRow>
                <FieldLabel>
                  <Typography>Instance Name</Typography>
                  <FieldDescription>
                    Name of your Home Assistant instance
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <StyledTextField
                    value={settings.general.instanceName}
                    onChange={handleInputChange('general', 'instanceName')}
                    size="small"
                    fullWidth
                  />
                </FieldInput>
              </FieldRow>
              <FieldRow>
                <FieldLabel>
                  <Typography>Home Assistant API Timeout</Typography>
                  <FieldDescription>
                    Timeout for Home Assistant API calls (minutes)
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <Slider
                    value={settings.general.hassAPITimeoutMinutes}
                    onChange={handleSliderChange('general', 'hassAPITimeoutMinutes')}
                    min={1}
                    max={30}
                    step={1}
                    valueLabelDisplay="auto"
                    marks={[
                      { value: 1, label: '1m' },
                      { value: 15, label: '15m' },
                      { value: 30, label: '30m' }
                    ]}
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Log Level</Typography>
                  <FieldDescription>
                    Set the application logging level
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <Select
                    value={settings.general.logLevelStr}
                    onChange={(e) => handleInputChange('general', 'logLevelStr')(e as any)}
                    size="small"
                    fullWidth
                  >
                    <MenuItem value="error">Error</MenuItem>
                    <MenuItem value="warning">Warning</MenuItem>
                    <MenuItem value="info">Info</MenuItem>
                    <MenuItem value="verbose">Verbose</MenuItem>
                  </Select>
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Notifications</Typography>
                  <FieldDescription>
                    Control application notification settings
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.general.notifyOnError}
                        onChange={handleInputChange('general', 'notifyOnError')}
                      />
                    }
                    label="Error Notifications"
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Anonymous Reporting</Typography>
                  <FieldDescription>
                    Help improve the addon by sending anonymous usage data
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.general.enableAnonymousErrorReporting}
                        onChange={handleInputChange('general', 'enableAnonymousErrorReporting')}
                      />
                    }
                    label="Send Anonymous Error Reports"
                  />
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.general.enableAnonymousTelemetry}
                        onChange={handleInputChange('general', 'enableAnonymousTelemetry')}
                      />
                    }
                    label="Send Anonymous Telemetry"
                  />
                </FieldInput>
              </FieldRow>
            </FormSection>
          </CardContent>
        </SettingsCard>

        <Divider />

        {/* Backup Settings */}
        <SettingsCard>
          <CardContent>
            <SectionHeader theme={settings.theme}>
              <SectionIcon theme={settings.theme}>
                <BackupIcon />
              </SectionIcon>
              <div>
                <Typography variant="h6">Backup Settings</Typography>
                <Typography variant="body2" color="text.secondary">
                  Configure backup behavior and retention
                </Typography>
              </div>
            </SectionHeader>

            <FormSection>
              <FieldRow>
                <FieldLabel>
                  <Typography>Backup Name Template</Typography>
                  <FieldDescription>
                    Template for backup names. Use {'{type}'} and {'{date}'} as placeholders
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <StyledTextField
                    value={settings.backup.backupName}
                    onChange={handleInputChange('backup', 'backupName')}
                    size="small"
                    fullWidth
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Backup Password</Typography>
                  <FieldDescription>
                    Optional password to encrypt backups
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <StyledTextField
                    type={showPassword ? 'text' : 'password'}
                    value={settings.backup.backupPassword || ''}
                    onChange={handleInputChange('backup', 'backupPassword')}
                    size="small"
                    fullWidth
                    InputProps={{
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            onClick={() => setShowPassword(!showPassword)}
                            edge="end"
                          >
                            {showPassword ? <VisibilityOffIcon /> : <VisibilityIcon />}
                          </IconButton>
                        </InputAdornment>
                      )
                    }}
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Backup Interval</Typography>
                  <FieldDescription>
                    Days between automatic backups
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <Slider
                    value={settings.backup.backupIntervalDays}
                    onChange={handleSliderChange('backup', 'backupIntervalDays')}
                    min={1}
                    max={30}
                    step={1}
                    valueLabelDisplay="auto"
                    marks={[
                      { value: 1, label: '1d' },
                      { value: 7, label: '7d' },
                      { value: 30, label: '30d' }
                    ]}
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Backup Allowed Hours</Typography>
                  <FieldDescription>
                    Hours when backups are allowed (e.g., "2-4,5-7" or "*" for any time)
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <StyledTextField
                    value={settings.backup.backupAllowedHours}
                    onChange={handleInputChange('backup', 'backupAllowedHours')}
                    size="small"
                    fullWidth
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Retention Settings</Typography>
                  <FieldDescription>
                    Configure how many backups to keep
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FieldGroup>
                    <TextField
                      type="number"
                      label="Local Backups"
                      value={settings.backup.maxLocalBackups}
                      onChange={handleInputChange('backup', 'maxLocalBackups')}
                      size="small"
                    />
                    <TextField
                      type="number"
                      label="OneDrive Backups"
                      value={settings.backup.maxOnedriveBackups}
                      onChange={handleInputChange('backup', 'maxOnedriveBackups')}
                      size="small"
                    />
                  </FieldGroup>
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Generational Backup Settings</Typography>
                  <FieldDescription>
                    Configure generational backup retention periods
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FieldGroup>
                    <TextField
                      type="number"
                      label="Days"
                      value={settings.backup.generationalDays}
                      onChange={handleInputChange('backup', 'generationalDays')}
                      size="small"
                    />
                    <TextField
                      type="number"
                      label="Weeks"
                      value={settings.backup.generationalWeeks}
                      onChange={handleInputChange('backup', 'generationalWeeks')}
                      size="small"
                    />
                    <TextField
                      type="number"
                      label="Months"
                      value={settings.backup.generationalMonths}
                      onChange={handleInputChange('backup', 'generationalMonths')}
                      size="small"
                    />
                    <TextField
                      type="number"
                      label="Years"
                      value={settings.backup.generationalYears}
                      onChange={handleInputChange('backup', 'generationalYears')}
                      size="small"
                    />
                  </FieldGroup>
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Excluded Addons</Typography>
                  <FieldDescription>
                    Addons to exclude from backups
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <StyledTextField
                    value={newExcludedAddon}
                    onChange={(e) => setNewExcludedAddon(e.target.value)}
                    placeholder="Addon name"
                    size="small"
                    fullWidth
                    InputProps={{
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton onClick={handleAddExcludedAddon} edge="end">
                            <AddIcon />
                          </IconButton>
                        </InputAdornment>
                      )
                    }}
                    onKeyPress={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        handleAddExcludedAddon();
                      }
                    }}
                  />
                  
                  {settings.backup.excludedAddons.length > 0 ? (
                    <List dense>
                      {settings.backup.excludedAddons.map((addon, index) => (
                        <ListItem key={addon}>
                          <ListItemText primary={addon} />
                          <ListItemSecondaryAction>
                            <IconButton edge="end" onClick={() => handleRemoveExcludedAddon(index)}>
                              <DeleteIcon />
                            </IconButton>
                          </ListItemSecondaryAction>
                        </ListItem>
                      ))}
                    </List>
                  ) : (
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
                      No addons excluded
                    </Typography>
                  )}
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Exclusions</Typography>
                  <FieldDescription>
                    Select items to exclude from backups
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.backup.excludeMediaFolder}
                        onChange={handleInputChange('backup', 'excludeMediaFolder')}
                      />
                    }
                    label="Media Folder"
                  />
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.backup.excludeSSLFolder}
                        onChange={handleInputChange('backup', 'excludeSSLFolder')}
                      />
                    }
                    label="SSL Folder"
                  />
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.backup.excludeShareFolder}
                        onChange={handleInputChange('backup', 'excludeShareFolder')}
                      />
                    }
                    label="Share Folder"
                  />
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.backup.excludeLocalAddonsFolder}
                        onChange={handleInputChange('backup', 'excludeLocalAddonsFolder')}
                      />
                    }
                    label="Local Addons Folder"
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Backup Behavior</Typography>
                  <FieldDescription>
                    Additional backup behavior settings
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.backup.monitorAllLocalBackups}
                        onChange={handleInputChange('backup', 'monitorAllLocalBackups')}
                      />
                    }
                    label="Monitor All Local Backups"
                  />
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.backup.ignoreUpgradeBackups}
                        onChange={handleInputChange('backup', 'ignoreUpgradeBackups')}
                      />
                    }
                    label="Ignore Upgrade Backups"
                  />
                </FieldInput>
              </FieldRow>
            </FormSection>
          </CardContent>
        </SettingsCard>

        <Divider />

        {/* File Sync Settings */}
        <SettingsCard>
          <CardContent>
            <SectionHeader theme={settings.theme}>
              <SectionIcon theme={settings.theme}>
                <FolderIcon />
              </SectionIcon>
              <div>
                <Typography variant="h6">File Sync Settings</Typography>
                <Typography variant="body2" color="text.secondary">
                  Configure file synchronization options
                </Typography>
              </div>
            </SectionHeader>

            <FormSection>
              <FieldRow>
                <FieldLabel>
                  <Typography>Sync Paths</Typography>
                  <FieldDescription>
                    Paths to synchronize with OneDrive
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <StyledTextField
                    value={newSyncPath}
                    onChange={(e) => setNewSyncPath(e.target.value)}
                    placeholder="Path to sync"
                    size="small"
                    fullWidth
                    InputProps={{
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton onClick={handleAddSyncPath} edge="end">
                            <AddIcon />
                          </IconButton>
                        </InputAdornment>
                      )
                    }}
                    onKeyPress={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        handleAddSyncPath();
                      }
                    }}
                  />
                  
                  {settings.fileSync.syncPaths.length > 0 ? (
                    <List dense>
                      {settings.fileSync.syncPaths.map((path, index) => (
                        <ListItem key={path}>
                          <ListItemText primary={path} />
                          <ListItemSecondaryAction>
                            <IconButton edge="end" onClick={() => handleRemoveSyncPath(index)}>
                              <DeleteIcon />
                            </IconButton>
                          </ListItemSecondaryAction>
                        </ListItem>
                      ))}
                    </List>
                  ) : (
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
                      No paths configured
                    </Typography>
                  )}
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Sync Options</Typography>
                  <FieldDescription>
                    Configure file synchronization behavior
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.fileSync.fileSyncRemoveDeleted}
                        onChange={handleInputChange('fileSync', 'fileSyncRemoveDeleted')}
                      />
                    }
                    label="Remove files from OneDrive when deleted locally"
                  />
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={settings.fileSync.ignoreAllowedHoursForFileSync}
                        onChange={handleInputChange('fileSync', 'ignoreAllowedHoursForFileSync')}
                      />
                    }
                    label="Sync files regardless of allowed hours setting"
                  />
                </FieldInput>
              </FieldRow>
            </FormSection>
          </CardContent>
        </SettingsCard>

        <ButtonContainer>
          {saveError && <Alert severity="error" onClose={() => setSaveError(null)} sx={{ mb: 2 }}>{saveError}</Alert>}
          {saveSuccess && <Alert severity="success" sx={{ mb: 2 }}>Settings saved.</Alert>}
          <Button
            type="submit"
            variant="contained"
            color="primary"
            startIcon={<SaveIcon />}
            disabled={updateSettings.isPending}
            size="large"
          >
            Save Settings
          </Button>
        </ButtonContainer>
      </form>

      <DeviceCodeDialog 
        open={deviceCodeData !== null}
        onClose={handleCloseDeviceCodeDialog}
        verificationUrl={deviceCodeData?.verificationUrl ?? ''}
        userCode={deviceCodeData?.userCode ?? ''}
      />
    </SettingsContainer>
  );
};

export default Settings;