/**
 * Collaboration Store
 *
 * Manages real-time collaboration state using Zustand.
 * Integrates with collaboration service for WebSocket communication.
 */

import { create } from 'zustand';
import { Node, Edge, NodeChange, EdgeChange } from 'reactflow';
import {
  collaborationService,
  CollaborationUser,
  CollaborationEvent,
  CollaborationRoom,
} from '@/services/collaborationService';

// ============================================================================
// Types
// ============================================================================

export interface CollaborationState {
  // Connection state
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: string | null;

  // Room state
  room: CollaborationRoom | null;
  currentUser: CollaborationUser | null;
  collaborators: CollaborationUser[];

  // Collaboration features
  userCursors: Record<string, { x: number; y: number }>;
  userSelections: Record<string, string[]>;
  isWorkflowLocked: boolean;
  lockedByUser: string | null;

  // Actions
  connect: (serverUrl: string, user: Partial<CollaborationUser>) => Promise<void>;
  disconnect: () => void;
  joinWorkflow: (workflowId: string) => Promise<void>;
  leaveWorkflow: () => void;

  // Cursor and selection
  updateCursor: (x: number, y: number) => void;
  updateSelection: (nodeIds: string[]) => void;

  // Workflow changes
  sendNodeChanges: (changes: NodeChange[]) => void;
  sendEdgeChanges: (changes: EdgeChange[]) => void;
  sendNodeAdded: (node: Node) => void;
  sendNodeDeleted: (nodeId: string) => void;
  sendNodeUpdated: (nodeId: string, data: Partial<Node['data']>) => void;
  sendEdgeAdded: (edge: Edge) => void;
  sendEdgeDeleted: (edgeId: string) => void;

  // Locking
  lockWorkflow: () => Promise<boolean>;
  unlockWorkflow: () => void;

  // Internal handlers
  handleCollaborationEvent: (event: CollaborationEvent) => void;
}

// ============================================================================
// Collaboration Store
// ============================================================================

export const useCollaborationStore = create<CollaborationState>((set, get) => ({
  // Initial state
  isConnected: false,
  isConnecting: false,
  connectionError: null,
  room: null,
  currentUser: null,
  collaborators: [],
  userCursors: {},
  userSelections: {},
  isWorkflowLocked: false,
  lockedByUser: null,

  // Connect to collaboration server
  connect: async (serverUrl: string, user: Partial<CollaborationUser>) => {
    const { isConnected, isConnecting } = get();
    if (isConnected || isConnecting) return;

    set({ isConnecting: true, connectionError: null });

    try {
      await collaborationService.connect(serverUrl, user);

      // Set up event handlers
      collaborationService.on('*', get().handleCollaborationEvent);

      set({
        isConnected: true,
        isConnecting: false,
        currentUser: collaborationService.getCurrentUser(),
      });
    } catch (error) {
      set({
        isConnected: false,
        isConnecting: false,
        connectionError: error instanceof Error ? error.message : 'Connection failed',
      });
      throw error;
    }
  },

  // Disconnect from server
  disconnect: () => {
    collaborationService.disconnect();
    set({
      isConnected: false,
      isConnecting: false,
      room: null,
      currentUser: null,
      collaborators: [],
      userCursors: {},
      userSelections: {},
      isWorkflowLocked: false,
      lockedByUser: null,
    });
  },

  // Join a workflow collaboration room
  joinWorkflow: async (workflowId: string) => {
    const { isConnected } = get();
    if (!isConnected) {
      throw new Error('Not connected to collaboration server');
    }

    await collaborationService.joinRoom(workflowId);
    set({
      room: collaborationService.getRoom(),
      collaborators: collaborationService.getUsers(),
    });
  },

  // Leave current workflow
  leaveWorkflow: () => {
    collaborationService.leaveRoom();
    set({
      room: null,
      collaborators: [],
      userCursors: {},
      userSelections: {},
    });
  },

  // Update cursor position
  updateCursor: (x: number, y: number) => {
    collaborationService.updateCursor(x, y);
  },

  // Update node selection
  updateSelection: (nodeIds: string[]) => {
    collaborationService.updateSelection(nodeIds);
  },

  // Send node changes
  sendNodeChanges: (changes: NodeChange[]) => {
    collaborationService.sendNodeChanges(changes);
  },

  // Send edge changes
  sendEdgeChanges: (changes: EdgeChange[]) => {
    collaborationService.sendEdgeChanges(changes);
  },

  // Send node added
  sendNodeAdded: (node: Node) => {
    collaborationService.sendNodeAdded(node);
  },

  // Send node deleted
  sendNodeDeleted: (nodeId: string) => {
    collaborationService.sendNodeDeleted(nodeId);
  },

  // Send node updated
  sendNodeUpdated: (nodeId: string, data: Partial<Node['data']>) => {
    collaborationService.sendNodeUpdated(nodeId, data);
  },

  // Send edge added
  sendEdgeAdded: (edge: Edge) => {
    collaborationService.sendEdgeAdded(edge);
  },

  // Send edge deleted
  sendEdgeDeleted: (edgeId: string) => {
    collaborationService.sendEdgeDeleted(edgeId);
  },

  // Lock workflow for exclusive editing
  lockWorkflow: async () => {
    const success = await collaborationService.lockWorkflow();
    if (success) {
      set({
        isWorkflowLocked: true,
        lockedByUser: get().currentUser?.id || null,
      });
    }
    return success;
  },

  // Unlock workflow
  unlockWorkflow: () => {
    collaborationService.unlockWorkflow();
    set({
      isWorkflowLocked: false,
      lockedByUser: null,
    });
  },

  // Handle incoming collaboration events
  handleCollaborationEvent: (event: CollaborationEvent) => {
    const state = get();

    switch (event.type) {
      case 'user:joined': {
        // Add new user to collaborators
        const newUser = event.data.user as CollaborationUser;
        if (newUser && newUser.id !== state.currentUser?.id) {
          set({
            collaborators: [...state.collaborators, newUser],
          });
        }
        break;
      }

      case 'user:left': {
        // Remove user from collaborators
        const leftUser = event.data.user as CollaborationUser;
        if (leftUser) {
          const { userCursors, userSelections } = state;
          const newCursors = { ...userCursors };
          const newSelections = { ...userSelections };
          delete newCursors[leftUser.id];
          delete newSelections[leftUser.id];

          set({
            collaborators: state.collaborators.filter((u) => u.id !== leftUser.id),
            userCursors: newCursors,
            userSelections: newSelections,
          });
        }
        break;
      }

      case 'user:cursor-moved':
        // Update user cursor position
        set({
          userCursors: {
            ...state.userCursors,
            [event.userId]: {
              x: event.data.x,
              y: event.data.y,
            },
          },
        });
        break;

      case 'user:selection-changed':
        // Update user selection
        set({
          userSelections: {
            ...state.userSelections,
            [event.userId]: event.data.nodeIds,
          },
        });
        break;

      case 'workflow:locked':
        // Workflow locked by another user
        set({
          isWorkflowLocked: true,
          lockedByUser: event.data.userId,
        });
        break;

      case 'workflow:unlocked':
        // Workflow unlocked
        set({
          isWorkflowLocked: false,
          lockedByUser: null,
        });
        break;

      // Note: Node and edge changes are handled by the workflow store
      // These events are dispatched there for proper state management
      default:
        break;
    }
  },
}));