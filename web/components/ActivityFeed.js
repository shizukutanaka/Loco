import React, { memo, useEffect, useState } from 'react';
import { 
  CheckCircle, 
  XCircle, 
  AlertCircle, 
  Clock,
  Zap,
  FileText,
  Bell
} from 'lucide-react';

// Activity item component
const ActivityItem = memo(({ activity }) => {
  const getIcon = () => {
    switch (activity.type) {
      case 'success':
        return <CheckCircle className="h-4 w-4 text-green-400" />;
      case 'error':
        return <XCircle className="h-4 w-4 text-red-400" />;
      case 'warning':
        return <AlertCircle className="h-4 w-4 text-yellow-400" />;
      case 'info':
        return <Bell className="h-4 w-4 text-blue-400" />;
      default:
        return <Zap className="h-4 w-4 text-gray-400" />;
    }
  };

  const getTimeAgo = (timestamp) => {
    const now = Date.now();
    const diff = now - timestamp;
    const seconds = Math.floor(diff / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (days > 0) return `${days}d ago`;
    if (hours > 0) return `${hours}h ago`;
    if (minutes > 0) return `${minutes}m ago`;
    return 'Just now';
  };

  return (
    <div className="flex items-start space-x-3 py-2 border-b border-gray-700 last:border-0">
      <div className="mt-1">{getIcon()}</div>
      <div className="flex-1 min-w-0">
        <p className="text-sm text-white truncate">{activity.message}</p>
        <p className="text-xs text-gray-400 mt-1">
          {activity.flow} • {getTimeAgo(activity.timestamp)}
        </p>
      </div>
    </div>
  );
});

ActivityItem.displayName = 'ActivityItem';

// Main activity feed component
const ActivityFeed = memo(() => {
  const [activities, setActivities] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Simulate fetching activities
    const fetchActivities = async () => {
      setLoading(true);
      
      // Simulate API delay
      await new Promise(resolve => setTimeout(resolve, 500));
      
      // Mock data
      const mockActivities = [
        {
          id: 1,
          type: 'success',
          message: 'Morning routine completed',
          flow: 'Morning Automation',
          timestamp: Date.now() - 1000 * 60 * 5 // 5 minutes ago
        },
        {
          id: 2,
          type: 'info',
          message: 'Backup started',
          flow: 'Daily Backup',
          timestamp: Date.now() - 1000 * 60 * 15 // 15 minutes ago
        },
        {
          id: 3,
          type: 'success',
          message: 'Files synchronized',
          flow: 'Cloud Sync',
          timestamp: Date.now() - 1000 * 60 * 30 // 30 minutes ago
        },
        {
          id: 4,
          type: 'warning',
          message: 'API rate limit approaching',
          flow: 'Weather Monitor',
          timestamp: Date.now() - 1000 * 60 * 60 // 1 hour ago
        },
        {
          id: 5,
          type: 'error',
          message: 'Connection timeout',
          flow: 'RSS Reader',
          timestamp: Date.now() - 1000 * 60 * 120 // 2 hours ago
        },
        {
          id: 6,
          type: 'success',
          message: 'Report generated',
          flow: 'Weekly Report',
          timestamp: Date.now() - 1000 * 60 * 180 // 3 hours ago
        },
        {
          id: 7,
          type: 'info',
          message: 'System check completed',
          flow: 'Health Monitor',
          timestamp: Date.now() - 1000 * 60 * 240 // 4 hours ago
        },
        {
          id: 8,
          type: 'success',
          message: 'Email sent successfully',
          flow: 'Email Notifier',
          timestamp: Date.now() - 1000 * 60 * 300 // 5 hours ago
        }
      ];
      
      setActivities(mockActivities);
      setLoading(false);
    };

    fetchActivities();
    
    // Set up polling for real-time updates
    const interval = setInterval(fetchActivities, 30000); // Refresh every 30 seconds
    
    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <div className="space-y-3">
        {[...Array(5)].map((_, i) => (
          <div key={i} className="animate-pulse">
            <div className="flex items-start space-x-3">
              <div className="h-4 w-4 bg-gray-700 rounded-full"></div>
              <div className="flex-1">
                <div className="h-4 bg-gray-700 rounded w-3/4 mb-2"></div>
                <div className="h-3 bg-gray-700 rounded w-1/2"></div>
              </div>
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (activities.length === 0) {
    return (
      <div className="text-center py-8">
        <Clock className="h-8 w-8 text-gray-600 mx-auto mb-2" />
        <p className="text-gray-400 text-sm">No recent activity</p>
      </div>
    );
  }

  return (
    <div className="space-y-1 max-h-96 overflow-y-auto custom-scrollbar">
      {activities.map(activity => (
        <ActivityItem key={activity.id} activity={activity} />
      ))}
    </div>
  );
});

ActivityFeed.displayName = 'ActivityFeed';

export default ActivityFeed;
