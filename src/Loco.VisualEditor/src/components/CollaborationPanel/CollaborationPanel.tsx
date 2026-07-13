/**
 * Collaboration Panel Component
 *
 * Manages real-time collaboration connection and features:
 * - Connect/disconnect from collaboration server
 * - Active users list with presence indicators
 * - Live cursor tracking
 * - Recent activity feed
 * - User invitation functionality
 */

import { useState, useCallback, useMemo, memo } from 'react';
import {
  X,
  Users,
  UserPlus,
  Circle,
  Activity,
  Eye,
  Edit3,
  Copy,
  Check,
  AlertCircle,
  Wifi,
  WifiOff,
  Lock,
  Unlock,
  MousePointer2,
} from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';
import { COPY_FEEDBACK_DURATION } from '@/utils/constants';
import { useCollaborationStore } from '@/store/collaborationStore';
import { useWorkflowStore } from '@/store/workflowStore';
import { FormInput } from '@/components/Form';

// ============================================================================
// Constants
// ============================================================================

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// Icon mapping for activity types (memoized outside component)
const ACTIVITY_ICON_MAP = {
  edit: <Edit3 className="w-3 h-3" />,
  view: <Eye className="w-3 h-3" />,
  save: <Check className="w-3 h-3" />,
  lock: <Lock className="w-3 h-3" />,
  unlock: <Unlock className="w-3 h-3" />,
  comment: <Activity className="w-3 h-3" />,
  run: <Activity className="w-3 h-3" />,
};

// ============================================================================
// Types
// ============================================================================

interface CollaborationPanelProps {
  workflowId: string;
  isOpen: boolean;
  onClose: () => void;
}

type ActivityType = 'edit' | 'view' | 'comment' | 'save' | 'run' | 'lock' | 'unlock';

interface ActivityLog {
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

function CollaborationPanelComponent({
  workflowId,
  isOpen,
  onClose,
}: CollaborationPanelProps) {
  const toast = useToast();
  const {
    isConnected,
    isConnecting,
    connectionError,
    currentUser,
    collaborators,
    userCursors,
    isWorkflowLocked,
    lockedByUser,
    connect,
    disconnect,
    joinWorkflow,
    leaveWorkflow,
    lockWorkflow,
    unlockWorkflow,
  } = useCollaborationStore();

  const { nodes } = useWorkflowStore();

  const [serverUrl, setServerUrl] = useState(
    (import.meta.env.VITE_COLLAB_SERVER_URL as string | undefined) || 'ws://localhost:3001'
  );
  const [userName, setUserName] = useState('');
  const [userEmail, setUserEmail] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteMessage, setInviteMessage] = useState('');
  const [shareLink, setShareLink] = useState('');
  const [linkCopied, setLinkCopied] = useState(false);
  const [activities, setActivities] = useState<ActivityLog[]>([]);

