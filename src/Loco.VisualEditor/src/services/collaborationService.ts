/**
 * Real-time Collaboration Service
 *
 * Manages WebSocket connections and real-time synchronization
 * for collaborative workflow editing.
 */

import { io, Socket } from 'socket.io-client';
import { Node, Edge } from 'reactflow';

// ============================================================================
// Types
// ============================================================================

export interface CollaborationUser {
  id: string;
  name: string;
  email?: string;
  avatar?: string;
  color: string;
  cursor?: {
    x: number;
    y: number;
  };
  selection?: string[]; // Selected node IDs
  isActive: boolean;
  lastActiveAt: string;
}

/**
 * Discriminated union for collaboration events.
 * Each event type has a specific data structure for type-safe handling.
 */
export type CollaborationEvent =
  | { type: 'user:joined'; userId: string; data: { user: CollaborationUser }; timestamp: string }
  | { type: 'user:left'; userId: string; data: { user: CollaborationUser | null }; timestamp: string }
  | { type: 'user:cursor-moved'; userId: string; data: { x: number; y: number }; timestamp: string }
  | { type: 'user:selection-changed'; userId: string; data: { nodeIds: string[] }; timestamp: string }
  | { type: 'nodes:changed'; userId: string; data: { changes: any[] }; timestamp: string }
  | { type: 'edges:changed'; userId: string; data: { changes: any[] }; timestamp: string }
  | { type: 'node:added'; userId: string; data: { node: Node }; timestamp: string }
  | { type: 'node:deleted'; userId: string; data: { nodeId: string }; timestamp: string }
  | { type: 'node:updated'; userId: string; data: { nodeId: string; data: any }; timestamp: string }
  | { type: 'edge:added'; userId: string; data: { edge: Edge }; timestamp: string }
  | { type: 'edge:deleted'; userId: string; data: { edgeId: string }; timestamp: string }
  | { type: 'workflow:locked'; userId: string; data: { userId: string }; timestamp: string }
  | { type: 'workflow:unlocked'; userId: string; data: { userId: string }; timestamp: string };

export interface CollaborationRoom {
  id: string;
  workflowId: string;
  users: CollaborationUser[];
  isLocked: boolean;
  lockedBy?: string;
}

type EventHandler = (event: CollaborationEvent) => void;

// ============================================================================
// Collaboration Service Class
// ============================================================================

export class CollaborationService {
  private socket: Socket | null = null;
  private room: CollaborationRoom | null = null;
  private currentUser: CollaborationUser | null = null;
  private eventHandlers: Map<string, Set<EventHandler>> = new Map();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private reconnectDelay = 1000;

  // User colors for collaboration
  private readonly USER_COLORS = [
    '#3B82F6', // Blue
    '#10B981', // Green
    '#F59E0B', // Amber
    '#EF4444', // Red
    '#8B5CF6', // Purple
    '#EC4899', // Pink
    '#06B6D4', // Cyan
    '#F97316', // Orange
  ];

  /**
   * Connect to collaboration server
   */
  connect(serverUrl: string, user: Partial<CollaborationUser>): Promise<void> {
    return new Promise((resolve, reject) => {
      try {
        // Create socket connection
        this.socket = io(serverUrl, {
          transports: ['websocket'],
          reconnection: true,
          reconnectionAttempts: this.maxReconnectAttempts,
          reconnectionDelay: this.reconnectDelay,
        });

        // Set current user
        this.currentUser = {
          id: user.id || crypto.randomUUID(),
          name: user.name || 'Anonymous',
          email: user.email,
          avatar: user.avatar,
          color: this.getRandomColor(),
          isActive: true,
          lastActiveAt: new Date().toISOString(),
        };

        // Socket event handlers
        this.socket.on('connect', () => {
          console.log('Connected to collaboration server');
          this.reconnectAttempts = 0;
          resolve();
        });

        this.socket.on('disconnect', (reason) => {
          console.log('Disconnected from collaboration server:', reason);
          this.handleDisconnect();
        });

        this.socket.on('error', (error) => {
          console.error('Socket error:', error);
          reject(error);
        });

        // Collaboration events
        this.socket.on('event', (event: CollaborationEvent) => {
          this.handleEvent(event);
        });

        this.socket.on('room:joined', (room: CollaborationRoom) => {
          this.room = room;
          this.emitEvent('user:joined', { user: this.currentUser });
        });

        this.socket.on('room:users', (users: CollaborationUser[]) => {
          if (this.room) {
            this.room.users = users;
          }
        });

      } catch (error) {
        reject(error);
      }
    });
  }

  /**
   * Join a collaboration room for a workflow
   */
  joinRoom(workflowId: string): Promise<void> {
    return new Promise((resolve, reject) => {
      if (!this.socket) {
        reject(new Error('Not connected to server'));
        return;
      }

      this.socket.emit('room:join', {
        workflowId,
        user: this.currentUser,
      }, (response: any) => {
        if (response.success) {
          this.room = response.room;
          resolve();
        } else {
          reject(new Error(response.error || 'Failed to join room'));
        }
      });
    });
  }

  /**
   * Leave the current collaboration room
   */
  leaveRoom(): void {
    if (!this.socket || !this.room) return;

    this.socket.emit('room:leave', {
      roomId: this.room.id,
    });

    this.emitEvent('user:left', { user: this.currentUser });
    this.room = null;
  }

