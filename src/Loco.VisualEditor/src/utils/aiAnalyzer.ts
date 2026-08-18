/**
 * AI Analyzer for Intelligent Workflow Analysis
 *
 * Provides AI-powered analysis and recommendations:
 * - Validation issue explanation and resolution suggestions
 * - Performance optimization recommendations
 * - Workflow pattern analysis and improvements
 * - Error recovery strategies
 */

import { ValidationReport, ValidationIssue } from './workflowValidationService';
import { Node, Edge } from 'reactflow';

// ============================================================================
// Types
// ============================================================================

export interface AIInsight {
  id: string;
  type: 'optimization' | 'pattern' | 'error_fix' | 'performance' | 'security';
  priority: 'high' | 'medium' | 'low';
  title: string;
  description: string;
  explanation: string;
  suggestedActions: Array<{
    action: string;
    impact: string;
    difficulty: 'easy' | 'medium' | 'hard';
  }>;
  estimatedBenefit?: string;
  relatedIssues?: string[];
}

export interface AIAnalysisResult {
  insights: AIInsight[];
  overallScore: number;
  optimizationPotential: number;
  keyRecommendations: string[];
  risksIdentified: string[];
  estimatedImprovementPercentage: number;
}

export interface WorkflowPattern {
  name: string;
  description: string;
  nodeTypes: string[];
  confidence: number;
  recommendations: string[];
}

// ============================================================================
// AI-Powered Issue Analysis
// ============================================================================

/**
 * Analyze validation issues and provide intelligent explanations
 */
export function analyzeValidationIssues(issues: ValidationIssue[]): AIInsight[] {
  const insights: AIInsight[] = [];

  // Group issues by category
  const issuesByCategory = new Map<string, ValidationIssue[]>();
  issues.forEach((issue) => {
    const key = issue.category;
    if (!issuesByCategory.has(key)) {
      issuesByCategory.set(key, []);
    }
    issuesByCategory.get(key)!.push(issue);
  });

  // Analyze structure issues
  const structureIssues = issuesByCategory.get('structure') || [];
  if (structureIssues.length > 0) {
    insights.push({
      id: 'structure-analysis',
      type: 'error_fix',
      priority: 'high',
      title: 'Workflow Structure Issues',
      description: `Found ${structureIssues.length} structural problem${structureIssues.length > 1 ? 's' : ''}`,
      explanation:
        'Your workflow has structural issues that could prevent execution. These typically involve missing connections, orphaned nodes, or circular dependencies.',
      suggestedActions: [
        {
          action: 'Verify all nodes are properly connected',
          impact: 'Ensures data flows correctly through the workflow',
          difficulty: 'easy',
        },
        {
          action: 'Add missing trigger nodes if needed',
          impact: 'Provides entry points for workflow execution',
          difficulty: 'easy',
        },
        {
          action: 'Resolve circular dependencies with conditions',
          impact: 'Prevents infinite loops',
          difficulty: 'medium',
        },
      ],
      relatedIssues: structureIssues.map((i) => i.id),
    });
  }

  // Analyze data flow issues
  const dataFlowIssues = issuesByCategory.get('data_flow') || [];
  if (dataFlowIssues.length > 0) {
    insights.push({
      id: 'dataflow-analysis',
      type: 'error_fix',
      priority: 'high',
      title: 'Data Flow Compatibility Issues',
      description: `Found ${dataFlowIssues.length} data flow issue${dataFlowIssues.length > 1 ? 's' : ''}`,
      explanation:
        'Some nodes may not receive the correct data types they expect. This could cause runtime errors even if the structure is valid.',
      suggestedActions: [
        {
          action: 'Review parameter mappings between connected nodes',
          impact: 'Ensures correct data transformation',
          difficulty: 'medium',
        },
        {
          action: 'Add transform nodes to convert data types',
          impact: 'Makes incompatible data sources compatible',
          difficulty: 'medium',
        },
        {
          action: 'Use explicit type casting in transforms',
          impact: 'Makes data conversions explicit and debuggable',
          difficulty: 'easy',
        },
      ],
      relatedIssues: dataFlowIssues.map((i) => i.id),
    });
  }

  // Analyze configuration issues
  const configIssues = issuesByCategory.get('configuration') || [];
  if (configIssues.length > 0) {
    insights.push({
      id: 'config-analysis',
      type: 'error_fix',
      priority: 'high',
      title: 'Missing Configuration',
      description: `Found ${configIssues.length} configuration issue${configIssues.length > 1 ? 's' : ''}`,
      explanation:
        'Some nodes are missing required configuration such as integration selection, action type, or required parameters.',
      suggestedActions: [
        {
          action: 'Select integrations for all action nodes',
          impact: 'Enables integration functionality',
          difficulty: 'easy',
        },
        {
          action: 'Fill in all required parameters',
          impact: 'Allows actions to execute properly',
          difficulty: 'easy',
        },
        {
          action: 'Add descriptive labels to nodes',
          impact: 'Improves workflow readability',
          difficulty: 'easy',
        },
      ],
      relatedIssues: configIssues.map((i) => i.id),
    });
  }

  return insights;
}

