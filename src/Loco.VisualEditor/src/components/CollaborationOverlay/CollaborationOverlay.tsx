/**
 * Collaboration Overlay Component
 *
 * Displays real-time collaboration features:
 * - User cursors with names
 * - Selection highlights
 * - User presence indicators
 * - Lock status
 */

import { useEffect, useRef, memo, useCallback, useMemo } from 'react';
import { useReactFlow } from 'reactflow';
import { useCollaborationStore } from '@/store/collaborationStore';
import { MousePointer2, Lock, Users } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface CursorProps {
  userName: string;
  userColor: string;
  position: { x: number; y: number };
}

// ============================================================================
// Cursor Component
// ============================================================================

const UserCursor = memo(({ userName, userColor, position }: CursorProps) => {
  return (
    <div
      className="absolute pointer-events-none z-50 transition-all duration-100"
      style={{
        left: position.x,
        top: position.y,
        transform: 'translate(-50%, -50%)',
      }}
    >
      <div className="relative">
        <MousePointer2
          className="w-5 h-5"
          style={{ color: userColor, fill: userColor }}
        />
        <div
          className="absolute top-5 left-2 px-2 py-1 rounded text-xs font-medium text-white whitespace-nowrap shadow-lg"
          style={{ backgroundColor: userColor }}
        >
          {userName}
        </div>
      </div>
    </div>
  );
});

UserCursor.displayName = 'UserCursor';

// ============================================================================
// Collaboration Overlay Component
// ============================================================================

