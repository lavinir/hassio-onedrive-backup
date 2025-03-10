import { useState, useEffect, useCallback } from 'react';
import { getTransferProgress } from '../api/backups';

export const useTransferProgress = (operationId: string | null) => {
  const [progress, setProgress] = useState<number>(0);
  const [isComplete, setIsComplete] = useState(false);

  const checkProgress = useCallback(async () => {
    if (!operationId) return;

    try {
      const currentProgress = await getTransferProgress(operationId);
      setProgress(currentProgress);
      
      if (currentProgress >= 100) {
        setIsComplete(true);
      }
    } catch (error) {
      console.error('Error checking progress:', error);
      setIsComplete(true); // Stop polling on error
    }
  }, [operationId]);

  useEffect(() => {
    if (!operationId) {
      setProgress(0);
      setIsComplete(false);
      return;
    }

    const pollInterval = setInterval(checkProgress, 1000);
    
    return () => {
      clearInterval(pollInterval);
    };
  }, [operationId, checkProgress]);

  return { progress, isComplete };
};