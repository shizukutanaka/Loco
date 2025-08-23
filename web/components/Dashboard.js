import React, { memo, useCallback, useMemo, lazy, Suspense } from 'react';
import dynamic from 'next/dynamic';
import { 
  Activity, 
  Zap, 
  Clock, 
  CheckCircle,
  TrendingUp,
  BarChart3,
  Calendar,
  Settings
} from 'lucide-react';

// Lazy load heavy components
const Chart = dynamic(() => import('react-chartjs-2').then(mod => mod.Line), {
  ssr: false,
  loading: () => <div className="h-64 bg-gray-800 animate-pulse rounded-lg" />
});

const ActivityFeed = lazy(() => import('./ActivityFeed'));
const ThemeSwitcher = dynamic(() => import('./ThemeSwitcher'), { ssr: false });
const Tooltip = dynamic(() => import('./Tooltip'), { ssr: false });

// Memoized stat card component
const StatCard = memo(({ icon: Icon, label, value, trend, color = 'blue' }) => (
  <div className="bg-gray-800 rounded-lg p-6 border border-gray-700 hover:border-gray-600 transition-all">
    <div className="flex items-center justify-between mb-2">
      <Tooltip content={label}>
        <Icon className={`h-5 w-5 text-${color}-400`} />
      </Tooltip>
      {trend && (
        <span className={`text-xs ${trend > 0 ? 'text-green-400' : 'text-red-400'} flex items-center`}>
          <TrendingUp className="h-3 w-3 mr-1" />
          {Math.abs(trend)}%
        </span>
      )}
    </div>
    <p className="text-2xl font-bold text-white">{value}</p>
    <p className="text-sm text-gray-400">{label}</p>
  </div>
));

StatCard.displayName = 'StatCard';

// Main dashboard component with optimizations
const Dashboard = memo(() => {
  // Memoize chart data to prevent recalculation
  const chartData = useMemo(() => ({
    labels: ['00:00', '04:00', '08:00', '12:00', '16:00', '20:00', '24:00'],
    datasets: [
      {
        label: 'Active Flows',
        data: [2, 3, 5, 8, 12, 9, 4],
        borderColor: 'rgb(59, 130, 246)',
        backgroundColor: 'rgba(59, 130, 246, 0.1)',
        tension: 0.4,
      },
      {
        label: 'Executions',
        data: [10, 15, 25, 45, 38, 22, 12],
        borderColor: 'rgb(34, 197, 94)',
        backgroundColor: 'rgba(34, 197, 94, 0.1)',
        tension: 0.4,
      }
    ]
  }), []);

  // Memoize chart options
  const chartOptions = useMemo(() => ({
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'top',
        labels: {
          color: '#9CA3AF',
          font: {
            size: 12
          }
        }
      },
      tooltip: {
        mode: 'index',
        intersect: false,
        backgroundColor: '#1F2937',
        titleColor: '#F3F4F6',
        bodyColor: '#D1D5DB',
        borderColor: '#374151',
        borderWidth: 1
      }
    },
    scales: {
      x: {
        grid: {
          color: '#374151',
          drawBorder: false
        },
        ticks: {
          color: '#9CA3AF'
        }
      },
      y: {
        grid: {
          color: '#374151',
          drawBorder: false
        },
        ticks: {
          color: '#9CA3AF'
        }
      }
    }
  }), []);

  // Memoize stats data
  const stats = useMemo(() => [
    { icon: Zap, label: 'Active Flows', value: '12', trend: 8, color: 'blue' },
    { icon: Clock, label: 'Avg. Execution Time', value: '2.3s', trend: -12, color: 'yellow' },
    { icon: CheckCircle, label: 'Success Rate', value: '99.8%', trend: 2, color: 'green' },
    { icon: Activity, label: 'Total Executions', value: '1,234', trend: 15, color: 'purple' }
  ], []);

  // Use callback for event handlers
  const handleRefresh = useCallback(() => {
    // Refresh logic here
    console.log('Refreshing dashboard...');
  }, []);

  return (
    <div className="p-6 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex justify-between items-start mb-8">
        <div>
          <h1 className="text-3xl font-bold text-white mb-2">Dashboard</h1>
          <p className="text-gray-400">Monitor your automation flows in real-time</p>
        </div>
        <ThemeSwitcher />
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        {stats.map((stat, index) => (
          <StatCard key={index} {...stat} />
        ))}
      </div>

      {/* Charts and Activity */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Main Chart */}
        <div className="lg:col-span-2 bg-gray-800 rounded-lg p-6 border border-gray-700">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-white flex items-center">
              <BarChart3 className="h-5 w-5 mr-2 text-blue-400" />
              Flow Activity
            </h2>
            <button 
              onClick={handleRefresh}
              className="text-sm text-gray-400 hover:text-white transition-colors"
            >
              Refresh
            </button>
          </div>
          <div className="h-64">
            <Suspense fallback={<div className="h-full bg-gray-700 animate-pulse rounded" />}>
              <Chart 
                data={chartData} 
                options={chartOptions} 
                role="img"
                aria-label="Line chart showing active flows and executions over the last 24 hours"
              />
            </Suspense>
          </div>
        </div>

        {/* Activity Feed */}
        <div className="bg-gray-800 rounded-lg p-6 border border-gray-700">
          <h2 className="text-lg font-semibold text-white mb-4 flex items-center">
            <Activity className="h-5 w-5 mr-2 text-green-400" />
            Recent Activity
          </h2>
          <Suspense fallback={<ActivitySkeleton />}>
            <ActivityFeed />
          </Suspense>
        </div>
      </div>

      {/* Quick Actions */}
      <div className="mt-8 grid grid-cols-1 md:grid-cols-3 gap-4">
        <QuickActionCard 
          icon={Calendar} 
          title="Schedule Flow" 
          description="Set up automated schedules"
        />
        <QuickActionCard 
          icon={Zap} 
          title="Create Flow" 
          description="Build a new automation"
        />
        <QuickActionCard 
          icon={Settings} 
          title="Settings" 
          description="Configure your preferences"
        />
      </div>
    </div>
  );
});

Dashboard.displayName = 'Dashboard';

// Memoized quick action card
const QuickActionCard = memo(({ icon: Icon, title, description }) => (
  <Tooltip content={description}>
    <button className="bg-gray-800 rounded-lg p-4 border border-gray-700 hover:border-blue-500 transition-all text-left group">
    <Icon className="h-8 w-8 text-gray-400 group-hover:text-blue-400 transition-colors mb-2" />
    <h3 className="text-white font-semibold">{title}</h3>
    <p className="text-sm text-gray-400">{description}</p>
    </button>
  </Tooltip>
));

QuickActionCard.displayName = 'QuickActionCard';

// Loading skeleton for activity feed
const ActivitySkeleton = () => (
  <div className="space-y-3">
    {[...Array(5)].map((_, i) => (
      <div key={i} className="animate-pulse">
        <div className="h-4 bg-gray-700 rounded w-3/4 mb-2"></div>
        <div className="h-3 bg-gray-700 rounded w-1/2"></div>
      </div>
    ))}
  </div>
);

export default Dashboard;
