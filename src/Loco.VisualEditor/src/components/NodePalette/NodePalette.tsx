import { useState, useCallback, useMemo, memo } from 'react';
import { integrations } from '@/data/integrations';
import { IntegrationCategory, Integration } from '@/types/workflow';
import { Search, ChevronDown, ChevronRight } from 'lucide-react';

const categories: { id: IntegrationCategory; label: string }[] = [
  { id: 'web', label: 'Web & APIs' },
  { id: 'communication', label: 'Communication' },
  { id: 'database', label: 'Database' },
  { id: 'cloud', label: 'Cloud' },
  { id: 'ai', label: 'AI' },
  { id: 'file', label: 'File' },
  { id: 'transform', label: 'Transform' },
];

function NodePaletteComponent() {
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedCategories, setExpandedCategories] = useState<Set<string>>(
    new Set(['web', 'communication', 'database'])
  );

  const toggleCategory = (categoryId: string) => {
    const newExpanded = new Set(expandedCategories);
    if (newExpanded.has(categoryId)) {
      newExpanded.delete(categoryId);
    } else {
      newExpanded.add(categoryId);
    }
    setExpandedCategories(newExpanded);
  };

  // Memoize filtered integrations to avoid unnecessary recomputation
  const filteredIntegrations = useMemo(
    () =>
      integrations.filter((integration) =>
        integration.name.toLowerCase().includes(searchQuery.toLowerCase())
      ),
    [searchQuery]
  );

  // Memoize drag start handler to prevent unnecessary function recreation
  const handleDragStart = useCallback(
    (event: React.DragEvent, integration: Integration) => {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData(
        'application/reactflow',
        JSON.stringify({
          integration: integration.id,
          type: integration.triggers && integration.triggers.length > 0 ? 'trigger' : 'action',
        })
      );
    },
    []
  );

  return (
    <div className="w-80 bg-white border-r border-gray-200 flex flex-col h-full">
      <div className="p-4 border-b border-gray-200">
        <h2 className="text-lg font-semibold text-gray-900 mb-3">Node Palette</h2>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" aria-hidden="true" />
          <input
            type="text"
            placeholder="Search integrations..."
            aria-label="Search available integrations and node types"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
          />
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-4">
        {/* Basic Nodes */}
        <div className="mb-6">
          <h3 className="text-xs font-semibold text-gray-500 uppercase mb-3">Basic Nodes</h3>
          <div className="space-y-2">
            <div
              draggable
              onDragStart={(e) => {
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('application/reactflow', JSON.stringify({
                  type: 'condition',
                  label: 'Condition',
                }));
              }}
              className="p-3 bg-yellow-50 border border-yellow-200 rounded-lg cursor-move hover:shadow-md transition-shadow"
            >
              <div className="flex items-center gap-2">
                <span className="text-xl">🔀</span>
                <div>
                  <div className="font-medium text-sm">Condition</div>
                  <div className="text-xs text-gray-600">Branch workflow</div>
                </div>
              </div>
            </div>

            <div
              draggable
              onDragStart={(e) => {
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('application/reactflow', JSON.stringify({
                  type: 'transform',
                  integration: 'transform',
                  label: 'Transform',
                }));
              }}
              className="p-3 bg-purple-50 border border-purple-200 rounded-lg cursor-move hover:shadow-md transition-shadow"
            >
              <div className="flex items-center gap-2">
                <span className="text-xl">🔄</span>
                <div>
                  <div className="font-medium text-sm">Transform</div>
                  <div className="text-xs text-gray-600">Transform data</div>
                </div>
              </div>
            </div>

            <div
              draggable
              onDragStart={(e) => {
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('application/reactflow', JSON.stringify({
                  type: 'loop',
                  label: 'Loop',
                }));
              }}
              className="p-3 bg-orange-50 border border-orange-200 rounded-lg cursor-move hover:shadow-md transition-shadow"
            >
              <div className="flex items-center gap-2">
                <span className="text-xl">🔁</span>
                <div>
                  <div className="font-medium text-sm">Loop</div>
                  <div className="text-xs text-gray-600">Iterate over items</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Integrations */}
        <div>
          <h3 className="text-xs font-semibold text-gray-500 uppercase mb-3">Integrations</h3>
          {categories.map((category) => {
            const categoryIntegrations = filteredIntegrations.filter(
              (i) => i.category === category.id
            );
            if (categoryIntegrations.length === 0) return null;

            const isExpanded = expandedCategories.has(category.id);

            return (
              <div key={category.id} className="mb-3">
                <button
                  onClick={() => toggleCategory(category.id)}
                  className="flex items-center gap-2 w-full text-left text-sm font-medium text-gray-700 hover:text-gray-900 mb-2"
                >
                  {isExpanded ? (
                    <ChevronDown className="w-4 h-4" />
                  ) : (
                    <ChevronRight className="w-4 h-4" />
                  )}
                  {category.label} ({categoryIntegrations.length})
                </button>

                {isExpanded && (
                  <div className="space-y-2 ml-2">
                    {categoryIntegrations.map((integration) => (
                      <div
                        key={integration.id}
                        draggable
                        onDragStart={(e) => handleDragStart(e, integration)}
                        className="p-3 bg-blue-50 border border-blue-200 rounded-lg cursor-move hover:shadow-md transition-shadow"
                      >
                        <div className="flex items-center gap-2">
                          <span className="text-xl">{integration.icon}</span>
                          <div>
                            <div className="font-medium text-sm">{integration.name}</div>
                            <div className="text-xs text-gray-600 line-clamp-1">
                              {integration.description}
                            </div>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

export const NodePalette = memo(NodePaletteComponent);
NodePalette.displayName = 'NodePalette';
