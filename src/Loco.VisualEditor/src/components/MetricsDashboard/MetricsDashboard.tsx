/**
 * Metrics Dashboard Component
 *
 * Displays comprehensive workflow execution metrics, performance analytics,
 * and usage statistics for monitoring and optimization.
 */

import { useState, useEffect } from 'react';
import {
  X,
  Activity,
  TrendingUp,
  TrendingDown,
  Clock,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Zap,
  BarChart3,
  PieChart,
  Calendar,
} from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface MetricsDashboardProps {
  isOpen: boolean;
  onClose: () => void;
}

interface ExecutionMetrics {
  total: number;
  successful: number;
  failed: number;
  running: number;
  successRate: number;
  avgDuration: number;
  totalDuration: number;
}

interface PerformanceMetrics {
  p50Duration: number;
  p95Duration: number;
  p99Duration: number;
  fastestExecution: number;
  slowestExecution: number;
}

interface UsageMetrics {
  totalWorkflows: number;
  activeWorkflows: number;
  scheduledExecutions: number;
  webhookTriggers: number;
  apiCalls: number;
}

interface TopWorkflow {
  id: string;
  name: string;
  executionCount: number;
  successRate: number;
  avgDuration: number;
}

// ============================================================================
// Metrics Dashboard Component
// ============================================================================

