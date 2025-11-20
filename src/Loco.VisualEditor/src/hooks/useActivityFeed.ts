import { useState, useCallback } from 'react';

export type ActivityType = 'view' | 'join' | 'lock' | 'unlock' | 'comment' | 'edit' | 'file' | 'execute';

export interface ActivityLog {
  id: string;
  userId: string;
  userName: string;
  type: ActivityType;
  description: string;
  timestamp: string;
  nodeId?: string;
  nodeName?: string;
}

interface UseActivityFeedOptions {
  maxActivities?: number;
  onActivityAdded?: (activity: ActivityLog) => void;
}

interface UseActivityFeedReturn {
  activities: ActivityLog[];
  addActivity: (type: ActivityType, description: string, nodeId?: string, nodeName?: string) => void;
  filterActivities: (userId?: string, type?: ActivityType) => ActivityLog[];
  clearActivities: () => void;
  getActivityColor: (type: ActivityType) => string;
}

const ACTIVITY_COLOR_MAP: Record<ActivityType, string> = {
  view: 'text-blue-600',
  join: 'text-green-600',
  lock: 'text-red-600',
  unlock: 'text-yellow-600',
  comment: 'text-purple-600',
  edit: 'text-orange-600',
  file: 'text-indigo-600',
  execute: 'text-teal-600',
};

export function useActivityFeed(options: UseActivityFeedOptions = {}): UseActivityFeedReturn {
  const { maxActivities = 50, onActivityAdded } = options;
  const [activities, setActivities] = useState<ActivityLog[]>([]);

  const addActivity = useCallback(
    (type: ActivityType, description: string, nodeId?: string, nodeName?: string) => {
      const activity: ActivityLog = {
        id: crypto.randomUUID(),
        userId: 'current-user',
        userName: 'You',
        type,
        description,
        timestamp: new Date().toISOString(),
        nodeId,
        nodeName,
      };
      setActivities((prev) => [activity, ...prev].slice(0, maxActivities));
      onActivityAdded?.(activity);
    },
    [maxActivities, onActivityAdded]
  );

  const filterActivities = useCallback(
    (userId?: string, type?: ActivityType): ActivityLog[] => {
      return activities.filter((activity) => {
        if (userId && activity.userId !== userId) return false;
        if (type && activity.type !== type) return false;
        return true;
      });
    },
    [activities]
  );

  const clearActivities = useCallback(() => {
    setActivities([]);
  }, []);

  const getActivityColor = useCallback((type: ActivityType): string => {
    return ACTIVITY_COLOR_MAP[type] || ACTIVITY_COLOR_MAP.comment;
  }, []);

  return {
    activities,
    addActivity,
    filterActivities,
    clearActivities,
    getActivityColor,
  };
}