  // Memoize form input handlers to preserve referential equality
  const handleServerUrlChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setServerUrl(e.target.value),
    []
  );

  const handleUserNameChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setUserName(e.target.value),
    []
  );

  const handleUserEmailChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setUserEmail(e.target.value),
    []
  );

  const handleInviteEmailChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setInviteEmail(e.target.value),
    []
  );

  // Memoize node lookup for activity logging to prevent O(n) searches per activity
  const nodeMap = useMemo(() => {
    return new Map(nodes.map((n) => [n.id, n.data.label]));
  }, [nodes]);

  // Memoize formatTime function to preserve referential equality
  const formatTime = useCallback((timestamp: string) => {
    const date = new Date(timestamp);
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);

    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return date.toLocaleDateString();
  }, []);

  // Add activity to log with memoized node lookup
  const addActivity = useCallback(
    (type: ActivityType, description: string, nodeId?: string) => {
      const activity: ActivityLog = {
        id: crypto.randomUUID(),
        userId: currentUser?.id || '',
        userName: currentUser?.name || 'Unknown',
        type,
        description,
        timestamp: new Date().toISOString(),
        nodeId,
        nodeName: nodeId ? nodeMap.get(nodeId) : undefined,
      };

      setActivities((prev) => [activity, ...prev].slice(0, 50));
    },
    [currentUser, nodeMap]
  );

  // Connect to collaboration server
  const handleConnect = useCallback(async () => {
    if (!userName) {
      toast.error('Please enter your name');
      return;
    }

    try {
      await connect(serverUrl, {
        name: userName,
        email: userEmail,
      });

      await joinWorkflow(workflowId);
      toast.success('Connected to collaboration server');

      // Generate share link
      const link = `${window.location.origin}?workflow=${workflowId}&collab=${serverUrl}`;
      setShareLink(link);

      // Add activity
      addActivity('view', 'joined the collaboration session');
    } catch (error) {
      toast.error(`Failed to connect: ${error}`);
    }
  }, [userName, serverUrl, userEmail, workflowId, connect, joinWorkflow, toast, addActivity]);

  // Disconnect from server
  const handleDisconnect = useCallback(() => {
    leaveWorkflow();
    disconnect();
    toast.info('Disconnected from collaboration');
    setShareLink('');
    setActivities([]);
  }, [leaveWorkflow, disconnect, toast]);

  // Toggle workflow lock
  const handleToggleLock = useCallback(async () => {
    if (isWorkflowLocked) {
      if (lockedByUser === currentUser?.id) {
        unlockWorkflow();
        toast.info('Workflow unlocked');
        addActivity('unlock', 'unlocked the workflow');
      } else {
        toast.error('Only the user who locked can unlock');
      }
    } else {
      const success = await lockWorkflow();
      if (success) {
        toast.success('Workflow locked for editing');
        addActivity('lock', 'locked the workflow for editing');
      } else {
        toast.error('Failed to lock workflow');
      }
    }
  }, [isWorkflowLocked, lockedByUser, currentUser?.id, unlockWorkflow, lockWorkflow, toast, addActivity]);

  // Copy share link
  const handleCopyLink = useCallback(() => {
    if (shareLink) {
      navigator.clipboard.writeText(shareLink);
      setLinkCopied(true);
      toast.success('Share link copied to clipboard');
      setTimeout(() => setLinkCopied(false), COPY_FEEDBACK_DURATION);
    }
  }, [shareLink, toast]);

  // Send invitation
  const handleSendInvite = useCallback(() => {
    if (!inviteEmail) {
      toast.error('Please enter an email address');
      return;
    }

    if (!EMAIL_REGEX.test(inviteEmail)) {
      toast.error('Please enter a valid email address');
      return;
    }

    // Prepare invitation data for backend API
    const invitationData = {
      email: inviteEmail,
      workflowId: 'current-workflow-id', // Would be set from store
      message: inviteMessage,
      timestamp: new Date().toISOString(),
    };

    // TODO: Call backend API when sendInvite endpoint is available
    // const response = await sendInvite(invitationData);
    // if (response.success) { ... }

    console.log('Preparing invitation:', invitationData);
    toast.success(`Invitation prepared for ${inviteEmail}`);
    toast.info('Email sending requires backend API integration', 5000);
    setInviteEmail('');
    setInviteMessage('');
  }, [inviteEmail, toast]);

  // Memoize user status color getter to preserve referential equality
  const getUserStatusColor = useCallback(
    (hasActiveCursor: boolean) => {
      return hasActiveCursor ? 'text-green-500' : 'text-gray-400';
    },
    []
  );

  // Get activity icon from memoized constant map
  const getActivityIcon = useCallback(
    (type: ActivityType) => ACTIVITY_ICON_MAP[type] || ACTIVITY_ICON_MAP['comment'],
    []
  );

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl w-full max-w-4xl h-[80vh] flex flex-col shadow-2xl">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Users className="w-5 h-5 text-loco-primary" />
            <h2 className="text-xl font-bold text-gray-900">Real-time Collaboration</h2>
            {isConnected && (
              <div className="flex items-center gap-2 ml-4">
                <Circle className="w-2 h-2 fill-green-500 text-green-500" />
                <span className="text-sm text-green-600">Connected</span>
              </div>
            )}
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-hidden flex">
          {/* Main Panel */}
          <div className="flex-1 p-6 overflow-y-auto">
            {!isConnected ? (
              // Connection Form
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">
                    Connect to Collaboration Server
                  </h3>

                  <div className="space-y-4">
                    <FormInput
                      id="server-url"
                      type="text"
                      label="Server URL"
                      value={serverUrl}
                      onChange={handleServerUrlChange}
                      placeholder="ws://localhost:3001"
                      helpText="WebSocket URL of the collaboration server"
                    />

                    <FormInput
                      id="user-name"
                      type="text"
                      label="Your Name"
                      value={userName}
                      onChange={handleUserNameChange}
                      placeholder="Enter your name"
                      required={true}
                      helpText="Your name for other collaborators"
                    />

                    <FormInput
                      id="user-email"
                      type="email"
                      label="Email (optional)"
                      value={userEmail}
                      onChange={handleUserEmailChange}
                      placeholder="your.email@example.com"
                      helpText="Email for invitation and collaboration notifications"
                    />

                    {connectionError && (
                      <div className="flex items-start gap-2 p-3 bg-red-50 rounded-lg">
                        <AlertCircle className="w-4 h-4 text-red-600 mt-0.5" />
                        <p className="text-sm text-red-600">{connectionError}</p>
                      </div>
                    )}

                    <button
                      onClick={handleConnect}
                      disabled={isConnecting || !userName}
                      className="flex items-center gap-2 px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50"
                    >
                      <Wifi className="w-4 h-4" />
                      {isConnecting ? 'Connecting...' : 'Connect'}
                    </button>
                  </div>
                </div>

                <div className="bg-blue-50 rounded-lg p-4">
                  <h4 className="font-medium text-blue-900 mb-2">
                    How Real-time Collaboration Works
                  </h4>
                  <ul className="space-y-1 text-sm text-blue-700">
                    <li>• See other users' cursors and selections in real-time</li>
                    <li>• Changes are instantly synchronized across all users</li>
                    <li>• Lock workflow for exclusive editing when needed</li>
                    <li>• Track activity and see who's working on what</li>
                  </ul>
                </div>
              </div>
            ) : (
              // Connected View
              <div className="space-y-6">
                {/* Share & Invite */}
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">
                    Share & Invite
                  </h3>

                  <div className="space-y-4">
                    {/* Share Link */}
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">
                        Share Link
                      </label>
                      <div className="flex gap-2">
                        <FormInput
                          id="share-link"
                          type="text"
                          value={shareLink}
                          onChange={() => {}}
                          disabled={true}
                          className="flex-1"
                          helpText="Shareable link for other collaborators"
                        />
                        <button
                          onClick={handleCopyLink}
                          className="flex items-center gap-2 px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors self-end"
                        >
                          {linkCopied ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
                          {linkCopied ? 'Copied!' : 'Copy'}
                        </button>
                      </div>
                    </div>

                    {/* Invite by Email */}
                    <FormInput
                      id="invite-email"
                      type="email"
                      label="Invite by Email"
                      value={inviteEmail}
                      onChange={handleInviteEmailChange}
                      placeholder="colleague@example.com"
                      helpText="Email address to invite to collaboration"
                      suffix={
                        <button
                          onClick={handleSendInvite}
                          className="flex items-center gap-2 px-3 py-1 bg-loco-primary text-white rounded hover:bg-blue-700 transition-colors text-sm"
                          tabIndex={-1}
                        >
                          <UserPlus className="w-4 h-4" />
                          Invite
                        </button>
                      }
                    />
                  </div>
                </div>

                {/* Workflow Lock */}
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">
                    Workflow Lock
                  </h3>
                  <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
                    <div className="flex items-center gap-3">
                      {isWorkflowLocked ? (
                        <>
                          <Lock className="w-5 h-5 text-yellow-600" />
                          <div>
                            <p className="text-sm font-medium text-gray-900">Workflow Locked</p>
                            <p className="text-xs text-gray-600">
                              {lockedByUser === currentUser?.id
                                ? 'You have exclusive editing rights'
                                : `Locked by ${collaborators.find((u) => u.id === lockedByUser)?.name || 'another user'}`}
                            </p>
                          </div>
                        </>
                      ) : (
                        <>
                          <Unlock className="w-5 h-5 text-green-600" />
                          <div>
                            <p className="text-sm font-medium text-gray-900">Workflow Unlocked</p>
                            <p className="text-xs text-gray-600">All users can edit</p>
                          </div>
                        </>
                      )}
                    </div>
                    <button
                      onClick={handleToggleLock}
                      className={`px-3 py-1.5 text-sm rounded-lg transition-colors ${
                        isWorkflowLocked
                          ? lockedByUser === currentUser?.id
                            ? 'bg-yellow-100 text-yellow-700 hover:bg-yellow-200'
                            : 'bg-gray-200 text-gray-400 cursor-not-allowed'
                          : 'bg-green-100 text-green-700 hover:bg-green-200'
                      }`}
                      disabled={isWorkflowLocked && lockedByUser !== currentUser?.id}
                    >
                      {isWorkflowLocked ? 'Unlock' : 'Lock'}
                    </button>
                  </div>
                </div>

                {/* Disconnect */}
                <button
                  onClick={handleDisconnect}
                  className="flex items-center gap-2 px-4 py-2 border border-red-300 text-red-600 rounded-lg hover:bg-red-50 transition-colors"
                >
                  <WifiOff className="w-4 h-4" />
                  Disconnect
                </button>
              </div>
            )}
          </div>

          {/* Side Panel - Active Users & Activity */}
          {isConnected && (
            <div className="w-80 border-l border-gray-200 flex flex-col">
              {/* Active Users */}
              <div className="p-4 border-b border-gray-200">
                <h3 className="text-sm font-semibold text-gray-700 mb-3">
                  Active Users ({collaborators.length + 1})
                </h3>
                <div className="space-y-2">
                  {/* Current User */}
                  <div className="flex items-center gap-3 p-2 bg-blue-50 rounded-lg">
                    <div
                      className="w-8 h-8 rounded-full flex items-center justify-center text-white font-medium text-sm"
                      style={{ backgroundColor: currentUser?.color || '#3B82F6' }}
                    >
                      {currentUser?.name?.charAt(0).toUpperCase() || 'U'}
                    </div>
                    <div className="flex-1">
                      <p className="text-sm font-medium text-gray-900">
                        {currentUser?.name || 'You'} (You)
                      </p>
                      <p className="text-xs text-gray-500">Active</p>
                    </div>
                    <Circle className="w-2 h-2 fill-green-500 text-green-500" />
                  </div>

                  {/* Other Users */}
                  {collaborators.map((user) => {
                    const hasActiveCursor = user.id in userCursors;
                    return (
                      <div key={user.id} className="flex items-center gap-3 p-2 rounded-lg hover:bg-gray-50">
                        <div
                          className="w-8 h-8 rounded-full flex items-center justify-center text-white font-medium text-sm"
                          style={{ backgroundColor: user.color }}
                        >
                          {user.name.charAt(0).toUpperCase()}
                        </div>
                        <div className="flex-1">
                          <p className="text-sm font-medium text-gray-900">{user.name}</p>
                          <p className="text-xs text-gray-500">
                            {hasActiveCursor ? 'Active' : 'Idle'}
                          </p>
                        </div>
                        <div className="flex items-center gap-1">
                          {hasActiveCursor && <MousePointer2 className="w-3 h-3 text-gray-400" />}
                          <Circle className={`w-2 h-2 fill-current ${getUserStatusColor(hasActiveCursor)}`} />
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* Activity Feed */}
              <div className="flex-1 p-4 overflow-y-auto">
                <h3 className="text-sm font-semibold text-gray-700 mb-3">Recent Activity</h3>
                <div className="space-y-2">
                  {activities.length === 0 ? (
                    <p className="text-sm text-gray-500 text-center py-4">No activity yet</p>
                  ) : (
                    activities.map((activity) => (
                      <div key={activity.id} className="flex items-start gap-2 text-xs">
                        <div className="mt-0.5 text-gray-400">{getActivityIcon(activity.type)}</div>
                        <div className="flex-1">
                          <p className="text-gray-700">
                            <span className="font-medium">{activity.userName}</span>{' '}
                            {activity.description}
                            {activity.nodeName && (
                              <span className="text-loco-primary"> "{activity.nodeName}"</span>
                            )}
                          </p>
                          <p className="text-gray-400">{formatTime(activity.timestamp)}</p>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export const CollaborationPanel = memo(CollaborationPanelComponent);
CollaborationPanel.displayName = 'CollaborationPanel';