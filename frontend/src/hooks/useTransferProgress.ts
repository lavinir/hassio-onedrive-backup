import { useState, useEffect, useCallback } from 'react';
import { getTransferProgress } from '../api/backups';

export const useTransferProgress = (operationId: string | null) => {
  const [progress, setProgress] = useState<number>(0);
  const [isComplete, setIsComplete] = useState(false);
  const [isFailed, setIsFailed] = useState(false);

  const checkProgress = useCallback(async () => {
    if (!operationId) return;

    try {
      const currentProgress = await getTransferProgress(operationId);
      
      // Check for failure condition (-1 response)
      if (currentProgress < 0) {
        setIsFailed(true);
        setIsComplete(true); // Stop polling on failure
        return;
      }
      
      setProgress(currentProgress);
      
      if (currentProgress >= 100) {
        setIsComplete(true);
      }
    } catch (error) {
      console.error('Error checking progress:', error);
      setIsFailed(true);
      setIsComplete(true); // Stop polling on error
    }
  }, [operationId]);

  useEffect(() => {
    if (!operationId) {
      setProgress(0);
      setIsComplete(false);
      setIsFailed(false);
      return;
    }

    const pollInterval = setInterval(checkProgress, 1000);
    
    return () => {
      clearInterval(pollInterval);
    };
  }, [operationId, checkProgress]);

  return { progress, isComplete, isFailed };
};