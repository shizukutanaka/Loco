/**
 * Workflow List Component
 *
 * Displays a list of all saved workflows with search, filter, and management capabilities.
 */

import { useRef, useState } from 'react';
import {
  Search,
  Plus,
  Trash2,
  Edit2,
  Play,
  Copy,
  Clock,
  CheckCircle,
  XCircle,
  Filter,
  X,
  Tag,
} from 'lucide-react';
import { useFocusTrap } from '@/hooks/useFocusTrap';
import { SkeletonCard } from '@/components/Skeleton/Skeleton';
import {
  useWorkflowListData,
  useWorkflowListFilters,
  useWorkflowListActions,
} from '@/hooks';

// ============================================================================
// Types
// ============================================================================

interface WorkflowListProps {
  isOpen: boolean;
  onClose: () => void;
}

// ============================================================================
// Workflow List Component
// ============================================================================

export function WorkflowList({ isOpen, onClose }: WorkflowListProps) {
  // Refs for focus management
  const modalRef = useRef<HTMLDivElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  // Manage sortBy state directly to avoid circular dependency
  const [sortBy, setSortBy] = useState<'name' | 'created' | 'updated'>('updated');

  // Apply focus trap to the modal
  useFocusTrap(modalRef, {
    isActive: isOpen,
    onEscape: onClose,
    restoreFocusRef: closeButtonRef,
  });

  // Data hook - fetches workflows and extracts tags
  const { workflows, isLoading, allTags, updateWorkflows } = useWorkflowListData({
    isOpen,
    sortBy,
  });

  // Filters hook - manages all filtering
  const {
    searchQuery,
    setSearchQuery,
    filterStatus,
    setFilterStatus,
    filterTag,
    setFilterTag,
    filteredWorkflows,
  } = useWorkflowListFilters(workflows);

  // Actions hook - provides event handlers
  const { handleNew, handleDelete, handleEdit, handleDuplicate, handleRun } =
    useWorkflowListActions({
      workflows,
      sortBy,
      onUpdateWorkflows: updateWorkflows,
      onClose,
    });

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6" role="presentation">
      <div
        ref={modalRef}
        className="bg-white rounded-xl shadow-2xl max-w-5xl w-full max-h-[90vh] flex flex-col"
        role="dialog"
        aria-modal="true"
        aria-labelledby="workflow-list-title"
      >
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 id="workflow-list-title" className="text-xl font-bold text-gray-900">
              My Workflows
            </h2>
            <p className="text-sm text-gray-500 mt-1">
              {workflows.length} workflow{workflows.length !== 1 ? 's' : ''} total
            </p>
          </div>
          <button
            ref={closeButtonRef}
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            aria-label="Close workflows dialog"
            title="Close"
          >
            <X className="w-5 h-5 text-gray-500" aria-hidden="true" />
          </button>
        </div>

        {/* Toolbar */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center gap-4">
          {/* Search */}
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" aria-hidden="true" />
            <input
              type="text"
              placeholder="Search workflows..."
              aria-label="Search workflows by name, description, or tags"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
            />
          </div>

          {/* Filter */}
          <select
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value as typeof filterStatus)}
            aria-label="Filter workflows by execution status"
            className="px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
          >
            <option value="all">All Status</option>
            <option value="completed">Completed</option>
            <option value="failed">Failed</option>
            <option value="running">Running</option>
          </select>

          {/* Tag Filter */}
          {allTags.length > 0 && (
            <select
              value={filterTag}
              onChange={(e) => setFilterTag(e.target.value)}
              aria-label="Filter workflows by tag"
              className="px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
            >
              <option value="all">All Tags</option>
              {allTags.map((tag) => (
                <option key={tag} value={tag}>
                  {tag}
                </option>
              ))}
            </select>
          )}

          {/* Sort */}
          <select
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value as typeof sortBy)}
            aria-label="Sort workflows"
            className="px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
          >
            <option value="updated">Last Updated</option>
            <option value="created">Date Created</option>
            <option value="name">Name</option>
          </select>

          {/* New Workflow */}
          <button
            onClick={handleNew}
            className="flex items-center gap-2 px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
          >
            <Plus className="w-4 h-4" />
            <span>New</span>
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6">
          {isLoading ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {Array.from({ length: 6 }).map((_, i) => (
                <SkeletonCard key={i} lines={2} className="h-auto" />
              ))}
            </div>
          ) : filteredWorkflows.length === 0 ? (
            <div className="text-center py-12">
              <Filter className="w-12 h-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-600 mb-2">No workflows found</p>
              <p className="text-sm text-gray-500">
                {searchQuery || filterStatus !== 'all'
                  ? 'Try adjusting your search or filters'
                  : 'Create a new workflow to get started'}
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {filteredWorkflows.map((workflow) => (
                <div
                  key={workflow.id}
                  className="border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow"
                >
                  {/* Header */}
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex-1">
                      <h3 className="font-semibold text-gray-900 truncate">
                        {workflow.name}
                      </h3>
                      {workflow.description && (
                        <p className="text-xs text-gray-500 mt-1 line-clamp-2">
                          {workflow.description}
                        </p>
                      )}
                    </div>
                    {workflow.lastExecutionStatus && (
                      <div className="ml-2">
                        {workflow.lastExecutionStatus === 'completed' && (
                          <CheckCircle className="w-5 h-5 text-green-500" />
                        )}
                        {workflow.lastExecutionStatus === 'failed' && (
                          <XCircle className="w-5 h-5 text-red-500" />
                        )}
                        {workflow.lastExecutionStatus === 'running' && (
                          <Clock className="w-5 h-5 text-blue-500 animate-pulse" />
                        )}
                      </div>
                    )}
                  </div>

                  {/* Tags */}
                  {workflow.tags && workflow.tags.length > 0 && (
                    <div className="flex items-center gap-2 mb-2 flex-wrap">
                      {workflow.tags.map((tag) => (
                        <div
                          key={tag}
                          className="inline-flex items-center gap-1 px-2 py-0.5 bg-loco-primary/10 text-loco-primary rounded text-xs"
                        >
                          <Tag className="w-2.5 h-2.5" />
                          <span>{tag}</span>
                        </div>
                      ))}
                    </div>
                  )}

                  {/* Stats */}
                  <div className="flex items-center gap-4 text-xs text-gray-600 mb-3">
                    <span>{workflow.nodeCount} nodes</span>
                    <span>{workflow.edgeCount} connections</span>
                  </div>

                  {/* Metadata */}
                  <div className="text-xs text-gray-500 mb-3">
                    <div>Updated: {new Date(workflow.updatedAt).toLocaleDateString()}</div>
                  </div>

                  {/* Actions */}
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => handleEdit(workflow.id)}
                      className="flex-1 flex items-center justify-center gap-1 px-3 py-2 bg-loco-primary text-white rounded text-xs hover:bg-blue-700 transition-colors"
                      title="Edit"
                    >
                      <Edit2 className="w-3 h-3" />
                      Edit
                    </button>
                    <button
                      onClick={() => handleRun(workflow.id, workflow.name)}
                      className="p-2 text-gray-600 hover:bg-gray-100 rounded transition-colors"
                      aria-label={`Run workflow ${workflow.name}`}
                      title="Run"
                    >
                      <Play className="w-4 h-4" aria-hidden="true" />
                    </button>
                    <button
                      onClick={() => handleDuplicate(workflow.id)}
                      className="p-2 text-gray-600 hover:bg-gray-100 rounded transition-colors"
                      aria-label={`Duplicate workflow ${workflow.name}`}
                      title="Duplicate"
                    >
                      <Copy className="w-4 h-4" aria-hidden="true" />
                    </button>
                    <button
                      onClick={() => handleDelete(workflow.id)}
                      className="p-2 text-red-600 hover:bg-red-50 rounded transition-colors"
                      aria-label={`Delete workflow ${workflow.name}`}
                      title="Delete"
                    >
                      <Trash2 className="w-4 h-4" aria-hidden="true" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
