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

import { useState, useEffect } from 'react';
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
} from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface WorkflowTesterProps {
  workflowId: string;
  workflowName: string;
  isOpen: boolean;
  onClose: () => void;
}

type IssueType = 'error' | 'warning' | 'info' | 'success';
type IssueCategory = 'structure' | 'dataflow' | 'performance' | 'bestpractice';

interface ValidationIssue {
  id: string;
  type: IssueType;
  category: IssueCategory;
  title: string;
  description: string;
  nodeId?: string;
  nodeName?: string;
  suggestion?: string;
}

interface ValidationResult {
  passed: boolean;
  score: number;
  totalIssues: number;
  errors: number;
  warnings: number;
  infos: number;
  issues: ValidationIssue[];
}

interface PerformanceMetric {
  metric: string;
  value: string;
  status: 'good' | 'fair' | 'poor';
  description: string;
}

// ============================================================================
// Workflow Tester Component
// ============================================================================

export function WorkflowTester({
  workflowId,
  workflowName,
  isOpen,
  onClose,
}: WorkflowTesterProps) {
  const [isValidating, setIsValidating] = useState(false);
  const [validationResult, setValidationResult] = useState<ValidationResult | null>(null);
  const [performanceMetrics, setPerformanceMetrics] = useState<PerformanceMetric[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<IssueCategory | 'all'>('all');
  const toast = useToast();

  // Run validation on open
  useEffect(() => {
    if (isOpen) {
      handleValidate();
    }
  }, [isOpen]);

  const handleValidate = async () => {
    setIsValidating(true);
    try {
      // TODO: Replace with actual API call
      console.log('Validating workflow:', workflowId);
      // const response = await validateWorkflow(workflowId);

      await new Promise((resolve) => setTimeout(resolve, 1500));

      // Mock validation results
      const issues: ValidationIssue[] = [
        {
          id: 'issue-1',
          type: 'error',
          category: 'structure',
          title: 'Disconnected Node',
          description: 'Node "Transform Data" has no incoming connections',
          nodeId: 'node-3',
          nodeName: 'Transform Data',
          suggestion: 'Connect this node to a data source or remove it from the workflow',
        },
        {
          id: 'issue-2',
          type: 'warning',
          category: 'dataflow',
          title: 'Missing Error Handler',
          description: 'HTTP Request node lacks error handling configuration',
          nodeId: 'node-1',
          nodeName: 'HTTP Request',
          suggestion: 'Add error handling to prevent workflow failures on network issues',
        },
        {
          id: 'issue-3',
          type: 'warning',
          category: 'performance',
          title: 'Sequential Processing',
          description: 'Multiple independent API calls are executed sequentially',
          nodeId: 'node-2',
          nodeName: 'API Calls',
          suggestion: 'Consider using parallel execution for better performance',
        },
        {
          id: 'issue-4',
          type: 'info',
          category: 'bestpractice',
          title: 'Missing Timeout',
          description: 'HTTP Request has no timeout configured',
          nodeId: 'node-1',
          nodeName: 'HTTP Request',
          suggestion: 'Set a timeout to prevent hanging requests (recommended: 30s)',
        },
        {
          id: 'issue-5',
          type: 'info',
          category: 'bestpractice',
          title: 'No Retry Logic',
          description: 'API call lacks retry configuration for transient failures',
          nodeId: 'node-2',
          nodeName: 'API Calls',
          suggestion: 'Add retry logic with exponential backoff (recommended: 3 retries)',
        },
        {
          id: 'issue-6',
          type: 'success',
          category: 'structure',
          title: 'Valid Workflow Structure',
          description: 'All nodes are properly connected with valid edge configurations',
          suggestion: 'No action required',
        },
      ];

      const errors = issues.filter((i) => i.type === 'error').length;
      const warnings = issues.filter((i) => i.type === 'warning').length;
      const infos = issues.filter((i) => i.type === 'info').length;

      // Calculate score (100 - penalties)
      const score = Math.max(0, 100 - errors * 20 - warnings * 10 - infos * 2);

      setValidationResult({
        passed: errors === 0,
        score,
        totalIssues: issues.length,
        errors,
        warnings,
        infos,
        issues,
      });

      // Mock performance metrics
      setPerformanceMetrics([
        {
          metric: 'Estimated Duration',
          value: '2.3s',
          status: 'good',
          description: 'Expected time to complete workflow execution',
        },
        {
          metric: 'Complexity Score',
          value: '6/10',
          status: 'fair',
          description: 'Workflow complexity based on nodes and connections',
        },
        {
          metric: 'Error Handling Coverage',
          value: '60%',
          status: 'fair',
          description: 'Percentage of nodes with error handling configured',
        },
        {
          metric: 'Parallelization Potential',
          value: 'Medium',
          status: 'fair',
          description: 'Opportunities for parallel execution optimization',
        },
      ]);

      toast.success('Validation complete');
    } catch (error) {
      console.error('Validation failed:', error);
      toast.error('Failed to validate workflow');
    } finally {
      setIsValidating(false);
    }
  };

  const getIssueIcon = (type: IssueType) => {
    switch (type) {
      case 'error':
        return <AlertCircle className="w-5 h-5 text-red-600" />;
      case 'warning':
        return <AlertTriangle className="w-5 h-5 text-yellow-600" />;
      case 'info':
        return <Info className="w-5 h-5 text-blue-600" />;
      case 'success':
        return <CheckCircle className="w-5 h-5 text-green-600" />;
    }
  };

  const getIssueColor = (type: IssueType) => {
    switch (type) {
      case 'error':
        return 'bg-red-50 border-red-200';
      case 'warning':
        return 'bg-yellow-50 border-yellow-200';
      case 'info':
        return 'bg-blue-50 border-blue-200';
      case 'success':
        return 'bg-green-50 border-green-200';
    }
  };

  const getCategoryIcon = (category: IssueCategory) => {
    switch (category) {
      case 'structure':
        return <GitBranch className="w-4 h-4" />;
      case 'dataflow':
        return <FileCode className="w-4 h-4" />;
      case 'performance':
        return <Zap className="w-4 h-4" />;
      case 'bestpractice':
        return <TrendingUp className="w-4 h-4" />;
    }
  };

  const getScoreColor = (score: number) => {
    if (score >= 90) return 'text-green-600';
    if (score >= 70) return 'text-yellow-600';
    return 'text-red-600';
  };

  const getScoreLabel = (score: number) => {
    if (score >= 90) return 'Excellent';
    if (score >= 70) return 'Good';
    if (score >= 50) return 'Fair';
    return 'Needs Improvement';
  };

  const getPerformanceStatusColor = (status: 'good' | 'fair' | 'poor') => {
    switch (status) {
      case 'good':
        return 'text-green-600 bg-green-50';
      case 'fair':
        return 'text-yellow-600 bg-yellow-50';
      case 'poor':
        return 'text-red-600 bg-red-50';
    }
  };

  const filteredIssues = validationResult?.issues.filter(
    (issue) => selectedCategory === 'all' || issue.category === selectedCategory
  );

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
            <div className="flex flex-col items-center justify-center py-12">
              <RefreshCw className="w-12 h-12 text-loco-primary animate-spin mb-4" />
              <p className="text-gray-600">Validating workflow...</p>
              <p className="text-sm text-gray-500 mt-2">Analyzing structure, data flow, and performance</p>
            </div>
          ) : validationResult ? (
            <div className="space-y-6">
              {/* Score Summary */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="p-6 bg-gradient-to-br from-blue-50 to-blue-100 rounded-lg border border-blue-200">
                  <div className="flex items-center justify-between mb-4">
                    <h3 className="text-lg font-semibold text-gray-900">Overall Score</h3>
                    <CheckCircle className="w-6 h-6 text-blue-600" />
                  </div>
                  <div className="flex items-baseline gap-2">
                    <span className={`text-4xl font-bold ${getScoreColor(validationResult.score)}`}>
                      {validationResult.score}
                    </span>
                    <span className="text-2xl text-gray-500">/100</span>
                  </div>
                  <p className="text-sm text-gray-700 mt-2">{getScoreLabel(validationResult.score)}</p>
                </div>

                <div className="p-6 bg-white rounded-lg border border-gray-200">
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Issue Summary</h3>
                  <div className="space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="flex items-center gap-2 text-sm text-gray-700">
                        <AlertCircle className="w-4 h-4 text-red-600" />
                        Errors
                      </span>
                      <span className="font-semibold text-red-600">{validationResult.errors}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="flex items-center gap-2 text-sm text-gray-700">
                        <AlertTriangle className="w-4 h-4 text-yellow-600" />
                        Warnings
                      </span>
                      <span className="font-semibold text-yellow-600">{validationResult.warnings}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="flex items-center gap-2 text-sm text-gray-700">
                        <Info className="w-4 h-4 text-blue-600" />
                        Suggestions
                      </span>
                      <span className="font-semibold text-blue-600">{validationResult.infos}</span>
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
                          className={`px-2 py-1 text-xs font-medium rounded ${getPerformanceStatusColor(
                            metric.status
                          )}`}
                        >
                          {metric.status}
                        </span>
                      </div>
                      <p className="text-2xl font-bold text-gray-900 mb-1">{metric.value}</p>
                      <p className="text-xs text-gray-600">{metric.description}</p>
                    </div>
                  ))}
                </div>
              </div>

              {/* Category Filter */}
              <div>
                <h3 className="text-lg font-semibold text-gray-900 mb-4">Validation Issues</h3>
                <div className="flex gap-2 mb-4">
                  <button
                    onClick={() => setSelectedCategory('all')}
                    className={`px-3 py-1.5 text-sm rounded-lg transition-colors ${
                      selectedCategory === 'all'
                        ? 'bg-loco-primary text-white'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    All ({validationResult.issues.length})
                  </button>
                  <button
                    onClick={() => setSelectedCategory('structure')}
                    className={`flex items-center gap-1 px-3 py-1.5 text-sm rounded-lg transition-colors ${
                      selectedCategory === 'structure'
                        ? 'bg-loco-primary text-white'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    <GitBranch className="w-3 h-3" />
                    Structure
                  </button>
                  <button
                    onClick={() => setSelectedCategory('dataflow')}
                    className={`flex items-center gap-1 px-3 py-1.5 text-sm rounded-lg transition-colors ${
                      selectedCategory === 'dataflow'
                        ? 'bg-loco-primary text-white'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    <FileCode className="w-3 h-3" />
                    Data Flow
                  </button>
                  <button
                    onClick={() => setSelectedCategory('performance')}
                    className={`flex items-center gap-1 px-3 py-1.5 text-sm rounded-lg transition-colors ${
                      selectedCategory === 'performance'
                        ? 'bg-loco-primary text-white'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    <Zap className="w-3 h-3" />
                    Performance
                  </button>
                  <button
                    onClick={() => setSelectedCategory('bestpractice')}
                    className={`flex items-center gap-1 px-3 py-1.5 text-sm rounded-lg transition-colors ${
                      selectedCategory === 'bestpractice'
                        ? 'bg-loco-primary text-white'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    <TrendingUp className="w-3 h-3" />
                    Best Practices
                  </button>
                </div>

                {/* Issues List */}
                <div className="space-y-3">
                  {filteredIssues && filteredIssues.length > 0 ? (
                    filteredIssues.map((issue) => (
                      <div
                        key={issue.id}
                        className={`p-4 border rounded-lg ${getIssueColor(issue.type)}`}
                      >
                        <div className="flex items-start gap-3">
                          {getIssueIcon(issue.type)}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2 mb-1">
                              <h4 className="text-sm font-semibold text-gray-900">{issue.title}</h4>
                              {getCategoryIcon(issue.category)}
                            </div>
                            <p className="text-sm text-gray-700 mb-2">{issue.description}</p>
                            {issue.nodeName && (
                              <p className="text-xs text-gray-600 mb-2">
                                Node: <span className="font-medium">{issue.nodeName}</span>
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
              {validationResult && `${validationResult.totalIssues} issue${validationResult.totalIssues !== 1 ? 's' : ''} found`}
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={handleValidate}
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
