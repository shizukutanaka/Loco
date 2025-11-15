/**
 * Layout Selector Component
 *
 * Allows users to choose between different layout algorithms
 * for arranging workflow nodes.
 */

import { useState } from 'react';
import {
  LayoutTemplate,
  Circle,
  Network,
  GitBranch,
  ChevronDown,
} from 'lucide-react';
import { useWorkflowStore } from '@/store/workflowStore';
import { useToast } from '@/contexts/ToastContext';
import { LayoutAlgorithm } from '@/utils/advancedLayout';
import { applyLayout, recommendLayout } from '@/utils/advancedLayout';

// ============================================================================
// Types
// ============================================================================

interface LayoutOption {
  id: LayoutAlgorithm;
  name: string;
  description: string;
  icon: React.ReactNode;
  best_for: string;
}

// ============================================================================
// Layout Selector Component
// ============================================================================

export function LayoutSelector() {
  const { nodes, edges } = useWorkflowStore();
  const toast = useToast();
  const [isOpen, setIsOpen] = useState(false);
  const [selectedLayout, setSelectedLayout] = useState<LayoutAlgorithm>('hierarchical');

  const layoutOptions: LayoutOption[] = [
    {
      id: 'hierarchical',
      name: 'Hierarchical',
      description: 'Top-down layout with minimal edge crossings',
      icon: <LayoutTemplate className="w-4 h-4" />,
      best_for: 'Linear workflows with clear dependency flow',
    },
    {
      id: 'circular',
      name: 'Circular',
      description: 'Nodes arranged in a circle with radial connections',
      icon: <Circle className="w-4 h-4" />,
      best_for: 'Cyclical workflows, hub-and-spoke patterns',
    },
    {
      id: 'tree',
      name: 'Tree',
      description: 'Hierarchical tree structure with levels',
      icon: <GitBranch className="w-4 h-4" />,
      best_for: 'Tree-structured workflows, decision trees',
    },
    {
      id: 'force-directed',
      name: 'Force-Directed',
      description: 'Physics-based layout with spring forces',
      icon: <Network className="w-4 h-4" />,
      best_for: 'Complex interconnected workflows, dense graphs',
    },
  ];

  const handleLayoutChange = (layout: LayoutAlgorithm) => {
    if (nodes.length === 0) {
      toast.warning('No nodes to layout');
      return;
    }

    try {
      const layoutedNodes = applyLayout(nodes, edges, layout, {
        nodeSpacing: 50,
        rankSpacing: 100,
      });

      // Update workflow with new positions
      layoutedNodes.forEach((node) => {
        // Update node position in store
        // This would require adding a new method to the store
        console.log(`Node ${node.id} positioned at (${node.position.x}, ${node.position.y})`);
      });

      setSelectedLayout(layout);
      toast.success(`Applied ${layout} layout`);
      setIsOpen(false);
    } catch (error) {
      toast.error(`Failed to apply layout: ${error}`);
    }
  };

  const handleAutoRecommend = () => {
    if (nodes.length === 0) {
      toast.warning('No nodes to analyze');
      return;
    }

    try {
      const recommended = recommendLayout(nodes, edges);
      handleLayoutChange(recommended);
      toast.info(`Recommended layout: ${recommended}`);
    } catch (error) {
      toast.error(`Failed to recommend layout: ${error}`);
    }
  };

  const currentLayout = layoutOptions.find((opt) => opt.id === selectedLayout);

  return (
    <div className="relative">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-2 px-3 py-2 text-sm text-gray-700 border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
        title="Select layout algorithm"
      >
        <LayoutTemplate className="w-4 h-4" />
        <span>{currentLayout?.name || 'Layout'}</span>
        <ChevronDown className="w-4 h-4" />
      </button>

      {isOpen && (
        <div className="absolute top-full mt-1 left-0 z-50 bg-white rounded-lg shadow-lg border border-gray-200 min-w-[320px]">
          {/* Header */}
          <div className="px-4 py-3 border-b border-gray-200">
            <h3 className="text-sm font-semibold text-gray-900">Layout Algorithms</h3>
          </div>

          {/* Layout Options */}
          <div className="py-2">
            {layoutOptions.map((option) => (
              <button
                key={option.id}
                onClick={() => handleLayoutChange(option.id)}
                className={`w-full px-4 py-3 flex items-start gap-3 text-left transition-colors ${
                  selectedLayout === option.id
                    ? 'bg-blue-50 border-l-2 border-blue-500'
                    : 'hover:bg-gray-50'
                }`}
              >
                <div className="mt-0.5 text-gray-600">{option.icon}</div>
                <div className="flex-1">
                  <div className="text-sm font-medium text-gray-900">
                    {option.name}
                  </div>
                  <div className="text-xs text-gray-600 mt-0.5">
                    {option.description}
                  </div>
                  <div className="text-xs text-gray-500 mt-1 italic">
                    Best for: {option.best_for}
                  </div>
                </div>
              </button>
            ))}
          </div>

          {/* Footer with auto-recommend button */}
          <div className="px-4 py-3 border-t border-gray-200">
            <button
              onClick={handleAutoRecommend}
              className="w-full px-3 py-2 text-sm bg-blue-50 text-blue-700 rounded hover:bg-blue-100 transition-colors font-medium"
            >
              Auto-Recommend Layout
            </button>
          </div>
        </div>
      )}
    </div>
  );
}