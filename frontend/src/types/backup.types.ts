export interface IBackup {
  id: number;
  name: string;
  date: string;
  size: string;
  status: 'In Progress' | 'Local' | 'OneDrive' | 'Synced';
  type: 'Automated' | 'Manual';
  path: string;
}

export interface IStatusInfo {
  icon: JSX.Element;
  color: string;
  tooltip: string;
}

export interface IThemeMode {
  mode: 'light' | 'dark';
}