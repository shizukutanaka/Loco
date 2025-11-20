import { memo } from 'react';
import { ActivityLog, ActivityType } from '@/hooks/useActivityFeed';
import { Eye, Users, Lock, Unlock, MessageSquare, Edit2, FileText, RefreshCw } from 'lucide-react';
import { formatRelativeTime } from '@/utils/timeFormatting';

interface ActivityFeedItemProps {
  activity: ActivityLog;
}

const ACTIVITY_ICON_MAP: Record<ActivityType, React.ReactNode> = {
  view: <Eye className="w-4 h-4" />,
  join: <Users className="w-4 h-4" />,
  lock: <Lock className="w-4 h-4" />,
  unlock: <Unlock className="w-4 h-4" />,
  comment: <MessageSquare className="w-4 h-4" />,
  edit: <Edit2 className="w-4 h-4" />,
  file: <FileText className="w-4 h-4" />,
  execute: <RefreshCw className="w-4 h-4" />,
};

const ACTIVITY_COLOR_MAP: Record<ActivityType, string> = {
  view: 'text-blue-600 bg-blue-50',
  join: 'text-green-600 bg-green-50',
  lock: 'text-red-600 bg-red-50',
  unlock: 'text-yellow-600 bg-yellow-50',
  comment: 'text-purple-600 bg-purple-50',
  edit: 'text-orange-600 bg-orange-50',
  file: 'text-indigo-600 bg-indigo-50',
  execute: 'text-teal-600 bg-teal-50',
};

function ActivityFeedItemComponent({ activity }: ActivityFeedItemProps) {
  const colorClass = ACTIVITY_COLOR_MAP[activity.type];
  const icon = ACTIVITY_ICON_MAP[activity.type];

  return (
    <div className="px-4 py-3 border-b border-gray-200 hover:bg-gray-50 transition-colors flex items-start gap-3 h-full">
      <div className={`mt-1 p-2 rounded ${colorClass}`}>
        {icon}
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-baseline gap-2">
          <span className="font-medium text-sm text-gray-900 truncate">
            {activity.userName}
          </span>
          <span className="text-xs text-gray-500 whitespace-nowrap">
            {formatRelativeTime(activity.timestamp)}
          </span>
        </div>
        <p className="text-sm text-gray-600 mt-1">{activity.description}</p>
        {activity.nodeName && (
          <p className="text-xs text-gray-500 mt-1">
            On node: <span className="font-mono">{activity.nodeName}</span>
          </p>
        )}
      </div>
    </div>
  );
}

export const ActivityFeedItem = memo(ActivityFeedItemComponent);
ActivityFeedItem.displayName = 'ActivityFeedItem';