export function MetricsDashboard({ isOpen, onClose }: MetricsDashboardProps) {
  const [timeRange, setTimeRange] = useState<'24h' | '7d' | '30d' | '90d'>('7d');
  const [executionMetrics, setExecutionMetrics] = useState<ExecutionMetrics | null>(null);
  const [performanceMetrics, setPerformanceMetrics] = useState<PerformanceMetrics | null>(null);
  const [usageMetrics, setUsageMetrics] = useState<UsageMetrics | null>(null);
  const [topWorkflows, setTopWorkflows] = useState<TopWorkflow[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const toast = useToast();

  // Fetch metrics data
  useEffect(() => {
    if (!isOpen) return;

    const fetchMetrics = async () => {
      setIsLoading(true);
      try {
        // TODO: Replace with actual API calls
        // const [execMetrics, perfMetrics, usageMetrics, trends, topWf] = await Promise.all([
        //   getExecutionMetrics(timeRange),
        //   getPerformanceMetrics(timeRange),
        //   getUsageMetrics(timeRange),
        //   getTrendData(timeRange),
        //   getTopWorkflows(timeRange),
        // ]);

        // Mock data for demonstration
        await new Promise((resolve) => setTimeout(resolve, 800));

        setExecutionMetrics({
          total: 1247,
          successful: 1089,
          failed: 142,
          running: 16,
          successRate: 87.3,
          avgDuration: 3420,
          totalDuration: 4266540,
        });

        setPerformanceMetrics({
          p50Duration: 2100,
          p95Duration: 8500,
          p99Duration: 15200,
          fastestExecution: 320,
          slowestExecution: 28400,
        });

        setUsageMetrics({
          totalWorkflows: 48,
          activeWorkflows: 32,
          scheduledExecutions: 156,
          webhookTriggers: 423,
          apiCalls: 2891,
        });

        setTopWorkflows([
          {
            id: 'wf-1',
            name: 'Process Payment',
            executionCount: 342,
            successRate: 94.7,
            avgDuration: 2100,
          },
          {
            id: 'wf-2',
            name: 'Send Email Notification',
            executionCount: 256,
            successRate: 98.4,
            avgDuration: 890,
          },
          {
            id: 'wf-3',
            name: 'Data Synchronization',
            executionCount: 189,
            successRate: 85.2,
            avgDuration: 5600,
          },
          {
            id: 'wf-4',
            name: 'User Onboarding',
            executionCount: 124,
            successRate: 91.1,
            avgDuration: 3200,
          },
          {
            id: 'wf-5',
            name: 'Report Generation',
            executionCount: 98,
            successRate: 76.5,
            avgDuration: 12400,
          },
        ]);
      } catch (error) {
        console.error('Failed to fetch metrics:', error);
        toast.error('Failed to load metrics data');
      } finally {
        setIsLoading(false);
      }
    };

    fetchMetrics();
  }, [isOpen, timeRange, toast]);

  const formatDuration = (ms: number) => {
    if (ms < 1000) return `${ms}ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
    return `${(ms / 60000).toFixed(1)}m`;
  };

  const formatNumber = (num: number) => {
    return num.toLocaleString();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-7xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Metrics Dashboard</h2>
            <p className="text-sm text-gray-500 mt-1">Performance analytics and usage statistics</p>
          </div>
          <div className="flex items-center gap-3">
            {/* Time Range Selector */}
            <div className="flex items-center gap-2 bg-gray-100 rounded-lg p-1">
              {(['24h', '7d', '30d', '90d'] as const).map((range) => (
                <button
                  key={range}
                  onClick={() => setTimeRange(range)}
                  className={`px-3 py-1 rounded text-sm font-medium transition-colors ${
                    timeRange === range
                      ? 'bg-white text-loco-primary shadow-sm'
                      : 'text-gray-600 hover:text-gray-900'
                  }`}
                >
                  {range}
                </button>
              ))}
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
              title="Close"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-loco-primary"></div>
            </div>
          ) : (
            <div className="space-y-6">
              {/* Execution Metrics */}
              {executionMetrics && (
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                    <Activity className="w-5 h-5 text-loco-primary" />
                    Execution Overview
                  </h3>
                  <div className="grid grid-cols-4 gap-4">
                    <div className="bg-gradient-to-br from-blue-50 to-blue-100 border border-blue-200 rounded-lg p-4">
                      <div className="flex items-center justify-between mb-2">
                        <span className="text-sm font-medium text-blue-700">Total Executions</span>
                        <Activity className="w-4 h-4 text-blue-600" />
                      </div>
                      <p className="text-2xl font-bold text-blue-900">{formatNumber(executionMetrics.total)}</p>
                      <p className="text-xs text-blue-600 mt-1">Last {timeRange}</p>
                    </div>

                    <div className="bg-gradient-to-br from-green-50 to-green-100 border border-green-200 rounded-lg p-4">
                      <div className="flex items-center justify-between mb-2">
                        <span className="text-sm font-medium text-green-700">Successful</span>
                        <CheckCircle className="w-4 h-4 text-green-600" />
                      </div>
                      <p className="text-2xl font-bold text-green-900">{formatNumber(executionMetrics.successful)}</p>
                      <p className="text-xs text-green-600 mt-1">{executionMetrics.successRate.toFixed(1)}% success rate</p>
                    </div>

                    <div className="bg-gradient-to-br from-red-50 to-red-100 border border-red-200 rounded-lg p-4">
                      <div className="flex items-center justify-between mb-2">
                        <span className="text-sm font-medium text-red-700">Failed</span>
                        <XCircle className="w-4 h-4 text-red-600" />
                      </div>
                      <p className="text-2xl font-bold text-red-900">{formatNumber(executionMetrics.failed)}</p>
                      <p className="text-xs text-red-600 mt-1">{((executionMetrics.failed / executionMetrics.total) * 100).toFixed(1)}% failure rate</p>
                    </div>

                    <div className="bg-gradient-to-br from-purple-50 to-purple-100 border border-purple-200 rounded-lg p-4">
                      <div className="flex items-center justify-between mb-2">
                        <span className="text-sm font-medium text-purple-700">Avg Duration</span>
                        <Clock className="w-4 h-4 text-purple-600" />
                      </div>
                      <p className="text-2xl font-bold text-purple-900">{formatDuration(executionMetrics.avgDuration)}</p>
                      <p className="text-xs text-purple-600 mt-1">Per execution</p>
                    </div>
                  </div>
                </div>
              )}

              {/* Performance Metrics */}
              {performanceMetrics && (
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                    <Zap className="w-5 h-5 text-yellow-600" />
                    Performance Metrics
                  </h3>
                  <div className="bg-white border border-gray-200 rounded-lg p-6">
                    <div className="grid grid-cols-5 gap-4">
                      <div className="text-center">
                        <p className="text-sm text-gray-600 mb-1">P50 (Median)</p>
                        <p className="text-xl font-bold text-gray-900">{formatDuration(performanceMetrics.p50Duration)}</p>
                      </div>
                      <div className="text-center">
                        <p className="text-sm text-gray-600 mb-1">P95</p>
                        <p className="text-xl font-bold text-gray-900">{formatDuration(performanceMetrics.p95Duration)}</p>
                      </div>
                      <div className="text-center">
                        <p className="text-sm text-gray-600 mb-1">P99</p>
                        <p className="text-xl font-bold text-gray-900">{formatDuration(performanceMetrics.p99Duration)}</p>
                      </div>
                      <div className="text-center">
                        <p className="text-sm text-gray-600 mb-1 flex items-center justify-center gap-1">
                          <TrendingUp className="w-3 h-3 text-green-600" />
                          Fastest
                        </p>
                        <p className="text-xl font-bold text-green-700">{formatDuration(performanceMetrics.fastestExecution)}</p>
                      </div>
                      <div className="text-center">
                        <p className="text-sm text-gray-600 mb-1 flex items-center justify-center gap-1">
                          <TrendingDown className="w-3 h-3 text-red-600" />
                          Slowest
                        </p>
                        <p className="text-xl font-bold text-red-700">{formatDuration(performanceMetrics.slowestExecution)}</p>
                      </div>
                    </div>
                  </div>
                </div>
              )}

              {/* Usage Metrics */}
              {usageMetrics && (
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                    <BarChart3 className="w-5 h-5 text-indigo-600" />
                    Usage Statistics
                  </h3>
                  <div className="grid grid-cols-5 gap-4">
                    <div className="bg-white border border-gray-200 rounded-lg p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <PieChart className="w-4 h-4 text-gray-600" />
                        <span className="text-sm font-medium text-gray-700">Workflows</span>
                      </div>
                      <p className="text-2xl font-bold text-gray-900">{usageMetrics.totalWorkflows}</p>
                      <p className="text-xs text-gray-600 mt-1">{usageMetrics.activeWorkflows} active</p>
                    </div>

                    <div className="bg-white border border-gray-200 rounded-lg p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <Calendar className="w-4 h-4 text-gray-600" />
                        <span className="text-sm font-medium text-gray-700">Scheduled</span>
                      </div>
                      <p className="text-2xl font-bold text-gray-900">{formatNumber(usageMetrics.scheduledExecutions)}</p>
                      <p className="text-xs text-gray-600 mt-1">Executions</p>
                    </div>

                    <div className="bg-white border border-gray-200 rounded-lg p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <Activity className="w-4 h-4 text-gray-600" />
                        <span className="text-sm font-medium text-gray-700">Webhooks</span>
                      </div>
                      <p className="text-2xl font-bold text-gray-900">{formatNumber(usageMetrics.webhookTriggers)}</p>
                      <p className="text-xs text-gray-600 mt-1">Triggers</p>
                    </div>

                    <div className="bg-white border border-gray-200 rounded-lg p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <Zap className="w-4 h-4 text-gray-600" />
                        <span className="text-sm font-medium text-gray-700">API Calls</span>
                      </div>
                      <p className="text-2xl font-bold text-gray-900">{formatNumber(usageMetrics.apiCalls)}</p>
                      <p className="text-xs text-gray-600 mt-1">Total requests</p>
                    </div>

                    <div className="bg-white border border-gray-200 rounded-lg p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <AlertTriangle className="w-4 h-4 text-gray-600" />
                        <span className="text-sm font-medium text-gray-700">Error Rate</span>
                      </div>
                      <p className="text-2xl font-bold text-gray-900">
                        {executionMetrics ? ((executionMetrics.failed / executionMetrics.total) * 100).toFixed(1) : '0'}%
                      </p>
                      <p className="text-xs text-gray-600 mt-1">Last {timeRange}</p>
                    </div>
                  </div>
                </div>
              )}

              {/* Top Workflows */}
              {topWorkflows.length > 0 && (
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                    <TrendingUp className="w-5 h-5 text-green-600" />
                    Top Workflows
                  </h3>
                  <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                    <table className="w-full">
                      <thead className="bg-gray-50">
                        <tr>
                          <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Workflow
                          </th>
                          <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Executions
                          </th>
                          <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Success Rate
                          </th>
                          <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Avg Duration
                          </th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-200">
                        {topWorkflows.map((workflow, index) => (
                          <tr key={workflow.id} className="hover:bg-gray-50">
                            <td className="px-4 py-3">
                              <div className="flex items-center gap-2">
                                <span className="text-xs font-medium text-gray-500">#{index + 1}</span>
                                <span className="font-medium text-gray-900">{workflow.name}</span>
                              </div>
                            </td>
                            <td className="px-4 py-3 text-right font-medium text-gray-900">
                              {formatNumber(workflow.executionCount)}
                            </td>
                            <td className="px-4 py-3 text-right">
                              <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
                                workflow.successRate >= 95
                                  ? 'bg-green-100 text-green-700'
                                  : workflow.successRate >= 85
                                  ? 'bg-yellow-100 text-yellow-700'
                                  : 'bg-red-100 text-red-700'
                              }`}>
                                {workflow.successRate.toFixed(1)}%
                              </span>
                            </td>
                            <td className="px-4 py-3 text-right font-medium text-gray-900">
                              {formatDuration(workflow.avgDuration)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
