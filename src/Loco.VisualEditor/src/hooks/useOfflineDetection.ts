/**
 * Offline Detection Hook
 *
 * Detects when the user goes offline/online and provides
 * connection status information.
 */

import { useEffect, useState } from 'react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Hook
// ============================================================================

export function useOfflineDetection() {
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [wasOffline, setWasOffline] = useState(false);
  const toast = useToast();

  useEffect(() => {
    const handleOnline = () => {
      setIsOnline(true);

      // Show "back online" message only if user was previously offline
      if (wasOffline) {
        toast.success('You are back online!');
        setWasOffline(false);
      }
    };

    const handleOffline = () => {
      setIsOnline(false);
      setWasOffline(true);
      toast.warning('You are offline. Changes will be saved locally.', 8000);
    };

    // Add event listeners
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    // Cleanup
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [wasOffline, toast]);

  return { isOnline };
}
