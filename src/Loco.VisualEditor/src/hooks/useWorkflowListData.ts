import { useState, useEffect } from 'react';
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

  // Fetch workflows when modal opens or sort changes
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
            tags: w.metadata?.tags,
          }));
          setWorkflows(items);

          // Extract all unique tags
          const tagsSet = new Set<string>();
          items.forEach((item) => {
            item.tags?.forEach((tag) => tagsSet.add(tag));
          });
          setAllTags(Array.from(tagsSet).sort());
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

  const updateWorkflows = (items: WorkflowListItem[]) => {
    setWorkflows(items);
  };

  return {
    workflows,
    isLoading,
    allTags,
    updateWorkflows,
  };
}
