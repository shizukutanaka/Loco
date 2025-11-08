/**
 * Workflow List Component
 *
 * Displays a list of all saved workflows with search, filter, and management capabilities.
 */

import { useState, useEffect } from 'react';
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
} from 'lucide-react';
import { listWorkflows, deleteWorkflow, getWorkflow, createWorkflow, executeWorkflow, workflowToCreateRequest } from '@/api/workflows';
import { useWorkflowStore } from '@/store/workflowStore';
import { useToast } from '@/contexts/ToastContext';
import { useExecutionStore } from '@/store/executionStore';

// ============================================================================
// Types
// ============================================================================

interface WorkflowListProps {
  isOpen: boolean;
  onClose: () => void;
}

interface WorkflowListItem {
  id: string;
  name: string;
  description?: string;
  nodeCount: number;
  edgeCount: number;
  createdAt: string;
  updatedAt: string;
  lastExecutionStatus?: 'completed' | 'failed' | 'running';
}

// ============================================================================
// Workflow List Component
// ============================================================================

export function WorkflowList({ isOpen, onClose }: WorkflowListProps) {
  const [workflows, setWorkflows] = useState<WorkflowListItem[]>([]);
  const [filteredWorkflows, setFilteredWorkflows] = useState<WorkflowListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [filterStatus, setFilterStatus] = useState<'all' | 'completed' | 'failed' | 'running'>('all');
  const [sortBy, setSortBy] = useState<'name' | 'created' | 'updated'>('updated');

  const { newWorkflow, loadWorkflow } = useWorkflowStore();
  const { setCurrentExecution, addToHistory } = useExecutionStore();
  const toast = useToast();

  // Fetch workflows
  useEffect(() => {
    if (!isOpen) return;

    const fetchWorkflows = async () => {
      setIsLoading(true);
      try {
        const response = await listWorkflows({ sortBy, sortOrder: 'desc' });

        if (response.success && response.data) {
          const items: WorkflowListItem[] = response.data.workflows.map((w) => ({
            id: w.id,
            name: w.name,
            description: w.description,
            nodeCount: w.nodes.length,
            edgeCount: w.edges.length,
            createdAt: w.createdAt,
            updatedAt: w.updatedAt,
          }));
          setWorkflows(items);
          setFilteredWorkflows(items);
        }
      } catch (error) {
        console.error('Failed to fetch workflows:', error);
        toast.error('Failed to load workflows');
      } finally {
        setIsLoading(false);
      }
    };

    fetchWorkflows();
  }, [isOpen, sortBy, toast]);

  // Filter workflows
  useEffect(() => {
    let filtered = [...workflows];

    // Search filter
    if (searchQuery) {
      filtered = filtered.filter(
        (w) =>
          w.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          w.description?.toLowerCase().includes(searchQuery.toLowerCase())
      );
    }

    // Status filter
    if (filterStatus !== 'all') {
      filtered = filtered.filter((w) => w.lastExecutionStatus === filterStatus);
    }

    setFilteredWorkflows(filtered);
  }, [workflows, searchQuery, filterStatus]);

  // Delete workflow
  const handleDelete = async (workflowId: string) => {
    if (!confirm('Are you sure you want to delete this workflow?')) return;

    try {
      const response = await deleteWorkflow(workflowId);
      if (response.success) {
        setWorkflows(workflows.filter((w) => w.id !== workflowId));
        toast.success('Workflow deleted successfully');
      } else {
        toast.error(`Failed to delete workflow: ${response.error?.message}`);
      }
    } catch (error) {
      console.error('Failed to delete workflow:', error);
      toast.error('An error occurred while deleting the workflow');
    }
  };

  // Create new workflow
  const handleNew = () => {
    newWorkflow();
    onClose();
    toast.info('New workflow created');
  };

  // Load workflow for editing
  const handleEdit = async (workflowId: string) => {
    try {
      const response = await getWorkflow(workflowId);
      if (response.success && response.data) {
        loadWorkflow(response.data);
        onClose();
        toast.success(`Workflow "${response.data.name}" loaded successfully!`);
      } else {
        toast.error(`Failed to load workflow: ${response.error?.message}`);
      }
    } catch (error) {
      console.error('Failed to load workflow:', error);
      toast.error('An error occurred while loading the workflow');
    }
  };

  // Duplicate workflow
  const handleDuplicate = async (workflowId: string) => {
    try {
      const response = await getWorkflow(workflowId);
      if (response.success && response.data) {
        const workflow = response.data;

        // Create a copy with modified name and new ID
        const duplicatedWorkflow = {
          ...workflow,
          id: crypto.randomUUID(), // Generate new ID
          name: `${workflow.name} (Copy)`,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        };

        // Create the new workflow
        const createResponse = await createWorkflow(workflowToCreateRequest(duplicatedWorkflow));

        if (createResponse.success && createResponse.data) {
          toast.success(`Workflow "${duplicatedWorkflow.name}" created successfully!`);

          // Refresh workflow list
          const listResponse = await listWorkflows({ sortBy, sortOrder: 'desc' });
          if (listResponse.success && listResponse.data) {
            const items: WorkflowListItem[] = listResponse.data.workflows.map((w) => ({
              id: w.id,
              name: w.name,
              description: w.description,
              nodeCount: w.nodes.length,
              edgeCount: w.edges.length,
              createdAt: w.createdAt,
              updatedAt: w.updatedAt,
            }));
            setWorkflows(items);
          }
        } else {
          toast.error(`Failed to duplicate workflow: ${createResponse.error?.message}`);
        }
      } else {
        toast.error(`Failed to load workflow: ${response.error?.message}`);
      }
    } catch (error) {
      console.error('Failed to duplicate workflow:', error);
      toast.error('An error occurred while duplicating the workflow');
    }
  };

  // Run workflow
  const handleRun = async (workflowId: string, workflowName: string) => {
    try {
      const response = await executeWorkflow({
        workflowId,
        input: {},
      });

      if (response.success && response.data) {
        // Add to execution history
        addToHistory({
          executionId: response.data.executionId,
          workflowId,
          workflowName,
          status: response.data.status,
          startedAt: response.data.startedAt,
          completedAt: response.data.completedAt,
        });

        // Open execution panel
        setCurrentExecution(response.data.executionId);
        onClose();

        toast.success(`Workflow "${workflowName}" is running...`);
        toast.info(`Execution ID: ${response.data.executionId}`, 7000);
      } else {
        toast.error(`Failed to run workflow: ${response.error?.message}`);
      }
    } catch (error) {
      console.error('Failed to run workflow:', error);
      toast.error('An error occurred while running the workflow');
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-5xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">My Workflows</h2>
            <p className="text-sm text-gray-500 mt-1">
              {workflows.length} workflow{workflows.length !== 1 ? 's' : ''} total
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            title="Close"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Toolbar */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center gap-4">
          {/* Search */}
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              placeholder="Search workflows..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
            />
          </div>

          {/* Filter */}
          <select
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value as typeof filterStatus)}
            className="px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
          >
            <option value="all">All Status</option>
            <option value="completed">Completed</option>
            <option value="failed">Failed</option>
            <option value="running">Running</option>
          </select>

          {/* Sort */}
          <select
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value as typeof sortBy)}
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
            <div className="flex items-center justify-center py-12">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-loco-primary"></div>
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
                      title="Run"
                    >
                      <Play className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => handleDuplicate(workflow.id)}
                      className="p-2 text-gray-600 hover:bg-gray-100 rounded transition-colors"
                      title="Duplicate"
                    >
                      <Copy className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => handleDelete(workflow.id)}
                      className="p-2 text-red-600 hover:bg-red-50 rounded transition-colors"
                      title="Delete"
                    >
                      <Trash2 className="w-4 h-4" />
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