// ============================================================================
// Workflow Pattern Detection
// ============================================================================

/**
 * Detect common workflow patterns
 */
export function detectWorkflowPatterns(nodes: Node[], edges: Edge[]): WorkflowPattern[] {
  const patterns: WorkflowPattern[] = [];
  const nodeTypes = new Set(nodes.map((n) => n.type));

  // Pattern 1: Request-Response pattern
  if (nodeTypes.has('action') && nodeTypes.has('transform')) {
    const actionNodes = nodes.filter((n) => n.type === 'action');
    const transformNodes = nodes.filter((n) => n.type === 'transform');

    if (actionNodes.length > 0 && transformNodes.length > 0) {
      patterns.push({
        name: 'Request-Response',
        description: 'Actions fetch data and transforms process the response',
        nodeTypes: ['action', 'transform'],
        confidence: 0.8,
        recommendations: [
          'Consider adding error handling for API calls',
          'Implement retry logic for transient failures',
          'Add timeout configurations to prevent hanging requests',
        ],
      });
    }
  }

  // Pattern 2: Conditional Logic
  if (nodeTypes.has('condition')) {
    const conditionNodes = nodes.filter((n) => n.type === 'condition');
    const conditionDepth = calculateConditionDepth(nodes, edges);

    patterns.push({
      name: 'Conditional Branching',
      description: `Workflow uses ${conditionNodes.length} condition node${conditionNodes.length > 1 ? 's' : ''}`,
      nodeTypes: ['condition'],
      confidence: 1.0,
      recommendations:
        conditionDepth > 3
          ? [
              'Consider simplifying deeply nested conditions',
              'Use a lookup table or state machine for complex logic',
              'Break into multiple workflows for clarity',
            ]
          : [
              'Ensure all branches are tested',
              'Consider adding default fallback paths',
              'Document the logic for maintenance',
            ],
    });
  }

  // Pattern 3: Batch Processing
  if (nodeTypes.has('loop')) {
    patterns.push({
      name: 'Batch Processing',
      description: 'Workflow iterates over collections',
      nodeTypes: ['loop'],
      confidence: 0.9,
      recommendations: [
        'Consider parallelization if operations are independent',
        'Implement pagination for large datasets',
        'Add monitoring for loop iterations',
      ],
    });
  }

  // Pattern 4: Data Transformation Pipeline
  if (
    nodeTypes.has('transform') &&
    nodes.filter((n) => n.type === 'transform').length >= 2
  ) {
    patterns.push({
      name: 'Transformation Pipeline',
      description:
        'Multiple transforms process data sequentially',
      nodeTypes: ['transform'],
      confidence: 0.85,
      recommendations: [
        'Verify data flows correctly between transforms',
        'Consider combining adjacent transforms for efficiency',
        'Add type definitions at each pipeline stage',
      ],
    });
  }

  return patterns;
}

/**
 * Calculate condition nesting depth
 */
function calculateConditionDepth(nodes: Node[], edges: Edge[]): number {
  const graph = new Map<string, string[]>();
  nodes.forEach((n) => graph.set(n.id, []));
  edges.forEach((e) => {
    if (graph.has(e.source)) {
      graph.get(e.source)!.push(e.target);
    }
  });

  let maxDepth = 0;

  function dfs(nodeId: string, depth: number): void {
    const node = nodes.find((n) => n.id === nodeId);
    if (!node) return;

    if (node.type === 'condition') {
      maxDepth = Math.max(maxDepth, depth + 1);
    }

    const children = graph.get(nodeId) || [];
    children.forEach((childId) => {
      const childNode = nodes.find((n) => n.id === childId);
      const newDepth = childNode?.type === 'condition' ? depth + 1 : depth;
      dfs(childId, newDepth);
    });
  }

  const startNode = nodes.find((n) => n.type === 'trigger');
  if (startNode) {
    dfs(startNode.id, 0);
  }

  return maxDepth;
}