  /**
   * Disconnect from collaboration server
   */
  disconnect(): void {
    if (this.socket) {
      this.leaveRoom();
      this.socket.disconnect();
      this.socket = null;
    }
    this.currentUser = null;
    this.eventHandlers.clear();
  }

  /**
   * Send cursor position update
   */
  updateCursor(x: number, y: number): void {
    if (!this.socket || !this.currentUser) return;

    this.currentUser.cursor = { x, y };
    this.emitEvent('user:cursor-moved', { x, y });
  }

  /**
   * Send selection update
   */
  updateSelection(nodeIds: string[]): void {
    if (!this.socket || !this.currentUser) return;

    this.currentUser.selection = nodeIds;
    this.emitEvent('user:selection-changed', { nodeIds });
  }

  /**
   * Send node changes
   */
  sendNodeChanges(changes: any[]): void {
    this.emitEvent('nodes:changed', { changes });
  }

  /**
   * Send edge changes
   */
  sendEdgeChanges(changes: any[]): void {
    this.emitEvent('edges:changed', { changes });
  }

  /**
   * Send node addition
   */
  sendNodeAdded(node: Node): void {
    this.emitEvent('node:added', { node });
  }

  /**
   * Send node deletion
   */
  sendNodeDeleted(nodeId: string): void {
    this.emitEvent('node:deleted', { nodeId });
  }

  /**
   * Send node update
   */
  sendNodeUpdated(nodeId: string, data: any): void {
    this.emitEvent('node:updated', { nodeId, data });
  }

  /**
   * Send edge addition
   */
  sendEdgeAdded(edge: Edge): void {
    this.emitEvent('edge:added', { edge });
  }

  /**
   * Send edge deletion
   */
  sendEdgeDeleted(edgeId: string): void {
    this.emitEvent('edge:deleted', { edgeId });
  }

  /**
   * Lock workflow for exclusive editing
   */
  lockWorkflow(): Promise<boolean> {
    return new Promise((resolve) => {
      if (!this.socket || !this.room) {
        resolve(false);
        return;
      }

      this.socket.emit('workflow:lock', {
        roomId: this.room.id,
      }, (response: any) => {
        if (response.success) {
          this.emitEvent('workflow:locked', { userId: this.currentUser?.id });
        }
        resolve(response.success);
      });
    });
  }

  /**
   * Unlock workflow
   */
  unlockWorkflow(): void {
    if (!this.socket || !this.room) return;

    this.socket.emit('workflow:unlock', {
      roomId: this.room.id,
    });

    this.emitEvent('workflow:unlocked', { userId: this.currentUser?.id });
  }

  /**
   * Subscribe to collaboration events
   */
  on(eventType: string, handler: EventHandler): void {
    if (!this.eventHandlers.has(eventType)) {
      this.eventHandlers.set(eventType, new Set());
    }
    this.eventHandlers.get(eventType)!.add(handler);
  }

  /**
   * Unsubscribe from collaboration events
   */
  off(eventType: string, handler: EventHandler): void {
    const handlers = this.eventHandlers.get(eventType);
    if (handlers) {
      handlers.delete(handler);
    }
  }

  /**
   * Get current room info
   */
  getRoom(): CollaborationRoom | null {
    return this.room;
  }

  /**
   * Get current user info
   */
  getCurrentUser(): CollaborationUser | null {
    return this.currentUser;
  }

  /**
   * Get all connected users
   */
  getUsers(): CollaborationUser[] {
    return this.room?.users || [];
  }

  /**
   * Check if workflow is locked
   */
  isLocked(): boolean {
    return this.room?.isLocked || false;
  }

  /**
   * Check if current user has lock
   */
  hasLock(): boolean {
    return this.room?.lockedBy === this.currentUser?.id;
  }

  // ============================================================================
  // Private Methods
  // ============================================================================

  private emitEvent(type: CollaborationEvent['type'], data: any): void {
    if (!this.socket) return;

    const event: CollaborationEvent = {
      type,
      userId: this.currentUser?.id || '',
      data,
      timestamp: new Date().toISOString(),
    };

    this.socket.emit('event', event);
  }

  private handleEvent(event: CollaborationEvent): void {
    // Don't process our own events
    if (event.userId === this.currentUser?.id) return;

    // Call registered handlers
    const handlers = this.eventHandlers.get(event.type);
    if (handlers) {
      handlers.forEach(handler => handler(event));
    }

    // Call wildcard handlers
    const wildcardHandlers = this.eventHandlers.get('*');
    if (wildcardHandlers) {
      wildcardHandlers.forEach(handler => handler(event));
    }
  }

  private handleDisconnect(): void {
    // Attempt to reconnect
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      this.reconnectAttempts++;
      console.log(`Attempting to reconnect (${this.reconnectAttempts}/${this.maxReconnectAttempts})...`);

      setTimeout(() => {
        if (this.socket) {
          this.socket.connect();
        }
      }, this.reconnectDelay * this.reconnectAttempts);
    }
  }

  private getRandomColor(): string {
    return this.USER_COLORS[Math.floor(Math.random() * this.USER_COLORS.length)];
  }
}

// Singleton instance
export const collaborationService = new CollaborationService();