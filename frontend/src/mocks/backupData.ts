import { Backup } from '../types/backup.types';

export const mockBackups: Backup[] = [
  {
    slug: 'backup_2024_01_01',
    name: 'Automated Backup 2024-01-01',
    date: '2024-01-01 12:00:00',
    size: '1.2 GB',
    status: 'Local',
    source_type: 'Automated',
    backup_type: 'Full',
  },
  {
    slug: 'backup_2024_01_02',
    name: 'Manual Backup 2024-01-02',
    date: '2024-01-02 15:30:00',
    size: '800 MB',
    status: 'OneDrive',
    source_type: 'Manual',
    backup_type: 'Partial',
  },
  {
    slug: 'backup_2024_01_03',
    name: 'Automated Backup 2024-01-03',
    date: '2024-01-03 00:00:00',
    size: '1.5 GB',
    status: 'Synced',
    source_type: 'Automated',
    backup_type: 'Full',
  },
  {
    slug: 'backup_2024_01_04',
    name: 'In Progress Backup',
    date: '2024-01-04 10:15:00',
    size: '900 MB',
    status: 'In Progress',
    source_type: 'Manual',
    backup_type: 'Partial',
  },
  {
    slug: 'backup_2024_01_05',
    name: 'External Service Backup',
    date: '2024-01-05 08:30:00',
    size: '2.1 GB',
    status: 'Local',
    source_type: 'External',
    backup_type: 'Full',
  }
];