// ============================================================================
// Performance Optimization Recommendations
// ============================================================================

/**
 * Generate performance optimization insights
 */
export function generatePerformanceInsights(
  _nodes: Node[],
  _edges: Edge[],
  validationReport: ValidationReport
): AIInsight[] {
  const insights: AIInsight[] = [];

  // Check for parallelization opportunities
  if (validationReport.performance.parallelizationOpportunities > 0) {
    insights.push({
      id: 'parallelization-opportunity',
      type: 'performance',
      priority: 'medium',
      title: 'Parallelization Opportunity',
      description: `Workflow has ${validationReport.performance.parallelizationOpportunities} independent branch${validationReport.performance.parallelizationOpportunities > 1 ? 'es' : ''}`,
      explanation:
        'Some branches of your workflow could potentially execute in parallel, which could significantly reduce execution time.',
      suggestedActions: [
        {
          action: 'Identify independent node sequences',
          impact: 'Reduces total execution time',
          difficulty: 'medium',
        },
        {
          action: 'Use parallel execution features if available',
          impact: 'Can achieve near-linear speedup for independent operations',
          difficulty: 'medium',
        },
        {
          action: 'Add synchronization points as needed',
          impact: 'Ensures data consistency between parallel branches',
          difficulty: 'hard',
        },
      ],
      estimatedBenefit: `Potential ${Math.round(validationReport.performance.parallelizationOpportunities * 20)}% speed improvement`,
    });
  }

  // Check for bottlenecks
  const highImpactBottlenecks = validationReport.performance.bottlenecks.filter(
    (b) => b.impact === 'high'
  );

  if (highImpactBottlenecks.length > 0) {
    insights.push({
      id: 'bottleneck-analysis',
      type: 'performance',
      priority: 'high',
      title: 'Critical Performance Bottlenecks',
      description: `Identified ${highImpactBottlenecks.length} bottleneck${highImpactBottlenecks.length > 1 ? 's' : ''}`,
      explanation:
        'These nodes are likely to be performance bottlenecks. Optimizing them could have significant impact on overall workflow performance.',
      suggestedActions: [
        {
          action: 'Review node configuration and optimize parameters',
          impact: 'Can reduce execution time per node',
          difficulty: 'medium',
        },
        {
          action: 'Consider caching results if node is called multiple times',
          impact: 'Eliminates redundant execution',
          difficulty: 'medium',
        },
        {
          action: 'Implement async/await patterns if available',
          impact: 'Allows non-blocking execution',
          difficulty: 'hard',
        },
      ],
      estimatedBenefit: 'Could significantly improve overall workflow performance',
    });
  }

  // Memory usage check
  if (validationReport.performance.estimatedMemoryUsage > 100 * 1024 * 1024) {
    insights.push({
      id: 'memory-optimization',
      type: 'performance',
      priority: 'medium',
      title: 'Memory Usage Optimization',
      description: `Workflow estimated memory usage is ${(validationReport.performance.estimatedMemoryUsage / 1024 / 1024).toFixed(1)}MB`,
      explanation:
        'Your workflow may consume significant memory. Consider optimization techniques to reduce memory footprint.',
      suggestedActions: [
        {
          action: 'Stream data instead of loading all at once',
          impact: 'Reduces peak memory usage',
          difficulty: 'hard',
        },
        {
          action: 'Implement garbage collection strategies',
          impact: 'Frees memory after processing',
          difficulty: 'medium',
        },
        {
          action: 'Reduce data duplication in transforms',
          impact: 'Minimizes unnecessary memory allocation',
          difficulty: 'medium',
        },
      ],
    });
  }

  // Data volume check
  if (validationReport.performance.estimatedDataVolume > 50 * 1024 * 1024) {
    insights.push({
      id: 'data-volume-optimization',
      type: 'performance',
      priority: 'medium',
      title: 'Large Data Volume Handling',
      description: `Workflow processes approximately ${(validationReport.performance.estimatedDataVolume / 1024 / 1024).toFixed(1)}MB of data`,
      explanation:
        'Your workflow handles significant data volume. Consider optimization techniques for large data processing.',
      suggestedActions: [
        {
          action: 'Implement pagination or chunking',
          impact: 'Allows processing of larger datasets',
          difficulty: 'medium',
        },
        {
          action: 'Use data filtering early in the pipeline',
          impact: 'Reduces data passed through subsequent nodes',
          difficulty: 'easy',
        },
        {
          action: 'Compress data between stages if applicable',
          impact: 'Reduces memory and network usage',
          difficulty: 'medium',
        },
      ],
    });
  }

  return insights;
}

