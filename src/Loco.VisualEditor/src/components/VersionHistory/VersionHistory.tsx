/**
 * Version History Component
 *
 * Displays workflow version history with Git integration.
 * Allows viewing commits, comparing versions, and rolling back changes.
 */

import { useState, useEffect } from 'react';
import { X, GitBranch, Clock, User, RotateCcw, Eye, ChevronDown, ChevronRight } from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface VersionHistoryProps {
  workflowId: string;
  workflowName: string;
  isOpen: boolean;
  onClose: () => void;
  onRestore?: (versionId: string) => void;
}

export interface WorkflowVersion {
  id: string;
  commitHash: string;
  message: string;
  author: string;
  timestamp: string;
  changes: {
    nodesAdded: number;
    nodesRemoved: number;
    nodesModified: number;
    edgesAdded: number;
    edgesRemoved: number;
  };
  workflowSnapshot?: {
    name: string;
    nodes: any[];
    edges: any[];
    metadata?: any;
  };
}

// ============================================================================
// Version History Component
// ============================================================================

export function VersionHistory({
  workflowId,
  workflowName,
  isOpen,
  onClose,
  onRestore,
}: VersionHistoryProps) {
  const [versions, setVersions] = useState<WorkflowVersion[]>([]);
  const [expandedVersions, setExpandedVersions] = useState<Set<string>>(new Set());
  const [isLoading, setIsLoading] = useState(false);
  const toast = useToast();

  // Fetch version history on mount
  useEffect(() => {
    if (!isOpen) return;

    const fetchVersions = async () => {
      setIsLoading(true);
      try {
        // TODO: Replace with actual API call
        // const response = await getWorkflowVersions(workflowId);

        // Mock data for demonstration
        await new Promise((resolve) => setTimeout(resolve, 500));
        setVersions([
          {
            id: 'v1',
            commitHash: 'a1b2c3d',
            message: 'Add email notification step',
            author: 'John Doe',
            timestamp: new Date(Date.now() - 3600000).toISOString(),
            changes: {
              nodesAdded: 1,
              nodesRemoved: 0,
              nodesModified: 0,
              edgesAdded: 2,
              edgesRemoved: 0,
            },
          },
          {
            id: 'v2',
            commitHash: 'e4f5g6h',
            message: 'Update HTTP request configuration',
            author: 'Jane Smith',
            timestamp: new Date(Date.now() - 7200000).toISOString(),
            changes: {
              nodesAdded: 0,
              nodesRemoved: 0,
              nodesModified: 1,
              edgesAdded: 0,
              edgesRemoved: 0,
            },
          },
          {
            id: 'v3',
            commitHash: 'i7j8k9l',
            message: 'Remove deprecated validation step',
            author: 'John Doe',
            timestamp: new Date(Date.now() - 86400000).toISOString(),
            changes: {
              nodesAdded: 0,
              nodesRemoved: 1,
              nodesModified: 0,
              edgesAdded: 0,
              edgesRemoved: 2,
            },
          },
          {
            id: 'v4',
            commitHash: 'm0n1o2p',
            message: 'Initial workflow creation',
            author: 'John Doe',
            timestamp: new Date(Date.now() - 172800000).toISOString(),
            changes: {
              nodesAdded: 3,
              nodesRemoved: 0,
              nodesModified: 0,
              edgesAdded: 2,
              edgesRemoved: 0,
            },
          },
        ]);
      } catch (error) {
        console.error('Failed to fetch versions:', error);
        toast.error('Failed to load version history');
      } finally {
        setIsLoading(false);
      }
    };

    fetchVersions();
  }, [isOpen, workflowId, toast]);

  const toggleVersionExpand = (versionId: string) => {
    const newExpanded = new Set(expandedVersions);
    if (newExpanded.has(versionId)) {
      newExpanded.delete(versionId);
    } else {
      newExpanded.add(versionId);
    }
    setExpandedVersions(newExpanded);
  };

  const handleRestore = async (version: WorkflowVersion) => {
    const confirmed = confirm(
      `Restore workflow to version "${version.message}"?\n\nThis will replace the current workflow with this version. Current changes will be lost unless you commit them first.`
    );

    if (!confirmed) return;

    try {
      // TODO: Call API to restore version
      // const response = await restoreWorkflowVersion(workflowId, version.id);

      toast.success(`Workflow restored to version: ${version.message}`);

      if (onRestore) {
        onRestore(version.id);
      }

      onClose();
    } catch (error) {
      console.error('Failed to restore version:', error);
      toast.error('Failed to restore version');
    }
  };

  const handleViewVersion = (version: WorkflowVersion) => {
    toast.info(`Version preview coming soon: ${version.message}`);
  };

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
    if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
    if (diffDays < 7) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
    return date.toLocaleDateString();
  };

  const getChangeSummary = (changes: WorkflowVersion['changes']) => {
    const parts = [];
    if (changes.nodesAdded > 0) parts.push(`+${changes.nodesAdded} node${changes.nodesAdded > 1 ? 's' : ''}`);
    if (changes.nodesRemoved > 0) parts.push(`-${changes.nodesRemoved} node${changes.nodesRemoved > 1 ? 's' : ''}`);
    if (changes.nodesModified > 0) parts.push(`~${changes.nodesModified} modified`);
    if (changes.edgesAdded > 0) parts.push(`+${changes.edgesAdded} edge${changes.edgesAdded > 1 ? 's' : ''}`);
    if (changes.edgesRemoved > 0) parts.push(`-${changes.edgesRemoved} edge${changes.edgesRemoved > 1 ? 's' : ''}`);
    return parts.length > 0 ? parts.join(', ') : 'No changes';
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-4xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Version History</h2>
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
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-loco-primary"></div>
            </div>
          ) : versions.length === 0 ? (
            <div className="text-center py-12">
              <GitBranch className="w-12 h-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-600 mb-2">No version history found</p>
              <p className="text-sm text-gray-500">
                Commit your workflow changes to start tracking versions
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              {/* Timeline */}
              <div className="relative">
                {/* Vertical line */}
                <div className="absolute left-[15px] top-0 bottom-0 w-0.5 bg-gray-200"></div>

                {versions.map((version, index) => (
                  <div key={version.id} className="relative pl-12 pb-8 last:pb-0">
                    {/* Timeline dot */}
                    <div className="absolute left-0 top-2 w-8 h-8 bg-loco-primary rounded-full flex items-center justify-center">
                      <GitBranch className="w-4 h-4 text-white" />
                    </div>

                    {/* Version card */}
                    <div className="bg-white border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow">
                      <div className="flex items-start justify-between mb-3">
                        <div className="flex-1">
                          <div className="flex items-center gap-2 mb-2">
                            <h3 className="font-semibold text-gray-900">{version.message}</h3>
                            {index === 0 && (
                              <span className="px-2 py-0.5 bg-green-100 text-green-700 rounded text-xs font-medium">
                                Latest
                              </span>
                            )}
                          </div>

                          <div className="flex items-center gap-4 text-xs text-gray-600">
                            <div className="flex items-center gap-1">
                              <User className="w-3 h-3" />
                              <span>{version.author}</span>
                            </div>
                            <div className="flex items-center gap-1">
                              <Clock className="w-3 h-3" />
                              <span>{formatTimestamp(version.timestamp)}</span>
                            </div>
                            <div className="flex items-center gap-1">
                              <code className="bg-gray-100 px-1.5 py-0.5 rounded text-gray-700">
                                {version.commitHash}
                              </code>
                            </div>
                          </div>
                        </div>

                        <div className="flex items-center gap-2 ml-4">
                          <button
                            onClick={() => handleViewVersion(version)}
                            className="p-2 text-gray-600 hover:bg-gray-100 rounded transition-colors"
                            title="View version"
                          >
                            <Eye className="w-4 h-4" />
                          </button>
                          {index !== 0 && (
                            <button
                              onClick={() => handleRestore(version)}
                              className="p-2 text-blue-600 hover:bg-blue-50 rounded transition-colors"
                              title="Restore this version"
                            >
                              <RotateCcw className="w-4 h-4" />
                            </button>
                          )}
                          <button
                            onClick={() => toggleVersionExpand(version.id)}
                            className="p-2 text-gray-600 hover:bg-gray-100 rounded transition-colors"
                            title="Show details"
                          >
                            {expandedVersions.has(version.id) ? (
                              <ChevronDown className="w-4 h-4" />
                            ) : (
                              <ChevronRight className="w-4 h-4" />
                            )}
                          </button>
                        </div>
                      </div>

                      {/* Change summary */}
                      <div className="text-xs text-gray-600 bg-gray-50 px-3 py-2 rounded">
                        {getChangeSummary(version.changes)}
                      </div>

                      {/* Expanded details */}
                      {expandedVersions.has(version.id) && (
                        <div className="mt-3 pt-3 border-t border-gray-200">
                          <div className="grid grid-cols-2 gap-3 text-sm">
                            <div>
                              <h4 className="font-medium text-gray-900 mb-2">Nodes</h4>
                              <div className="space-y-1 text-xs">
                                {version.changes.nodesAdded > 0 && (
                                  <div className="flex items-center gap-2 text-green-700">
                                    <span className="font-medium">+{version.changes.nodesAdded}</span>
                                    <span>Added</span>
                                  </div>
                                )}
                                {version.changes.nodesModified > 0 && (
                                  <div className="flex items-center gap-2 text-blue-700">
                                    <span className="font-medium">~{version.changes.nodesModified}</span>
                                    <span>Modified</span>
                                  </div>
                                )}
                                {version.changes.nodesRemoved > 0 && (
                                  <div className="flex items-center gap-2 text-red-700">
                                    <span className="font-medium">-{version.changes.nodesRemoved}</span>
                                    <span>Removed</span>
                                  </div>
                                )}
                              </div>
                            </div>
                            <div>
                              <h4 className="font-medium text-gray-900 mb-2">Edges</h4>
                              <div className="space-y-1 text-xs">
                                {version.changes.edgesAdded > 0 && (
                                  <div className="flex items-center gap-2 text-green-700">
                                    <span className="font-medium">+{version.changes.edgesAdded}</span>
                                    <span>Added</span>
                                  </div>
                                )}
                                {version.changes.edgesRemoved > 0 && (
                                  <div className="flex items-center gap-2 text-red-700">
                                    <span className="font-medium">-{version.changes.edgesRemoved}</span>
                                    <span>Removed</span>
                                  </div>
                                )}
                              </div>
                            </div>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50">
          <div className="flex items-center justify-between text-sm">
            <div className="text-gray-600">
              {versions.length} version{versions.length !== 1 ? 's' : ''} in history
            </div>
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
  );
}
