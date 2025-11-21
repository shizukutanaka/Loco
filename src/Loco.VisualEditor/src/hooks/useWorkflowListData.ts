import { useState, useEffect, useMemo } from 'react';
import { listWorkflows } from '@/api/workflows';
import { useToast } from '@/contexts/ToastContext';

export interface WorkflowListItem {
  id: string;
  name: string;
  description?: string;
  nodeCount: number;
  edgeCount: number;
  createdAt: string;
  updatedAt: string;
  lastExecutionStatus?: 'completed' | 'failed' | 'running';
  tags?: string[];
}

interface UseWorkflowListDataOptions {
  isOpen: boolean;
  sortBy: 'name' | 'created' | 'updated';
}

/**
 * Custom hook for managing workflow list data and tags
 * Handles: fetching workflows, transforming API responses, extracting tags
 */
export function useWorkflowListData({ isOpen, sortBy }: UseWorkflowListDataOptions) {
  const [workflows, setWorkflows] = useState<WorkflowListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [allTags, setAllTags] = useState<string[]>([]);
  const toast = useToast();

  // Fetch workflows only when modal opens (not on every sort change)
  // Apply sorting client-side using useMemo to avoid unnecessary API calls
  useEffect(() => {
    if (!isOpen) return;

    const fetchWorkflows = async () => {
      setIsLoading(true);
      try {
        // Don't pass sortBy to API - fetch all workflows once and sort client-side
        const response = await listWorkflows({ sortBy: 'updated', sortOrder: 'desc' });

        if (response.success && response.data) {
          // Extract tags during workflow transformation for single-pass optimization
          const tagsSet = new Set<string>();
          const items: WorkflowListItem[] = response.data.workflows.map((w) => {
            const tags = w.metadata?.tags;
            tags?.forEach((tag) => tagsSet.add(tag));

            return {
              id: w.id,
              name: w.name,
              description: w.description,
              nodeCount: w.nodes.length,
              edgeCount: w.edges.length,
              createdAt: w.createdAt,
              updatedAt: w.updatedAt,
              tags,
            };
          });

          setWorkflows(items);

          // Only convert Set to Array and sort if tags have actually changed
          const newTags = Array.from(tagsSet).sort();
          setAllTags((prevTags) => {
            // Compare sorted arrays to avoid unnecessary re-renders
            const tagsChanged =
              newTags.length !== prevTags.length ||
              newTags.some((tag, index) => tag !== prevTags[index]);
            return tagsChanged ? newTags : prevTags;
          });
        }
      } catch (error) {
        console.error('Failed to fetch workflows:', error);
        toast.error('Failed to load workflows');
      } finally {
        setIsLoading(false);
      }
    };

    fetchWorkflows();
  }, [isOpen, toast]);

  // Sort workflows client-side based on sortBy parameter
  // Uses memoization to avoid re-sorting on every render
  const sortedWorkflows = useMemo(() => {
    const sorted = [...workflows];

    switch (sortBy) {
      case 'name':
        sorted.sort((a, b) => a.name.localeCompare(b.name));
        break;
      case 'created':
        sorted.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        break;
      case 'updated':
      default:
        sorted.sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime());
        break;
    }

    return sorted;
  }, [workflows, sortBy]);

  const updateWorkflows = (items: WorkflowListItem[]) => {
    setWorkflows(items);
  };

  return {
    workflows: sortedWorkflows,
    isLoading,
    allTags,
    updateWorkflows,
  };
}
