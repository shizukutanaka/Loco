/**
 * Workflow Tester Component
 *
 * Provides comprehensive workflow testing and validation:
 * - Structure validation (nodes, edges, connections)
 * - Data flow analysis
 * - Error detection and warnings
 * - Best practices recommendations
 * - Performance analysis
 * - Dry-run testing
 */

import { useState, useEffect, useCallback, useMemo, memo } from 'react';
import {
  X,
  Play,
  CheckCircle,
  AlertTriangle,
  AlertCircle,
  Info,
  Zap,
  GitBranch,
  FileCode,
  TrendingUp,
  RefreshCw,
  Cpu,
} from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';
import { useWorkflowStore } from '@/store/workflowStore';
import { ValidationReport } from '@/utils/workflowValidationService';
import { generateExecutionReport } from '@/utils/workflowSimulator';
import { Skeleton, SkeletonCard } from '@/components/Skeleton/Skeleton';

// ============================================================================
// Types
// ============================================================================

interface WorkflowTesterProps {
  workflowId: string;
  workflowName: string;
  isOpen: boolean;
  onClose: () => void;
}

type DisplayCategory = 'structure' | 'data_flow' | 'performance' | 'configuration' | 'best_practices' | 'security' | 'all';

interface PerformanceMetric {
  metric: string;
  value: string | number;
  status: 'good' | 'warning' | 'critical';
  description?: string;
}

// ============================================================================
// Constants (Memoized - prevent recreation on every render)
// ============================================================================

const SEVERITY_ICONS = {
  error: <AlertCircle className="w-5 h-5 text-red-600" />,
  warning: <AlertTriangle className="w-5 h-5 text-yellow-600" />,
  info: <Info className="w-5 h-5 text-blue-600" />,
};

const SEVERITY_COLORS = {
  error: 'bg-red-50 border-red-200',
  warning: 'bg-yellow-50 border-yellow-200',
  info: 'bg-blue-50 border-blue-200',
};

const CATEGORY_ICONS = {
  structure: <GitBranch className="w-4 h-4" />,
  data_flow: <FileCode className="w-4 h-4" />,
  performance: <Zap className="w-4 h-4" />,
  configuration: <Cpu className="w-4 h-4" />,
  best_practices: <TrendingUp className="w-4 h-4" />,
  security: <AlertTriangle className="w-4 h-4" />,
};

const CATEGORIES: Array<{ id: DisplayCategory; label: string; icon: React.ReactNode }> = [
  { id: 'all', label: 'All', icon: null },
  { id: 'structure', label: 'Structure', icon: CATEGORY_ICONS.structure },
  { id: 'data_flow', label: 'Data Flow', icon: CATEGORY_ICONS.data_flow },
  { id: 'performance', label: 'Performance', icon: CATEGORY_ICONS.performance },
  { id: 'best_practices', label: 'Best Practices', icon: CATEGORY_ICONS.best_practices },
  { id: 'security', label: 'Security', icon: CATEGORY_ICONS.security },
];

// Utility functions (memoized outside component)
const getScoreColor = (score: number): string => {
  if (score >= 90) return 'text-green-600';
  if (score >= 70) return 'text-yellow-600';
  return 'text-red-600';
};

const getScoreLabel = (score: number): string => {
  if (score >= 90) return 'Excellent';
  if (score >= 70) return 'Good';
  if (score >= 50) return 'Fair';
  return 'Needs Improvement';
};

const getMetricStatusColor = (status: 'good' | 'warning' | 'critical'): string => {
  switch (status) {
    case 'good':
      return 'text-green-600 bg-green-50';
    case 'warning':
      return 'text-yellow-600 bg-yellow-50';
    case 'critical':
      return 'text-red-600 bg-red-50';
  }
};

// ============================================================================
// Workflow Tester Component
// ============================================================================

