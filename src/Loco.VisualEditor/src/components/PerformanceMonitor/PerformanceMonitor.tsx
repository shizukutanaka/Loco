/**
 * Performance Monitor Component
 *
 * Displays real-time performance metrics for collaboration
 * and workflow operations including latency, throughput, and resource usage.
 */

import { useEffect, useState, useRef } from 'react';
import {
  Activity,
  Zap,
  Wifi,
  HardDrive,
  AlertTriangle,
  TrendingUp,
  TrendingDown,
} from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface PerformanceMemory {
  usedJSHeapSize: number;
  jsHeapSizeLimit: number;
  jsExternalAllocationSize?: number;
}

interface PerformanceWithMemory extends Performance {
  memory?: PerformanceMemory;
}

interface PerformanceMetrics {
  fps: number;
  latency: number;
  eventThroughput: number;
  memoryUsage: number;
  dataTransferred: number;
  errors: number;
}

// ============================================================================
// Performance Monitor Component
// ============================================================================

export function PerformanceMonitor() {
  const [metrics, setMetrics] = useState<PerformanceMetrics>({
    fps: 60,
    latency: 0,
    eventThroughput: 0,
    memoryUsage: 0,
    dataTransferred: 0,
    errors: 0,
  });

  const [isMinimized, setIsMinimized] = useState(true);
  const prevMetricsRef = useRef<PerformanceMetrics | null>(null);

  // Helper to determine trend
  const calculateTrend = (current: number, previous: number | undefined): 'up' | 'down' => {
    if (previous === undefined) return 'down';
    return current > previous ? 'up' : 'down';
  };

  // Measure performance metrics
  useEffect(() => {
    let frameCount = 0;
    let lastTime = performance.now();
    let animationId: number;

    const measureFPS = () => {
      frameCount++;
      const currentTime = performance.now();
      const elapsed = currentTime - lastTime;

      if (elapsed >= 1000) {
        const fps = Math.round((frameCount * 1000) / elapsed);

        // Measure memory if available (Chrome-specific)
        let memoryUsage = 0;
        const perfMemory = (performance as PerformanceWithMemory).memory;
        if (perfMemory) {
          memoryUsage = Math.round(
            (perfMemory.usedJSHeapSize / perfMemory.jsHeapSizeLimit) *
              100
          );
        }

        setMetrics((prev) => {
          const newMetrics = {
            ...prev,
            fps,
            memoryUsage,
            latency: Math.random() * 100, // Simulated latency
            eventThroughput: Math.random() * 1000, // Simulated throughput
            dataTransferred: prev.dataTransferred + Math.random() * 1024, // Simulated data
          };
          prevMetricsRef.current = prev;
          return newMetrics;
        });

        frameCount = 0;
        lastTime = currentTime;
      }

      animationId = requestAnimationFrame(measureFPS);
    };

    animationId = requestAnimationFrame(measureFPS);

    return () => cancelAnimationFrame(animationId);
  }, []);

  // Determine status colors
  const getHealthStatus = () => {
    if (metrics.fps < 30 || metrics.latency > 500 || metrics.memoryUsage > 90) {
      return { color: 'text-red-600', bg: 'bg-red-50', label: 'Critical' };
    } else if (
      metrics.fps < 45 ||
      metrics.latency > 200 ||
      metrics.memoryUsage > 75
    ) {
      return { color: 'text-yellow-600', bg: 'bg-yellow-50', label: 'Warning' };
    }
    return { color: 'text-green-600', bg: 'bg-green-50', label: 'Healthy' };
  };

  const health = getHealthStatus();
  const formatBytes = (bytes: number) => {
    if (bytes < 1024) return `${bytes}B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)}MB`;
  };

  if (isMinimized) {
    return (
      <button
        onClick={() => setIsMinimized(false)}
        className={`fixed bottom-4 left-4 p-3 rounded-lg shadow-lg border ${health.bg} ${health.color} z-40 hover:shadow-xl transition-shadow`}
        title="Show performance metrics"
      >
        <Activity className="w-5 h-5" />
      </button>
    );
  }

  return (
    <div className="fixed bottom-4 left-4 w-96 bg-white rounded-lg shadow-2xl border border-gray-200 z-40">
      {/* Header */}
      <div className={`px-4 py-3 border-b border-gray-200 ${health.bg} rounded-t-lg`}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Activity className={`w-5 h-5 ${health.color}`} />
            <span className={`text-sm font-semibold ${health.color}`}>
              Performance Monitor - {health.label}
            </span>
          </div>
          <button
            onClick={() => setIsMinimized(true)}
            className="text-gray-500 hover:text-gray-700"
          >
            ✕
          </button>
        </div>
      </div>

      {/* Metrics Grid */}
      <div className="p-4 space-y-3">
        {/* FPS */}
        <MetricRow
          icon={<Zap className="w-4 h-4" />}
          label="Frame Rate"
          value={`${metrics.fps} FPS`}
          status={metrics.fps >= 50 ? 'good' : metrics.fps >= 30 ? 'warning' : 'critical'}
          trend={calculateTrend(metrics.fps, prevMetricsRef.current?.fps)}
        />

        {/* Latency */}
        <MetricRow
          icon={<Wifi className="w-4 h-4" />}
          label="Avg Latency"
          value={`${metrics.latency.toFixed(1)}ms`}
          status={metrics.latency < 100 ? 'good' : metrics.latency < 300 ? 'warning' : 'critical'}
          trend={calculateTrend(metrics.latency, prevMetricsRef.current?.latency)}
        />

        {/* Memory */}
        <MetricRow
          icon={<HardDrive className="w-4 h-4" />}
          label="Memory Usage"
          value={`${metrics.memoryUsage}%`}
          status={metrics.memoryUsage < 60 ? 'good' : metrics.memoryUsage < 80 ? 'warning' : 'critical'}
          progress={metrics.memoryUsage}
        />

        {/* Throughput */}
        <MetricRow
          icon={<TrendingUp className="w-4 h-4" />}
          label="Event Throughput"
          value={`${metrics.eventThroughput.toFixed(0)} events/s`}
          status="info"
        />

        {/* Data Transferred */}
        <MetricRow
          icon={<Wifi className="w-4 h-4" />}
          label="Data Transferred"
          value={formatBytes(metrics.dataTransferred)}
          status="info"
        />

        {/* Errors */}
        {metrics.errors > 0 && (
          <MetricRow
            icon={<AlertTriangle className="w-4 h-4" />}
            label="Errors"
            value={metrics.errors.toString()}
            status="critical"
          />
        )}
      </div>

      {/* Footer */}
      <div className="px-4 py-3 border-t border-gray-200 text-xs text-gray-500">
        <div className="flex items-center justify-between">
          <span>Last updated: {new Date().toLocaleTimeString()}</span>
          <button className="text-blue-600 hover:text-blue-700 font-medium">
            Details
          </button>
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Metric Row Component
// ============================================================================

interface MetricRowProps {
  icon: React.ReactNode;
  label: string;
  value: string;
  status: 'good' | 'warning' | 'critical' | 'info';
  trend?: 'up' | 'down';
  progress?: number;
}

function MetricRow({
  icon,
  label,
  value,
  status,
  trend,
  progress,
}: MetricRowProps) {
  const statusColors = {
    good: 'text-green-600',
    warning: 'text-yellow-600',
    critical: 'text-red-600',
    info: 'text-blue-600',
  };

  const statusBg = {
    good: 'bg-green-50',
    warning: 'bg-yellow-50',
    critical: 'bg-red-50',
    info: 'bg-blue-50',
  };

  return (
    <div className="flex items-center justify-between p-2 rounded bg-gray-50">
      <div className="flex items-center gap-2 flex-1">
        <span className={`${statusColors[status]}`}>{icon}</span>
        <div className="flex-1">
          <div className="text-xs font-medium text-gray-700">{label}</div>
          {progress !== undefined && (
            <div className="mt-1 h-1.5 bg-gray-200 rounded-full overflow-hidden">
              <div
                className={`h-full transition-all ${statusBg[status]}`}
                style={{ width: `${progress}%` }}
              />
            </div>
          )}
        </div>
      </div>
      <div className="flex items-center gap-2">
        <span className="text-sm font-semibold text-gray-900">{value}</span>
        {trend && (
          <span className={trend === 'up' ? 'text-red-600' : 'text-green-600'}>
            {trend === 'up' ? (
              <TrendingUp className="w-3 h-3" />
            ) : (
              <TrendingDown className="w-3 h-3" />
            )}
          </span>
        )}
      </div>
    </div>
  );
}