// ============================================================================
// Security Analysis
// ============================================================================

/**
 * Generate security-focused insights
 */
export function generateSecurityInsights(issues: ValidationIssue[]): AIInsight[] {
  const insights: AIInsight[] = [];
  const securityIssues = issues.filter((i) => i.category === 'security');

  if (securityIssues.length > 0) {
    insights.push({
      id: 'security-concerns',
      type: 'security',
      priority: 'high',
      title: 'Security Issues Detected',
      description: `Found ${securityIssues.length} security concern${securityIssues.length > 1 ? 's' : ''}`,
      explanation:
        'Your workflow contains potential security vulnerabilities that should be addressed before production deployment.',
      suggestedActions: [
        {
          action: 'Move hardcoded credentials to environment variables',
          impact: 'Prevents exposure of sensitive data in version control',
          difficulty: 'easy',
        },
        {
          action: 'Replace dynamic code generation with static alternatives',
          impact: 'Reduces attack surface and improves performance',
          difficulty: 'hard',
        },
        {
          action: 'Add input validation and sanitization',
          impact: 'Prevents injection attacks',
          difficulty: 'medium',
        },
      ],
      relatedIssues: securityIssues.map((i) => i.id),
    });
  }

  return insights;
}

// ============================================================================
// Comprehensive AI Analysis
// ============================================================================

/**
 * Perform comprehensive AI analysis of workflow
 */
export function analyzeWorkflow(
  nodes: Node[],
  edges: Edge[],
  validationReport: ValidationReport
): AIAnalysisResult {
  const insights: AIInsight[] = [];

  // Collect insights from different analysis types
  insights.push(...analyzeValidationIssues(validationReport.issues));
  insights.push(...generatePerformanceInsights(nodes, edges, validationReport));
  insights.push(...generateSecurityInsights(validationReport.issues));

  // Detect patterns
  const patterns = detectWorkflowPatterns(nodes, edges);

  // Generate pattern-based insights
  patterns.forEach((pattern) => {
    if (pattern.recommendations.length > 0) {
      insights.push({
        id: `pattern-${pattern.name.toLowerCase().replace(/\s+/g, '-')}`,
        type: 'pattern',
        priority: 'low',
        title: `${pattern.name} Pattern Detected`,
        description: pattern.description,
        explanation: `Your workflow exhibits the "${pattern.name}" pattern. ${pattern.recommendations[0]}`,
        suggestedActions: pattern.recommendations.map((rec) => ({
          action: rec,
          impact: 'Improves workflow quality and maintainability',
          difficulty: 'easy',
        })),
      });
    }
  });

  // Calculate scores
  const errorCount = validationReport.issues.filter((i) => i.severity === 'error').length;
  const warningCount = validationReport.issues.filter((i) => i.severity === 'warning').length;

  const overallScore = validationReport.overallScore;
  const optimizationPotential = Math.min(100, 100 - overallScore + insights.length * 5);

  // Key recommendations
  const keyRecommendations = insights
    .filter((i) => i.priority === 'high')
    .slice(0, 3)
    .map((i) => i.title);

  // Risks identified
  const risksIdentified = insights
    .filter((i) => ['error_fix', 'security'].includes(i.type))
    .map((i) => i.title);

  // Estimated improvement
  const estimatedImprovementPercentage = Math.min(
    50,
    Math.round(
      ((errorCount * 10 + warningCount * 5) / Math.max(1, nodes.length)) * 2
    )
  );

  return {
    insights,
    overallScore,
    optimizationPotential,
    keyRecommendations,
    risksIdentified,
    estimatedImprovementPercentage,
  };
}