function WorkflowTesterComponent({
  workflowId: _workflowId,
  workflowName,
  isOpen,
  onClose,
}: WorkflowTesterProps) {
  const [isValidating, setIsValidating] = useState(false);
  const [validationReport, setValidationReport] = useState<ValidationReport | null>(null);
  const [performanceMetrics, setPerformanceMetrics] = useState<PerformanceMetric[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<DisplayCategory>('all');
  const [simulationLog, setSimulationLog] = useState<string>('');
  const toast = useToast();
  const { nodes, edges } = useWorkflowStore();

  // Memoized handler to prevent unnecessary re-renders
  const handleValidateCallback = useCallback(async () => {
    setIsValidating(true);
    try {
      // Use unified analysis engine for consistent results
      const { analysisEngine } = await import('@/utils/workflowAnalysisEngine');
      const analysisResult = analysisEngine.analyze(nodes, edges);
      const report = analysisResult.validation;

      setValidationReport(report);

      // Convert report metrics to display metrics
      const displayMetrics: PerformanceMetric[] = [
        {
          metric: 'Estimated Duration',
          value: `${Math.round(report.performance.estimatedDuration)}ms`,
          status: report.performance.estimatedDuration < 5000 ? 'good' : report.performance.estimatedDuration < 15000 ? 'warning' : 'critical',
          description: 'Expected time to complete workflow execution',
        },
        {
          metric: 'Memory Usage',
          value: `${(report.performance.estimatedMemoryUsage / 1024 / 1024).toFixed(1)}MB`,
          status: report.performance.estimatedMemoryUsage < 50 * 1024 * 1024 ? 'good' : 'warning',
          description: 'Estimated memory consumption',
        },
        {
          metric: 'Node Coverage',
          value: `${report.coverage.nodesCovered}/${report.coverage.totalNodes}`,
          status: report.coverage.percentage > 80 ? 'good' : report.coverage.percentage > 50 ? 'warning' : 'critical',
          description: 'Nodes validated without errors',
        },
        {
          metric: 'Bottlenecks Detected',
          value: report.performance.bottlenecks.length,
          status: report.performance.bottlenecks.length === 0 ? 'good' : report.performance.bottlenecks.some((b) => b.impact === 'high') ? 'critical' : 'warning',
          description: 'Performance issues found',
        },
      ];

      if (analysisResult.cacheHit) {
        displayMetrics.push({
          metric: 'Cache Status',
          value: 'Cached',
          status: 'good',
          description: 'Using cached analysis results',
        });
      }

      setPerformanceMetrics(displayMetrics);

      // Use simulation from analysis engine
      const executionReport = generateExecutionReport(analysisResult.simulation);
      setSimulationLog(executionReport);

      toast.success(`Validation complete${analysisResult.cacheHit ? ' (cached)' : ''}`);
    } catch (error) {
      console.error('Validation failed:', error);
      toast.error('Failed to validate workflow');
    } finally {
      setIsValidating(false);
    }
  }, [nodes, edges, toast]);

  // Run validation on open
  useEffect(() => {
    if (isOpen) {
      handleValidateCallback();
    }
  }, [isOpen, handleValidateCallback]);

  // Memoized filtered issues to prevent recalculation on every render
  const filteredIssues = useMemo(
    () =>
      validationReport?.issues.filter(
        (issue) => selectedCategory === 'all' || issue.category === selectedCategory
      ),
    [validationReport, selectedCategory]
  );

  // Memoized issue counts to avoid multiple filter operations
  const issueCounts = useMemo(() => {
    if (!validationReport) return { errors: 0, warnings: 0, suggestions: 0 };
    return {
      errors: validationReport.issues.filter((i) => i.severity === 'error').length,
      warnings: validationReport.issues.filter((i) => i.severity === 'warning').length,
      suggestions: validationReport.issues.filter((i) => i.severity === 'info').length,
    };
  }, [validationReport]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-5xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Workflow Testing & Validation</h2>
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
          {isValidating ? (
            <div className="space-y-6">
              {/* Score Summary Skeletons */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <SkeletonCard lines={2} className="p-6 h-auto" />
                <SkeletonCard lines={2} className="p-6 h-auto" />
              </div>

              {/* Performance Metrics Skeletons */}
              <div>
                <Skeleton width="200px" height="24px" className="mb-4" />
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {Array.from({ length: 4 }).map((_, i) => (
                    <SkeletonCard key={i} lines={2} className="p-4 h-auto" />
                  ))}
                </div>
              </div>
            </div>
          ) : validationReport ? (
            <div className="space-y-6">
              {/* Score Summary */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="p-6 bg-gradient-to-br from-blue-50 to-blue-100 rounded-lg border border-blue-200">
                  <div className="flex items-center justify-between mb-4">
                    <h3 className="text-lg font-semibold text-gray-900">Overall Score</h3>
                    <CheckCircle className="w-6 h-6 text-blue-600" />
                  </div>
                  <div className="flex items-baseline gap-2">
                    <span className={`text-4xl font-bold ${getScoreColor(validationReport.overallScore)}`}>
                      {validationReport.overallScore}
                    </span>
                    <span className="text-2xl text-gray-500">/100</span>
                  </div>
                  <p className="text-sm text-gray-700 mt-2">{getScoreLabel(validationReport.overallScore)}</p>
                </div>

                <div className="p-6 bg-white rounded-lg border border-gray-200">
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Issue Summary</h3>
                  <div className="space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="flex items-center gap-2 text-sm text-gray-700">
                        {SEVERITY_ICONS.error}
                        Errors
                      </span>
                      <span className="font-semibold text-red-600">
                        {issueCounts.errors}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="flex items-center gap-2 text-sm text-gray-700">
                        {SEVERITY_ICONS.warning}
                        Warnings
                      </span>
                      <span className="font-semibold text-yellow-600">
                        {issueCounts.warnings}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="flex items-center gap-2 text-sm text-gray-700">
                        {SEVERITY_ICONS.info}
                        Suggestions
                      </span>
                      <span className="font-semibold text-blue-600">
                        {issueCounts.suggestions}
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Performance Metrics */}
              <div>
                <h3 className="text-lg font-semibold text-gray-900 mb-4">Performance Analysis</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {performanceMetrics.map((metric, index) => (
                    <div key={index} className="p-4 bg-white border border-gray-200 rounded-lg">
                      <div className="flex items-center justify-between mb-2">
                        <h4 className="text-sm font-semibold text-gray-900">{metric.metric}</h4>
                        <span
                          className={`px-2 py-1 text-xs font-medium rounded ${getMetricStatusColor(
                            metric.status
                          )}`}
                        >
                          {metric.status}
                        </span>
                      </div>
                      <p className="text-2xl font-bold text-gray-900 mb-1">{metric.value}</p>
                      {metric.description && <p className="text-xs text-gray-600">{metric.description}</p>}
                    </div>
                  ))}
                </div>
              </div>

              {/* Category Filter */}
              <div>
                <h3 className="text-lg font-semibold text-gray-900 mb-4">Validation Issues</h3>
                <div className="flex flex-wrap gap-2 mb-4">
                  {CATEGORIES.map((category) => (
                    <button
                      key={category.id}
                      onClick={() => setSelectedCategory(category.id)}
                      className={`flex items-center gap-1 px-3 py-1.5 text-sm rounded-lg transition-colors ${
                        selectedCategory === category.id
                          ? 'bg-loco-primary text-white'
                          : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                      }`}
                    >
                      {category.icon && <span className="w-3 h-3">{category.icon}</span>}
                      {category.id === 'all' ? `All (${validationReport.issues.length})` : category.label}
                    </button>
                  ))}
                </div>

                {/* Issues List */}
                <div className="space-y-3">
                  {filteredIssues && filteredIssues.length > 0 ? (
                    filteredIssues.map((issue) => (
                      <div
                        key={issue.id}
                        className={`p-4 border rounded-lg ${SEVERITY_COLORS[issue.severity]}`}
                      >
                        <div className="flex items-start gap-3">
                          {SEVERITY_ICONS[issue.severity]}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2 mb-1">
                              <h4 className="text-sm font-semibold text-gray-900">{issue.title}</h4>
                              {CATEGORY_ICONS[issue.category]}
                            </div>
                            <p className="text-sm text-gray-700 mb-2">{issue.description}</p>
                            {issue.nodeId && (
                              <p className="text-xs text-gray-600 mb-2">
                                Node: <span className="font-medium">{issue.nodeId}</span>
                              </p>
                            )}
                            {issue.suggestion && (
                              <div className="mt-2 p-2 bg-white bg-opacity-50 rounded text-xs text-gray-700">
                                <span className="font-medium">Suggestion:</span> {issue.suggestion}
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    ))
                  ) : (
                    <div className="text-center py-8 text-gray-500">
                      <CheckCircle className="w-12 h-12 mx-auto mb-2 text-green-500" />
                      <p>No issues found in this category</p>
                    </div>
                  )}
                </div>
              </div>

              {/* Recommendations */}
              {validationReport.recommendations.length > 0 && (
                <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                  <h4 className="text-sm font-semibold text-blue-900 mb-2">Recommendations</h4>
                  <ul className="text-sm text-blue-800 space-y-1">
                    {validationReport.recommendations.map((rec, index) => (
                      <li key={index} className="flex items-start gap-2">
                        <span className="text-blue-600 mt-0.5">•</span>
                        <span>{rec}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Simulation Log */}
              {simulationLog && (
                <div className="p-4 bg-gray-900 text-gray-100 rounded-lg font-mono text-xs overflow-auto max-h-40">
                  <h4 className="text-sm font-semibold text-white mb-2">Simulation Report</h4>
                  <pre className="whitespace-pre-wrap break-words">{simulationLog}</pre>
                </div>
              )}
            </div>
          ) : (
            <div className="text-center py-12">
              <Play className="w-16 h-16 text-gray-300 mx-auto mb-4" />
              <p className="text-gray-500">Click "Run Validation" to test your workflow</p>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50">
          <div className="flex items-center justify-between">
            <div className="text-sm text-gray-600">
              {validationReport && `${validationReport.issues.length} issue${validationReport.issues.length !== 1 ? 's' : ''} found`}
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={handleValidateCallback}
                disabled={isValidating}
                className="flex items-center gap-2 px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50"
              >
                <RefreshCw className={`w-4 h-4 ${isValidating ? 'animate-spin' : ''}`} />
                {isValidating ? 'Validating...' : 'Run Validation'}
              </button>
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
    </div>
  );
}

export const WorkflowTester = memo(WorkflowTesterComponent);
WorkflowTester.displayName = 'WorkflowTester';
