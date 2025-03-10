import { CloudSync as CloudSyncIcon, Storage as StorageIcon, Cloud as CloudIcon, SyncAlt as SyncAltIcon } from '@mui/icons-material';
import { IStatusInfo } from '../../types/backup.types';

export const getStatusInfo = (status: string): IStatusInfo => {
  switch (status) {
    case 'In Progress':
      return {
        icon: CloudSyncIcon,
        color: 'warning.main',
        tooltip: 'Currently syncing to OneDrive'
      };
    case 'Local':
      return {
        icon: StorageIcon,
        color: 'info.main',
        tooltip: 'Exists locally only'
      };
    case 'OneDrive':
      return {
        icon: CloudIcon,
        color: '#0078d4',
        tooltip: 'Exists on OneDrive only'
      };
    case 'Synced':
      return {
        icon: SyncAltIcon,
        color: 'success.main',
        tooltip: 'Synced to both local and OneDrive'
      };
    default:
      return {
        icon: CloudSyncIcon,
        color: 'text.secondary',
        tooltip: 'Unknown status'
      };
  }
};