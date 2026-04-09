export interface ICreateBackupModalProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (name: string) => void;
}
