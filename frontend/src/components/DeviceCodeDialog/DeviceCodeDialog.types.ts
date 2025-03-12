export interface IDeviceCodeDialogProps {
    open: boolean;
    onClose: () => void;
    verificationUrl: string;
    userCode: string;
}