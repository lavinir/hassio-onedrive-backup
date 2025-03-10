import { FC, useState, useEffect } from 'react';
import {
  Typography,
  CardContent,
  FormControlLabel,
  Button,
  Slider,
  Select,
  MenuItem,
  TextField
} from '@mui/material';
import {
  Backup as BackupIcon,
  FolderOpen as FolderIcon,
  Settings as GeneralIcon,
  Save as SaveIcon,
} from '@mui/icons-material';

import { ISettingsProps } from './Settings.types';
import { ISettingsForm } from '../../types/settings.types';
import { useSettings } from '../../queries/useSettings';
import { useUpdateSettings } from '../../mutations/useSettingsMutations';
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
  StyledTextField,
  StyledSwitch,
  Divider,
  ButtonContainer,
} from './Settings.style';

const Settings: FC<ISettingsProps> = () => {
  const { data: initialSettings, isLoading } = useSettings();
  const updateSettings = useUpdateSettings();
  
  const [formData, setFormData] = useState<ISettingsForm>({
    general: {
      hassAPITimeoutMinutes: 5,
      logLevelStr: 'info',
      notifyOnError: true,
      enableAnonymousErrorReporting: false,
      enableAnonymousTelemetry: false
    },
    backup: {
      instanceName: 'Home Assistant',
      backupName: '{type}-backup-{date}',
      backupIntervalDays: 3,
      backupAllowedHours: '*',
      maxLocalBackups: 10,
      maxOnedriveBackups: 20,
      generationalDays: 7,
      generationalWeeks: 4,
      generationalMonths: 6,
      generationalYears: 1,
      excludedAddons: [],
      excludeMediaFolder: false,
      excludeSSLFolder: false,
      excludeShareFolder: false,
      excludeLocalAddonsFolder: false,
      monitorAllLocalBackups: true,
      ignoreUpgradeBackups: false
    },
    fileSync: {
      syncPaths: [],
      fileSyncRemoveDeleted: true,
      ignoreAllowedHoursForFileSync: false
    }
  });

  // Load initial data
  useEffect(() => {
    if (initialSettings) {
      setFormData(initialSettings);
    }
  }, [initialSettings]);

  const handleInputChange = (section: keyof ISettingsForm, field: string) => (
    e: React.ChangeEvent<HTMLInputElement>
  ) => {
    const { value, type, checked } = e.target;
    setFormData(prev => ({
      ...prev,
      [section]: {
        ...prev[section],
        [field]: type === 'checkbox' ? checked : value
      }
    }));
  };

  const handleSliderChange = (section: keyof ISettingsForm, field: string) => (
    _: Event | React.SyntheticEvent,
    newValue: number | number[]
  ) => {
    setFormData(prev => ({
      ...prev,
      [section]: {
        ...prev[section],
        [field]: newValue
      }
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await updateSettings.mutateAsync(formData);
    } catch (error) {
      console.error('Failed to save settings:', error);
    }
  };

  if (isLoading) {
    return <Typography>Loading settings...</Typography>;
  }

  return (
    <SettingsContainer>
      <Typography variant="h4" component="h1" gutterBottom>
        Settings
      </Typography>
      
      <form onSubmit={handleSubmit}>
        {/* General Settings */}
        <SettingsCard>
          <CardContent>
            <SectionHeader>
              <SectionIcon>
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
                  <Typography>Home Assistant API Timeout</Typography>
                  <FieldDescription>
                    Timeout for Home Assistant API calls (minutes)
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <Slider
                    value={formData.general.hassAPITimeoutMinutes}
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
                    value={formData.general.logLevelStr}
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
                  <Typography>Error Notifications</Typography>
                  <FieldDescription>
                    Enable notifications on errors
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={formData.general.notifyOnError}
                        onChange={handleInputChange('general', 'notifyOnError')}
                      />
                    }
                    label=""
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Anonymous Error Reporting</Typography>
                  <FieldDescription>
                    Help improve the addon by sending anonymous error reports
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={formData.general.enableAnonymousErrorReporting}
                        onChange={handleInputChange('general', 'enableAnonymousErrorReporting')}
                      />
                    }
                    label=""
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
            <SectionHeader>
              <SectionIcon>
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
                  <Typography>Instance Name</Typography>
                  <FieldDescription>
                    Name of your Home Assistant instance
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <StyledTextField
                    value={formData.backup.instanceName}
                    onChange={handleInputChange('backup', 'instanceName')}
                    size="small"
                    fullWidth
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Backup Name Template</Typography>
                  <FieldDescription>
                    Template for backup names. Use {'{type}'} and {'{date}'} as placeholders
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <StyledTextField
                    value={formData.backup.backupName}
                    onChange={handleInputChange('backup', 'backupName')}
                    size="small"
                    fullWidth
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
                    value={formData.backup.backupIntervalDays}
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
                  <Typography>Retention Settings</Typography>
                  <FieldDescription>
                    Configure how many backups to keep
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <TextField
                    type="number"
                    label="Local Backups"
                    value={formData.backup.maxLocalBackups}
                    onChange={handleInputChange('backup', 'maxLocalBackups')}
                    size="small"
                    sx={{ mr: 2 }}
                  />
                  <TextField
                    type="number"
                    label="OneDrive Backups"
                    value={formData.backup.maxOnedriveBackups}
                    onChange={handleInputChange('backup', 'maxOnedriveBackups')}
                    size="small"
                  />
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
                        checked={formData.backup.excludeMediaFolder}
                        onChange={handleInputChange('backup', 'excludeMediaFolder')}
                      />
                    }
                    label="Media Folder"
                  />
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={formData.backup.excludeSSLFolder}
                        onChange={handleInputChange('backup', 'excludeSSLFolder')}
                      />
                    }
                    label="SSL Folder"
                  />
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={formData.backup.excludeShareFolder}
                        onChange={handleInputChange('backup', 'excludeShareFolder')}
                      />
                    }
                    label="Share Folder"
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
            <SectionHeader>
              <SectionIcon>
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
                  <Typography>Remove Deleted Files</Typography>
                  <FieldDescription>
                    Remove files from OneDrive when deleted locally
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={formData.fileSync.fileSyncRemoveDeleted}
                        onChange={handleInputChange('fileSync', 'fileSyncRemoveDeleted')}
                      />
                    }
                    label=""
                  />
                </FieldInput>
              </FieldRow>

              <FieldRow>
                <FieldLabel>
                  <Typography>Ignore Time Restrictions</Typography>
                  <FieldDescription>
                    Sync files regardless of allowed hours setting
                  </FieldDescription>
                </FieldLabel>
                <FieldInput>
                  <FormControlLabel
                    control={
                      <StyledSwitch
                        checked={formData.fileSync.ignoreAllowedHoursForFileSync}
                        onChange={handleInputChange('fileSync', 'ignoreAllowedHoursForFileSync')}
                      />
                    }
                    label=""
                  />
                </FieldInput>
              </FieldRow>
            </FormSection>
          </CardContent>
        </SettingsCard>

        <ButtonContainer>
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
    </SettingsContainer>
  );
};

export default Settings;