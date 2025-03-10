import { CloudSync as CloudSyncIcon, Storage as StorageIcon, Cloud as CloudIcon, SyncAlt as SyncAltIcon } from '@mui/icons-material';
import { IStatusInfo } from '../../types/backup.types';

export const getStatusInfo = (status: string): IStatusInfo => {
  switch (status) {
    case 'In Progress':
      return {
        icon: <CloudSyncIcon/>,
        color: '#ff9800', // More vibrant warning color
        tooltip: 'Currently syncing to OneDrive'
      };
    case 'Local':
      return {
        icon: <StorageIcon/>,
        color: 'text.primary', // This will be white in dark mode and black in light mode
        tooltip: 'Exists locally only'
      };
    case 'OneDrive':
      return {
        icon: <CloudIcon/>,
        color: '#0078d4', // OneDrive blue
        tooltip: 'Exists on OneDrive only'
      };
    case 'Synced':
      return {
        icon: <SyncAltIcon/>,
        color: '#4caf50', // Brighter success green
        tooltip: 'Synced to both local and OneDrive'
      };
    default:
      return {
        icon: <CloudSyncIcon/>,
        color: '#9e9e9e', // Neutral gray
        tooltip: 'Unknown status'
      };
  }
};