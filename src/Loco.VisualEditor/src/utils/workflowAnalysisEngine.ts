/**
 * Unified Workflow Analysis Engine
 *
 * Single source of truth for all workflow analysis:
 * - Combines validation, simulation, and AI analysis
 * - Implements caching to avoid redundant computation
 * - Provides incremental analysis for performance
 * - Tracks analysis history for change detection
 */

import { Node, Edge } from 'reactflow';
import { validateWorkflow, ValidationReport } from './workflowValidationService';
import { simulateWorkflow, SimulationResult } from './workflowSimulator';
import { analyzeWorkflow, AIAnalysisResult } from './aiAnalyzer';
import { deepClone } from './deepClone';
import { detectChanged } from './detectChanges';
import { ANALYSIS_CACHE_TTL } from './constants';

// ============================================================================
// Types
// ============================================================================

export interface WorkflowSnapshot {
  nodes: Node[];
  edges: Edge[];
  hash: string;
  timestamp: number;
}

export interface CachedAnalysis {
  validation: ValidationReport;
  simulation: SimulationResult;
  aiAnalysis: AIAnalysisResult;
  snapshot: WorkflowSnapshot;
  timestamp: number;
}

export interface AnalysisCache {
  current: CachedAnalysis | null;
  previous: WorkflowSnapshot | null;
}

export interface AnalysisResult {
  validation: ValidationReport;
  simulation: SimulationResult;
  aiAnalysis: AIAnalysisResult;
  isIncremental: boolean;
  changedNodeIds: Set<string>;
  changedEdgeIds: Set<string>;
  cacheHit: boolean;
}

// ============================================================================
// Utility Functions
// ============================================================================

/**
 * Simple hash function for workflow snapshot
 */
function hashWorkflow(nodes: Node[], edges: Edge[]): string {
  const nodeStr = JSON.stringify(nodes.sort((a, b) => a.id.localeCompare(b.id)));
  const edgeStr = JSON.stringify(edges.sort((a, b) => a.id.localeCompare(b.id)));
  let hash = 0;

  for (let i = 0; i < nodeStr.length; i++) {
    const char = nodeStr.charCodeAt(i);
    hash = (hash << 5) - hash + char;
    hash = hash & hash;
  }

  for (let i = 0; i < edgeStr.length; i++) {
    const char = edgeStr.charCodeAt(i);
    hash = (hash << 5) - hash + char;
    hash = hash & hash;
  }

  return Math.abs(hash).toString(36);
}

// Change detection functions use the generic detectChanged utility

// ============================================================================
// Unified Analysis Engine
// ============================================================================

export class WorkflowAnalysisEngine {
  private cache: AnalysisCache = {
    current: null,
    previous: null,
  };

  private maxCacheAge = ANALYSIS_CACHE_TTL;

  /**
   * Perform comprehensive workflow analysis with caching
   */
  public analyze(nodes: Node[], edges: Edge[]): AnalysisResult {
    const snapshot: WorkflowSnapshot = {
      nodes: deepClone(nodes),
      edges: deepClone(edges),
      hash: hashWorkflow(nodes, edges),
      timestamp: Date.now(),
    };

    // Check cache validity
    const cacheHit = this.isCacheValid(snapshot);

    if (cacheHit && this.cache.current) {
      const cached = this.cache.current;
      const changedNodes = this.cache.previous
        ? detectChanged({ items: this.cache.previous.nodes }, { items: snapshot.nodes })
        : new Set<string>();
      const changedEdges = this.cache.previous
        ? detectChanged({ items: this.cache.previous.edges }, { items: snapshot.edges })
        : new Set<string>();

      return {
        validation: cached.validation,
        simulation: cached.simulation,
        aiAnalysis: cached.aiAnalysis,
        isIncremental: changedNodes.size > 0 || changedEdges.size > 0,
        changedNodeIds: changedNodes,
        changedEdgeIds: changedEdges,
        cacheHit: true,
      };
    }

    // Perform fresh analysis
    const validation = validateWorkflow(nodes, edges);
    const simulation = simulateWorkflow(nodes, edges, {
      injectErrors: false,
      mockDelay: false,
      recordTrace: true,
    });
    const aiAnalysis = analyzeWorkflow(nodes, edges, validation);

    // Update cache
    this.cache.previous = this.cache.current?.snapshot || null;
    this.cache.current = {
      validation,
      simulation,
      aiAnalysis,
      snapshot,
      timestamp: Date.now(),
    };

    const changedNodes = this.cache.previous
      ? detectChanged({ items: this.cache.previous.nodes }, { items: snapshot.nodes })
      : new Set<string>();
    const changedEdges = this.cache.previous
      ? detectChanged({ items: this.cache.previous.edges }, { items: snapshot.edges })
      : new Set<string>();

    return {
      validation,
      simulation,
      aiAnalysis,
      isIncremental: changedNodes.size > 0 || changedEdges.size > 0,
      changedNodeIds: changedNodes,
      changedEdgeIds: changedEdges,
      cacheHit: false,
    };
  }

  /**
   * Check if cached analysis is still valid
   */
  private isCacheValid(currentSnapshot: WorkflowSnapshot): boolean {
    if (!this.cache.current) {
      return false;
    }

    // Check if hash matches (exact same workflow)
    if (this.cache.current.snapshot.hash !== currentSnapshot.hash) {
      return false;
    }

    // Check if cache is too old
    if (Date.now() - this.cache.current.timestamp > this.maxCacheAge) {
      return false;
    }

    return true;
  }

  /**
   * Get cached analysis without recomputing
   */
  public getCached(): CachedAnalysis | null {
    return this.cache.current;
  }

  /**
   * Clear cache manually
   */
  public clearCache(): void {
    this.cache = {
      current: null,
      previous: null,
    };
  }

  /**
   * Get analysis metrics for monitoring
   */
  public getMetrics() {
    return {
      hasCachedAnalysis: this.cache.current !== null,
      cacheAge: this.cache.current ? Date.now() - this.cache.current.timestamp : null,
      previousSnapshot: this.cache.previous
        ? {
            nodeCount: this.cache.previous.nodes.length,
            edgeCount: this.cache.previous.edges.length,
            hash: this.cache.previous.hash,
          }
        : null,
      currentSnapshot: this.cache.current
        ? {
            nodeCount: this.cache.current.snapshot.nodes.length,
            edgeCount: this.cache.current.snapshot.edges.length,
            hash: this.cache.current.snapshot.hash,
          }
        : null,
    };
  }
}

// ============================================================================
// Global Singleton Instance
// ============================================================================

export const analysisEngine = new WorkflowAnalysisEngine();
