/**
 * Collaboration Panel Component
 *
 * Provides real-time collaboration features:
 * - Active users list with presence indicators
 * - Recent activity feed
 * - User invitation functionality
 * - Cursor tracking and presence awareness
 */

import { useState, useEffect } from 'react';
import { X, Users, UserPlus, Circle, Activity, Eye, Edit3, Copy, Check, AlertCircle } from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface CollaborationPanelProps {
  workflowId: string;
  workflowName: string;
  isOpen: boolean;
  onClose: () => void;
}

type UserStatus = 'active' | 'idle' | 'away';
type ActivityType = 'edit' | 'view' | 'comment' | 'save' | 'run';

interface CollaborationUser {
  id: string;
  name: string;
  email: string;
  avatarColor: string;
  status: UserStatus;
  currentNode?: string;
  lastActive: string;
  cursor?: {
    x: number;
    y: number;
  };
}

interface Activity {
  id: string;
  userId: string;
  userName: string;
  type: ActivityType;
  description: string;
  timestamp: string;
  nodeId?: string;
  nodeName?: string;
}

// ============================================================================
// Collaboration Panel Component
// ============================================================================

export function CollaborationPanel({
  workflowId,
  workflowName,
  isOpen,
  onClose,
}: CollaborationPanelProps) {
  const [activeUsers, setActiveUsers] = useState<CollaborationUser[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [inviteEmail, setInviteEmail] = useState('');
  const [isInviting, setIsInviting] = useState(false);
  const [showInviteForm, setShowInviteForm] = useState(false);
  const [copied, setCopied] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const toast = useToast();

  // Fetch active users and activities
  useEffect(() => {
    if (!isOpen) return;

    const fetchCollaborationData = async () => {
      setIsLoading(true);
      try {
        // TODO: Replace with actual WebSocket connection
        // const ws = new WebSocket(`wss://api.loco.dev/collaborate/${workflowId}`);
        // ws.onmessage = (event) => handleRealtimeUpdate(event.data);

        // Mock data for demonstration
        await new Promise((resolve) => setTimeout(resolve, 500));

        setActiveUsers([
          {
            id: 'user-1',
            name: 'You',
            email: 'you@example.com',
            avatarColor: '#3b82f6',
            status: 'active',
            currentNode: 'HTTP Request',
            lastActive: new Date().toISOString(),
          },
          {
            id: 'user-2',
            name: 'Alice Johnson',
            email: 'alice@example.com',
            avatarColor: '#10b981',
            status: 'active',
            currentNode: 'Transform Data',
            lastActive: new Date(Date.now() - 120000).toISOString(),
          },
          {
            id: 'user-3',
            name: 'Bob Smith',
            email: 'bob@example.com',
            avatarColor: '#f59e0b',
            status: 'idle',
            lastActive: new Date(Date.now() - 300000).toISOString(),
          },
        ]);

        setActivities([
          {
            id: 'act-1',
            userId: 'user-2',
            userName: 'Alice Johnson',
            type: 'edit',
            description: 'Modified HTTP Request node configuration',
            timestamp: new Date(Date.now() - 180000).toISOString(),
            nodeId: 'node-1',
            nodeName: 'HTTP Request',
          },
          {
            id: 'act-2',
            userId: 'user-1',
            userName: 'You',
            type: 'save',
            description: 'Saved workflow changes',
            timestamp: new Date(Date.now() - 240000).toISOString(),
          },
          {
            id: 'act-3',
            userId: 'user-3',
            userName: 'Bob Smith',
            type: 'run',
            description: 'Executed workflow',
            timestamp: new Date(Date.now() - 360000).toISOString(),
          },
          {
            id: 'act-4',
            userId: 'user-2',
            userName: 'Alice Johnson',
            type: 'edit',
            description: 'Added Transform Data node',
            timestamp: new Date(Date.now() - 480000).toISOString(),
            nodeId: 'node-2',
            nodeName: 'Transform Data',
          },
          {
            id: 'act-5',
            userId: 'user-1',
            userName: 'You',
            type: 'view',
            description: 'Opened workflow',
            timestamp: new Date(Date.now() - 600000).toISOString(),
          },
        ]);
      } catch (error) {
        console.error('Failed to fetch collaboration data:', error);
        toast.error('Failed to load collaboration data');
      } finally {
        setIsLoading(false);
      }
    };

    fetchCollaborationData();
  }, [isOpen, workflowId, toast]);

  const handleInviteUser = async () => {
    if (!inviteEmail.trim()) {
      toast.error('Please enter an email address');
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(inviteEmail)) {
      toast.error('Please enter a valid email address');
      return;
    }

    setIsInviting(true);

    try {
      // TODO: Call API to invite user
      // const response = await inviteUserToWorkflow(workflowId, inviteEmail);
      console.log('Inviting user:', inviteEmail, 'to workflow:', workflowId);

      await new Promise((resolve) => setTimeout(resolve, 1000));

      toast.success(`Invitation sent to ${inviteEmail}`);
      setInviteEmail('');
      setShowInviteForm(false);
    } catch (error) {
      console.error('Failed to invite user:', error);
      toast.error('Failed to send invitation');
    } finally {
      setIsInviting(false);
    }
  };

  const handleCopyShareLink = () => {
    const shareLink = `https://loco.dev/workflows/${workflowId}?invite=true`;
    navigator.clipboard.writeText(shareLink);
    setCopied(true);
    toast.success('Share link copied to clipboard');

    setTimeout(() => setCopied(false), 2000);
  };

  const getStatusColor = (status: UserStatus) => {
    switch (status) {
      case 'active':
        return 'bg-green-500';
      case 'idle':
        return 'bg-yellow-500';
      case 'away':
        return 'bg-gray-400';
    }
  };

  const getStatusText = (status: UserStatus) => {
    switch (status) {
      case 'active':
        return 'Active';
      case 'idle':
        return 'Idle';
      case 'away':
        return 'Away';
    }
  };

  const getActivityIcon = (type: ActivityType) => {
    switch (type) {
      case 'edit':
        return <Edit3 className="w-3 h-3" />;
      case 'view':
        return <Eye className="w-3 h-3" />;
      case 'save':
        return <Check className="w-3 h-3" />;
      case 'run':
        return <Activity className="w-3 h-3" />;
      case 'comment':
        return <AlertCircle className="w-3 h-3" />;
    }
  };

  const getActivityColor = (type: ActivityType) => {
    switch (type) {
      case 'edit':
        return 'text-blue-700 bg-blue-50';
      case 'view':
        return 'text-gray-700 bg-gray-50';
      case 'save':
        return 'text-green-700 bg-green-50';
      case 'run':
        return 'text-purple-700 bg-purple-50';
      case 'comment':
        return 'text-orange-700 bg-orange-50';
    }
  };

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    return date.toLocaleDateString();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-3xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Collaboration</h2>
            <p className="text-sm text-gray-500 mt-1">{workflowName}</p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            title="Close"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-loco-primary"></div>
            </div>
          ) : (
            <div className="space-y-6">
              {/* Active Users Section */}
              <div>
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-lg font-semibold text-gray-900 flex items-center gap-2">
                    <Users className="w-5 h-5" />
                    Active Users ({activeUsers.length})
                  </h3>
                  <button
                    onClick={() => setShowInviteForm(!showInviteForm)}
                    className="flex items-center gap-1 px-3 py-1.5 text-sm bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
                  >
                    <UserPlus className="w-4 h-4" />
                    Invite
                  </button>
                </div>

                {/* Invite Form */}
                {showInviteForm && (
                  <div className="mb-4 p-4 bg-blue-50 border border-blue-200 rounded-lg">
                    <h4 className="text-sm font-semibold text-gray-900 mb-3">Invite User</h4>
                    <div className="space-y-3">
                      <div>
                        <label className="block text-xs font-medium text-gray-700 mb-1">
                          Email Address
                        </label>
                        <input
                          type="email"
                          value={inviteEmail}
                          onChange={(e) => setInviteEmail(e.target.value)}
                          placeholder="colleague@example.com"
                          className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent text-sm"
                          onKeyDown={(e) => e.key === 'Enter' && handleInviteUser()}
                        />
                      </div>
                      <div className="flex items-center gap-2">
                        <button
                          onClick={handleInviteUser}
                          disabled={isInviting}
                          className="px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 text-sm"
                        >
                          {isInviting ? 'Sending...' : 'Send Invitation'}
                        </button>
                        <button
                          onClick={handleCopyShareLink}
                          className="flex items-center gap-1 px-4 py-2 bg-gray-100 text-gray-700 rounded-lg hover:bg-gray-200 transition-colors text-sm"
                        >
                          {copied ? (
                            <>
                              <Check className="w-4 h-4" />
                              Copied
                            </>
                          ) : (
                            <>
                              <Copy className="w-4 h-4" />
                              Copy Link
                            </>
                          )}
                        </button>
                      </div>
                    </div>
                  </div>
                )}

                {/* Users List */}
                <div className="space-y-2">
                  {activeUsers.map((user) => (
                    <div
                      key={user.id}
                      className="flex items-center gap-3 p-3 bg-white border border-gray-200 rounded-lg hover:shadow-sm transition-shadow"
                    >
                      {/* Avatar */}
                      <div
                        className="w-10 h-10 rounded-full flex items-center justify-center text-white font-semibold text-sm relative"
                        style={{ backgroundColor: user.avatarColor }}
                      >
                        {user.name.charAt(0).toUpperCase()}
                        <div
                          className={`absolute bottom-0 right-0 w-3 h-3 rounded-full border-2 border-white ${getStatusColor(
                            user.status
                          )}`}
                        ></div>
                      </div>

                      {/* User Info */}
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <p className="text-sm font-semibold text-gray-900 truncate">
                            {user.name}
                          </p>
                          <span
                            className={`px-2 py-0.5 text-xs font-medium rounded ${
                              user.status === 'active'
                                ? 'bg-green-100 text-green-700'
                                : user.status === 'idle'
                                ? 'bg-yellow-100 text-yellow-700'
                                : 'bg-gray-100 text-gray-700'
                            }`}
                          >
                            {getStatusText(user.status)}
                          </span>
                        </div>
                        <p className="text-xs text-gray-500 truncate">{user.email}</p>
                        {user.currentNode && (
                          <p className="text-xs text-blue-600 mt-1">
                            Editing: {user.currentNode}
                          </p>
                        )}
                      </div>

                      {/* Last Active */}
                      <div className="text-xs text-gray-500">
                        {formatTimestamp(user.lastActive)}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Activity Feed Section */}
              <div>
                <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                  <Activity className="w-5 h-5" />
                  Recent Activity
                </h3>

                <div className="space-y-2">
                  {activities.map((activity) => (
                    <div
                      key={activity.id}
                      className="flex items-start gap-3 p-3 bg-white border border-gray-200 rounded-lg hover:shadow-sm transition-shadow"
                    >
                      {/* Activity Icon */}
                      <div
                        className={`w-8 h-8 rounded-full flex items-center justify-center ${getActivityColor(
                          activity.type
                        )}`}
                      >
                        {getActivityIcon(activity.type)}
                      </div>

                      {/* Activity Info */}
                      <div className="flex-1 min-w-0">
                        <p className="text-sm text-gray-900">
                          <span className="font-semibold">{activity.userName}</span>{' '}
                          {activity.description}
                        </p>
                        {activity.nodeName && (
                          <p className="text-xs text-gray-600 mt-1">Node: {activity.nodeName}</p>
                        )}
                        <p className="text-xs text-gray-500 mt-1">
                          {formatTimestamp(activity.timestamp)}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Info Box */}
              <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                <div className="flex items-start gap-2">
                  <Circle className="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5 fill-current" />
                  <div className="text-sm text-blue-700">
                    <p className="font-semibold mb-1">Real-time Collaboration</p>
                    <p>
                      See who's working on this workflow in real-time. Changes from other users will
                      appear automatically, and you can see their cursor positions and current
                      activity.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50">
          <div className="flex items-center justify-between text-sm">
            <div className="text-gray-600">
              {activeUsers.length} user{activeUsers.length !== 1 ? 's' : ''} active
            </div>
            <button
              onClick={onClose}
              className="px-4 py-2 bg-white border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 transition-colors"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
