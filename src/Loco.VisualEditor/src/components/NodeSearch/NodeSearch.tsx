import { useState, useEffect, useRef } from 'react';
import { Search, X } from 'lucide-react';
import { integrations } from '@/data/integrations';
import { useWorkflowStore } from '@/store/workflowStore';

interface NodeSearchProps {
  isOpen: boolean;
  onClose: () => void;
}

interface SearchResult {
  id: string;
  name: string;
  description: string;
  category: string;
  icon: string;
  type: 'integration' | 'node' | 'basic';
}

export function NodeSearch({ isOpen, onClose }: NodeSearchProps) {
  const [query, setQuery] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const { addNode } = useWorkflowStore();

  // Build search results
  const searchResults: SearchResult[] = [];

  if (query.trim()) {
    const lowerQuery = query.toLowerCase();

    // Search integrations
    integrations.forEach((integration) => {
      if (
        integration.name.toLowerCase().includes(lowerQuery) ||
        integration.description.toLowerCase().includes(lowerQuery)
      ) {
        searchResults.push({
          id: integration.id,
          name: integration.name,
          description: integration.description,
          category: integration.category,
          icon: integration.icon,
          type: 'integration',
        });
      }
    });

    // Search basic nodes
    const basicNodes = [
      { id: 'condition', name: 'Condition', description: 'Branch workflow based on conditions', icon: '🔀' },
      { id: 'transform', name: 'Transform', description: 'Transform data with C# code', icon: '🔄' },
      { id: 'loop', name: 'Loop', description: 'Iterate over items', icon: '🔁' },
    ];

    basicNodes.forEach((node) => {
      if (
        node.name.toLowerCase().includes(lowerQuery) ||
        node.description.toLowerCase().includes(lowerQuery)
      ) {
        searchResults.push({
          id: node.id,
          name: node.name,
          description: node.description,
          category: 'basic',
          icon: node.icon,
          type: 'basic',
        });
      }
    });
  }

  // Focus input when opened
  useEffect(() => {
    if (isOpen && inputRef.current) {
      inputRef.current.focus();
      setQuery('');
      setSelectedIndex(0);
    }
  }, [isOpen]);

  // Handle keyboard navigation
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (!isOpen) return;

      switch (event.key) {
        case 'ArrowDown':
          event.preventDefault();
          setSelectedIndex((prev) =>
            prev < searchResults.length - 1 ? prev + 1 : prev
          );
          break;
        case 'ArrowUp':
          event.preventDefault();
          setSelectedIndex((prev) => (prev > 0 ? prev - 1 : prev));
          break;
        case 'Enter':
          event.preventDefault();
          if (searchResults[selectedIndex]) {
            handleSelectResult(searchResults[selectedIndex]);
          }
          break;
        case 'Escape':
          event.preventDefault();
          onClose();
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, selectedIndex, searchResults, onClose]);

  const handleSelectResult = (result: SearchResult) => {
    // Add node to center of canvas
    const newNode = {
      id: `node-${Date.now()}`,
      type: result.type === 'basic' ? result.id : 'action',
      position: { x: 400, y: 200 }, // Center position
      data: {
        label: result.name,
        integration: result.type === 'integration' ? result.id : result.id,
        config: {},
        description: result.description,
      },
    };

    addNode(newNode as any);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 bg-black bg-opacity-50 flex items-start justify-center pt-32 z-50"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-xl shadow-2xl w-full max-w-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Search Input */}
        <div className="p-4 border-b border-gray-200">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
            <input
              ref={inputRef}
              type="text"
              placeholder="Search nodes and integrations..."
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setSelectedIndex(0);
              }}
              className="w-full pl-12 pr-12 py-3 text-lg border-none focus:outline-none"
            />
            <button
              onClick={onClose}
              className="absolute right-3 top-1/2 transform -translate-y-1/2 p-1 hover:bg-gray-100 rounded transition-colors"
            >
              <X className="w-5 h-5 text-gray-400" />
            </button>
          </div>
        </div>

        {/* Search Results */}
        <div className="max-h-96 overflow-y-auto">
          {query.trim() === '' ? (
            <div className="p-8 text-center text-gray-500">
              <div className="text-lg mb-2">Start typing to search</div>
              <div className="text-sm">
                Search for integrations, conditions, transforms, and more
              </div>
            </div>
          ) : searchResults.length === 0 ? (
            <div className="p-8 text-center text-gray-500">
              <div className="text-lg mb-2">No results found</div>
              <div className="text-sm">Try a different search term</div>
            </div>
          ) : (
            <div className="p-2">
              {searchResults.map((result, index) => (
                <button
                  key={`${result.type}-${result.id}`}
                  onClick={() => handleSelectResult(result)}
                  className={`w-full p-3 rounded-lg text-left transition-colors ${
                    index === selectedIndex
                      ? 'bg-loco-primary bg-opacity-10 border border-loco-primary'
                      : 'hover:bg-gray-100'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <div className="text-2xl">{result.icon}</div>
                    <div className="flex-1">
                      <div className="font-medium text-gray-900">{result.name}</div>
                      <div className="text-sm text-gray-600 line-clamp-1">
                        {result.description}
                      </div>
                    </div>
                    <div className="text-xs text-gray-500 capitalize">
                      {result.category}
                    </div>
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-3 border-t border-gray-200 bg-gray-50">
          <div className="flex items-center justify-between text-xs text-gray-600">
            <div className="flex items-center gap-4">
              <div>
                <kbd className="px-2 py-1 bg-white border border-gray-300 rounded">↑↓</kbd> Navigate
              </div>
              <div>
                <kbd className="px-2 py-1 bg-white border border-gray-300 rounded">Enter</kbd> Select
              </div>
              <div>
                <kbd className="px-2 py-1 bg-white border border-gray-300 rounded">Esc</kbd> Close
              </div>
            </div>
            <div>{searchResults.length} results</div>
          </div>
        </div>
      </div>
    </div>
  );
}
