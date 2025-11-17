import { useState, useCallback, useMemo, memo } from 'react';
import { templates, WorkflowTemplate } from '@/data/templates';
import { useWorkflowStore } from '@/store/workflowStore';
import { X, Zap, Database, MessageSquare, Sparkles, Activity } from 'lucide-react';
import { FormInput } from '@/components/Form';

// ============================================================================
// Types
// ============================================================================

interface TemplateGalleryProps {
  isOpen: boolean;
  onClose: () => void;
}

// ============================================================================
// Constants (Memoized - prevent recreation on every render)
// ============================================================================

const CATEGORY_ICONS = {
  communication: MessageSquare,
  automation: Zap,
  data: Database,
  ai: Sparkles,
  monitoring: Activity,
};

const CATEGORY_LABELS = {
  communication: 'Communication',
  automation: 'Automation',
  data: 'Data',
  ai: 'AI',
  monitoring: 'Monitoring',
};

// ============================================================================
// Template Gallery Component
// ============================================================================

function TemplateGalleryComponent({ isOpen, onClose }: TemplateGalleryProps) {
  const { loadWorkflow } = useWorkflowStore();
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);

  if (!isOpen) return null;

  // Memoize filtered templates to prevent recalculation on every render
  const filteredTemplates = useMemo(() => {
    return templates.filter((template) => {
      const matchesSearch =
        template.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        template.description.toLowerCase().includes(searchQuery.toLowerCase());
      const matchesCategory = !selectedCategory || template.category === selectedCategory;
      return matchesSearch && matchesCategory;
    });
  }, [searchQuery, selectedCategory]);

  // Memoize categories extraction to prevent array recreation
  const categories = useMemo(() => {
    return Array.from(new Set(templates.map((t) => t.category)));
  }, []);

  // Memoize template selection handler
  const handleSelectTemplate = useCallback((template: WorkflowTemplate) => {
    // Clone the workflow with new IDs
    const newWorkflow = {
      ...template.workflow,
      id: crypto.randomUUID(),
      name: `${template.workflow.name} (Copy)`,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    loadWorkflow(newWorkflow);
    onClose();
  }, [loadWorkflow, onClose]);

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-5xl h-[80vh] flex flex-col">
        {/* Header */}
        <div className="p-6 border-b border-gray-200">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-2xl font-bold text-gray-900">Workflow Templates</h2>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          {/* Search */}
          <FormInput
            id="template-search"
            type="text"
            placeholder="Search templates..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            description="Search workflow templates by name, category, or description"
          />

          {/* Categories */}
          <div className="flex gap-2 mt-4">
            <button
              onClick={() => setSelectedCategory(null)}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                selectedCategory === null
                  ? 'bg-loco-primary text-white'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
              }`}
            >
              All
            </button>
            {categories.map((category) => {
              const Icon = CATEGORY_ICONS[category];
              return (
                <button
                  key={category}
                  onClick={() => setSelectedCategory(category)}
                  className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                    selectedCategory === category
                      ? 'bg-loco-primary text-white'
                      : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                  }`}
                >
                  <Icon className="w-4 h-4" />
                  {CATEGORY_LABELS[category]}
                </button>
              );
            })}
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6">
          {filteredTemplates.length === 0 ? (
            <div className="text-center py-12">
              <div className="text-gray-400 text-lg mb-2">No templates found</div>
              <div className="text-gray-500 text-sm">
                Try adjusting your search or filters
              </div>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {filteredTemplates.map((template) => {
                const Icon = CATEGORY_ICONS[template.category];
                return (
                  <div
                    key={template.id}
                    onClick={() => handleSelectTemplate(template)}
                    className="p-4 border-2 border-gray-200 rounded-lg hover:border-loco-primary hover:shadow-lg transition-all cursor-pointer group"
                  >
                    <div className="flex items-start gap-3 mb-3">
                      <div className="text-3xl">{template.icon}</div>
                      <div className="flex-1">
                        <h3 className="font-semibold text-gray-900 group-hover:text-loco-primary transition-colors">
                          {template.name}
                        </h3>
                        <div className="flex items-center gap-1 text-xs text-gray-500 mt-1">
                          <Icon className="w-3 h-3" />
                          <span>{CATEGORY_LABELS[template.category]}</span>
                        </div>
                      </div>
                    </div>
                    <p className="text-sm text-gray-600 line-clamp-2">
                      {template.description}
                    </p>
                    <div className="flex items-center gap-4 mt-3 pt-3 border-t border-gray-100 text-xs text-gray-500">
                      <div>{template.workflow.nodes.length} nodes</div>
                      <div>{template.workflow.edges.length} connections</div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-6 border-t border-gray-200 bg-gray-50">
          <div className="flex items-center justify-between">
            <div className="text-sm text-gray-600">
              {filteredTemplates.length} template{filteredTemplates.length !== 1 ? 's' : ''}{' '}
              available
            </div>
            <button
              onClick={onClose}
              className="px-4 py-2 text-gray-700 hover:bg-gray-200 rounded-lg transition-colors"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

export const TemplateGallery = memo(TemplateGalleryComponent);
TemplateGallery.displayName = 'TemplateGallery';
