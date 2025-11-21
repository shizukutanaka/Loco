import { useState, useCallback, useRef } from 'react';

export interface CollaborationUser {
  id: string;
  name: string;
  email: string;
  hasActiveCursor?: boolean;
  cursorPosition?: { x: number; y: number };
  lastActive?: string;
}

interface UseCollaborationSessionOptions {
  workflowId: string;
  onStatusChange?: (status: 'connected' | 'disconnected' | 'connecting') => void;
  onError?: (error: Error) => void;
}

interface UseCollaborationSessionReturn {
  isConnected: boolean;
  isConnecting: boolean;
  currentUser: CollaborationUser | null;
  connectedUsers: CollaborationUser[];
  isWorkflowLocked: boolean;
  lockedByUser: string | null;
  shareLink: string;
  connect: (serverUrl: string, user: Omit<CollaborationUser, 'id'>) => Promise<void>;
  disconnect: () => void;
  lockWorkflow: () => Promise<boolean>;
  unlockWorkflow: () => void;
  inviteUser: (email: string) => Promise<boolean>;
  updatePresence: (position?: { x: number; y: number }) => void;
  generateShareLink: (baseUrl: string) => string;
}

export function useCollaborationSession(
  options: UseCollaborationSessionOptions
): UseCollaborationSessionReturn {
  const { workflowId, onStatusChange, onError } = options;
  const [isConnected, setIsConnected] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);
  const [currentUser, setCurrentUser] = useState<CollaborationUser | null>(null);
  const [connectedUsers, setConnectedUsers] = useState<CollaborationUser[]>([]);
  const [isWorkflowLocked, setIsWorkflowLocked] = useState(false);
  const [lockedByUser, setLockedByUser] = useState<string | null>(null);
  const [shareLink, setShareLink] = useState('');
  const serverUrlRef = useRef<string>('');

  const connect = useCallback(
    async (serverUrl: string, user: Omit<CollaborationUser, 'id'>) => {
      setIsConnecting(true);
      onStatusChange?.('connecting');
      try {
        serverUrlRef.current = serverUrl;
        await new Promise((resolve) => setTimeout(resolve, 500));

        const newUser: CollaborationUser = {
          id: `user-${Date.now()}`,
          ...user,
          lastActive: new Date().toISOString(),
        };

        setCurrentUser(newUser);
        setIsConnected(true);
        onStatusChange?.('connected');

        const link = `${serverUrl}?workflow=${workflowId}&user=${newUser.id}`;
        setShareLink(link);
      } catch (error) {
        const err = error instanceof Error ? error : new Error(String(error));
        onError?.(err);
        setIsConnected(false);
        onStatusChange?.('disconnected');
        throw err;
      } finally {
        setIsConnecting(false);
      }
    },
    [workflowId, onStatusChange, onError]
  );

  const disconnect = useCallback(() => {
    setIsConnected(false);
    setCurrentUser(null);
    setConnectedUsers([]);
    setIsWorkflowLocked(false);
    setLockedByUser(null);
    setShareLink('');
    onStatusChange?.('disconnected');
  }, [onStatusChange]);

  const lockWorkflow = useCallback(async (): Promise<boolean> => {
    if (!currentUser) return false;
    try {
      await new Promise((resolve) => setTimeout(resolve, 300));
      setIsWorkflowLocked(true);
      setLockedByUser(currentUser.id);
      return true;
    } catch (error) {
      const err = error instanceof Error ? error : new Error(String(error));
      onError?.(err);
      return false;
    }
  }, [currentUser, onError]);

  const unlockWorkflow = useCallback(() => {
    if (isWorkflowLocked && lockedByUser === currentUser?.id) {
      setIsWorkflowLocked(false);
      setLockedByUser(null);
    }
  }, [isWorkflowLocked, lockedByUser, currentUser?.id]);

  const inviteUser = useCallback(
    async (email: string): Promise<boolean> => {
      if (!isConnected || !shareLink) return false;
      try {
        // Validate email format (basic check)
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email)) return false;
        
        await new Promise((resolve) => setTimeout(resolve, 500));
        return true;
      } catch (error) {
        const err = error instanceof Error ? error : new Error(String(error));
        onError?.(err);
        return false;
      }
    },
    [isConnected, shareLink, onError]
  );

  const updatePresence = useCallback(
    (position?: { x: number; y: number }) => {
      setCurrentUser((prev) =>
        prev
          ? {
              ...prev,
              cursorPosition: position,
              lastActive: new Date().toISOString(),
            }
          : null
      );
    },
    []
  );

  const generateShareLink = useCallback((baseUrl: string): string => {
    if (!currentUser) return '';
    return `${baseUrl}?workflow=${workflowId}&user=${currentUser.id}`;
  }, [workflowId, currentUser]);

  // Return object directly - consumers should use granular selectors for memoization benefits
  // This hook intentionally returns a new object on every render to avoid stale closures
  // while granular subscriptions via selectors prevent unnecessary component re-renders
  return {
    isConnected,
    isConnecting,
    currentUser,
    connectedUsers,
    isWorkflowLocked,
    lockedByUser,
    shareLink,
    connect,
    disconnect,
    lockWorkflow,
    unlockWorkflow,
    inviteUser,
    updatePresence,
    generateShareLink,
  };
}