function CollaborationOverlayComponent() {
  const canvasRef = useRef<HTMLDivElement>(null);
  const lastUpdateTimeRef = useRef(0);
  const highlightedNodesRef = useRef<Map<string, { nodeId: string; color: string }>>(new Map());
  const reactFlowInstance = useReactFlow();
  const {
    isConnected,
    currentUser,
    collaborators,
    userCursors,
    userSelections,
    isWorkflowLocked,
    lockedByUser,
    updateCursor,
  } = useCollaborationStore();

  // Create collaborator map for O(1) lookups instead of O(n)
  const collaboratorMap = useMemo(
    () => new Map(collaborators.map((u) => [u.id, u])),
    [collaborators]
  );

  // Memoize mouse move handler to preserve referential equality
  const handleMouseMove = useCallback(
    (e: MouseEvent) => {
      const now = Date.now();
      const updateInterval = 50; // Throttle updates to 20fps

      if (now - lastUpdateTimeRef.current < updateInterval) return;

      const rect = canvasRef.current?.getBoundingClientRect();
      if (!rect) return;

      const position = reactFlowInstance.project({
        x: e.clientX - rect.left,
        y: e.clientY - rect.top,
      });

      updateCursor(position.x, position.y);
      lastUpdateTimeRef.current = now;
    },
    [updateCursor, reactFlowInstance]
  );

  // Track mouse movement and send cursor updates
  useEffect(() => {
    if (!isConnected || !canvasRef.current) return;

    const canvas = canvasRef.current;
    canvas.addEventListener('mousemove', handleMouseMove);

    return () => {
      canvas.removeEventListener('mousemove', handleMouseMove);
    };
  }, [isConnected, handleMouseMove]);

  // Highlight selected nodes for each user - optimized with tracking to minimize DOM queries
  useEffect(() => {
    const newHighlighted = new Map<string, { nodeId: string; color: string }>();

    // Track which nodes are now highlighted and their colors
    Object.entries(userSelections).forEach(([userId, nodeIds]) => {
      const user = collaboratorMap.get(userId);
      if (!user) return;

      nodeIds.forEach((nodeId: string) => {
        newHighlighted.set(nodeId, { nodeId, color: user.color });
      });
    });

    // Get previous highlighting
    const previousHighlighted = highlightedNodesRef.current;

    // Apply highlights only to newly highlighted nodes
    newHighlighted.forEach((highlight, nodeId) => {
      if (!previousHighlighted.has(nodeId)) {
        const nodeElement = document.querySelector(`[data-id="${nodeId}"]`);
        if (nodeElement) {
          (nodeElement as HTMLElement).style.outline = `2px solid ${highlight.color}`;
          (nodeElement as HTMLElement).style.outlineOffset = '2px';
        }
      }
    });

    // Remove highlights from nodes that are no longer highlighted
    previousHighlighted.forEach(({ nodeId }, key) => {
      if (!newHighlighted.has(key)) {
        const nodeElement = document.querySelector(`[data-id="${nodeId}"]`);
        if (nodeElement) {
          (nodeElement as HTMLElement).style.outline = '';
          (nodeElement as HTMLElement).style.outlineOffset = '';
        }
      }
    });

    // Update the ref with current highlighting
    highlightedNodesRef.current = newHighlighted;

    // Cleanup: remove all highlights when component unmounts
    return () => {
      highlightedNodesRef.current.forEach(({ nodeId }) => {
        const nodeElement = document.querySelector(`[data-id="${nodeId}"]`);
        if (nodeElement) {
          (nodeElement as HTMLElement).style.outline = '';
          (nodeElement as HTMLElement).style.outlineOffset = '';
        }
      });
      highlightedNodesRef.current.clear();
    };
  }, [userSelections, collaboratorMap]);

  if (!isConnected) return null;

  return (
    <>
      {/* Canvas overlay for cursor tracking */}
      <div
        ref={canvasRef}
        className="absolute inset-0 pointer-events-none z-40"
      >
        {/* Render user cursors - using Map for O(1) lookups */}
        {Object.entries(userCursors).map(([userId, position]) => {
          const user = collaboratorMap.get(userId);
          if (!user) return null;

          return (
            <UserCursor
              key={userId}
              userName={user.name}
              userColor={user.color}
              position={position}
            />
          );
        })}
      </div>

      {/* Presence indicator */}
      <div className="absolute top-4 right-4 z-50">
        <div className="bg-white rounded-lg shadow-lg border border-gray-200 p-3">
          <div className="flex items-center gap-2 mb-2">
            <Users className="w-4 h-4 text-gray-600" />
            <span className="text-sm font-medium text-gray-700">
              Active Users ({collaborators.length + 1})
            </span>
          </div>

          {/* Current user */}
          <div className="flex items-center gap-2 mb-1">
            <div
              className="w-2 h-2 rounded-full"
              style={{ backgroundColor: currentUser?.color || '#3B82F6' }}
            />
            <span className="text-xs text-gray-600">
              {currentUser?.name || 'You'} (You)
            </span>
          </div>

          {/* Other users */}
          {collaborators.map((user) => (
            <div key={user.id} className="flex items-center gap-2 mb-1">
              <div
                className="w-2 h-2 rounded-full"
                style={{ backgroundColor: user.color }}
              />
              <span className="text-xs text-gray-600">{user.name}</span>
              {user.cursor && (
                <MousePointer2 className="w-3 h-3 text-gray-400" />
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Lock status indicator */}
      {isWorkflowLocked && (
        <div className="absolute bottom-4 right-4 z-50">
          <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3 flex items-center gap-2">
            <Lock className="w-4 h-4 text-yellow-600" />
            <span className="text-sm text-yellow-700">
              {lockedByUser === currentUser?.id
                ? 'You have locked this workflow'
                : `Locked by ${collaboratorMap.get(lockedByUser!)?.name || 'another user'}`}
            </span>
          </div>
        </div>
      )}
    </>
  );
}

export const CollaborationOverlay = memo(CollaborationOverlayComponent);
CollaborationOverlay.displayName = 'CollaborationOverlay';

// ============================================================================
// Collaboration Status Bar Component
// ============================================================================

function CollaborationStatusBarComponent() {
  const { isConnected, isConnecting, connectionError, collaborators } =
    useCollaborationStore();

  return (
    <div className="h-8 bg-gray-50 border-t border-gray-200 flex items-center justify-between px-4">
      <div className="flex items-center gap-3">
        {/* Connection status */}
        <div className="flex items-center gap-2">
          <div
            className={`w-2 h-2 rounded-full ${
              isConnected
                ? 'bg-green-500'
                : isConnecting
                ? 'bg-yellow-500 animate-pulse'
                : 'bg-red-500'
            }`}
          />
          <span className="text-xs text-gray-600">
            {isConnected
              ? 'Connected'
              : isConnecting
              ? 'Connecting...'
              : connectionError || 'Disconnected'}
          </span>
        </div>

        {/* User count */}
        {isConnected && (
          <div className="flex items-center gap-1">
            <Users className="w-3 h-3 text-gray-500" />
            <span className="text-xs text-gray-600">
              {collaborators.length + 1} active
            </span>
          </div>
        )}
      </div>

      {/* Collaboration mode indicator */}
      {isConnected && (
        <div className="text-xs text-gray-500">
          Real-time collaboration enabled
        </div>
      )}
    </div>
  );
}

export const CollaborationStatusBar = memo(CollaborationStatusBarComponent);
CollaborationStatusBar.displayName = 'CollaborationStatusBar';