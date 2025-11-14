/**
 * Keyboard Shortcuts & Help Panel Component
 *
 * Provides comprehensive keyboard shortcuts reference and help:
 * - Categorized shortcuts list
 * - Search functionality
 * - Feature descriptions
 * - Quick access guide
 */

import { useState } from 'react';
import {
  X,
  Search,
  Keyboard,
  Command,
  Save,
  Play,
  Download,
  Upload,
  Copy,
  Settings,
  LayoutTemplate,
  List,
  Calendar,
  Globe,
  GitCommit,
  History,
  BarChart3,
  Users,
  Package,
  CheckCircle,
  Info,
} from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface KeyboardShortcutsProps {
  isOpen: boolean;
  onClose: () => void;
}

type ShortcutCategory = 'file' | 'navigation' | 'execution' | 'tools' | 'view';

interface Shortcut {
  id: string;
  category: ShortcutCategory;
  keys: string[];
  description: string;
  icon?: React.ReactNode;
}

// ============================================================================
// Keyboard Shortcuts Component
// ============================================================================

export function KeyboardShortcuts({ isOpen, onClose }: KeyboardShortcutsProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<ShortcutCategory | 'all'>('all');

  const shortcuts: Shortcut[] = [
    // File Operations
    {
      id: 'new',
      category: 'file',
      keys: ['Ctrl', 'N'],
      description: 'Create new workflow',
      icon: <Command className="w-4 h-4" />,
    },
    {
      id: 'save',
      category: 'file',
      keys: ['Ctrl', 'S'],
      description: 'Save current workflow',
      icon: <Save className="w-4 h-4" />,
    },
    {
      id: 'undo',
      category: 'file',
      keys: ['Ctrl', 'Z'],
      description: 'Undo last action',
      icon: <History className="w-4 h-4" />,
    },
    {
      id: 'redo',
      category: 'file',
      keys: ['Ctrl', 'Y'],
      description: 'Redo last action',
      icon: <History className="w-4 h-4" />,
    },
    {
      id: 'import',
      category: 'file',
      keys: ['Ctrl', 'O'],
      description: 'Import workflow from JSON',
      icon: <Upload className="w-4 h-4" />,
    },
    {
      id: 'export',
      category: 'file',
      keys: ['Ctrl', 'E'],
      description: 'Export workflow to JSON',
      icon: <Download className="w-4 h-4" />,
    },
    {
      id: 'duplicate',
      category: 'file',
      keys: ['Ctrl', 'D'],
      description: 'Duplicate selected node',
      icon: <Copy className="w-4 h-4" />,
    },

    // Navigation
    {
      id: 'workflows',
      category: 'navigation',
      keys: ['Ctrl', 'K'],
      description: 'Open My Workflows list',
      icon: <List className="w-4 h-4" />,
    },
    {
      id: 'templates',
      category: 'navigation',
      keys: ['Ctrl', 'T'],
      description: 'Browse template gallery',
      icon: <LayoutTemplate className="w-4 h-4" />,
    },
    {
      id: 'settings',
      category: 'navigation',
      keys: ['Ctrl', ','],
      description: 'Open settings panel',
      icon: <Settings className="w-4 h-4" />,
    },
    {
      id: 'help',
      category: 'navigation',
      keys: ['?'],
      description: 'Show this help panel',
      icon: <Keyboard className="w-4 h-4" />,
    },
    {
      id: 'help-alt',
      category: 'navigation',
      keys: ['Ctrl', '/'],
      description: 'Show this help panel (alternative)',
      icon: <Keyboard className="w-4 h-4" />,
    },

    // Execution
    {
      id: 'run',
      category: 'execution',
      keys: ['Ctrl', 'Enter'],
      description: 'Run current workflow',
      icon: <Play className="w-4 h-4" />,
    },
    {
      id: 'test',
      category: 'execution',
      keys: ['Ctrl', 'Shift', 'T'],
      description: 'Test & validate workflow',
      icon: <CheckCircle className="w-4 h-4" />,
    },

    // Tools
    {
      id: 'schedules',
      category: 'tools',
      keys: ['Ctrl', 'Shift', 'S'],
      description: 'Manage schedules',
      icon: <Calendar className="w-4 h-4" />,
    },
    {
      id: 'webhooks',
      category: 'tools',
      keys: ['Ctrl', 'Shift', 'W'],
      description: 'Manage webhooks',
      icon: <Globe className="w-4 h-4" />,
    },
    {
      id: 'metrics',
      category: 'tools',
      keys: ['Ctrl', 'Shift', 'M'],
      description: 'View metrics dashboard',
      icon: <BarChart3 className="w-4 h-4" />,
    },
    {
      id: 'collaborate',
      category: 'tools',
      keys: ['Ctrl', 'Shift', 'C'],
      description: 'Open collaboration panel',
      icon: <Users className="w-4 h-4" />,
    },
    {
      id: 'plugins',
      category: 'tools',
      keys: ['Ctrl', 'Shift', 'P'],
      description: 'Browse plugin marketplace',
      icon: <Package className="w-4 h-4" />,
    },

    // Version Control
    {
      id: 'commit',
      category: 'view',
      keys: ['Ctrl', 'Shift', 'K'],
      description: 'Commit workflow changes',
      icon: <GitCommit className="w-4 h-4" />,
    },
    {
      id: 'history',
      category: 'view',
      keys: ['Ctrl', 'H'],
      description: 'View version history',
      icon: <History className="w-4 h-4" />,
    },

    // View
    {
      id: 'zoom-in',
      category: 'view',
      keys: ['Ctrl', '+'],
      description: 'Zoom in canvas',
    },
    {
      id: 'zoom-out',
      category: 'view',
      keys: ['Ctrl', '-'],
      description: 'Zoom out canvas',
    },
    {
      id: 'zoom-reset',
      category: 'view',
      keys: ['Ctrl', '0'],
      description: 'Reset canvas zoom',
    },
    {
      id: 'fit-view',
      category: 'view',
      keys: ['Ctrl', 'Shift', 'F'],
      description: 'Fit workflow to view',
    },
  ];

  const categories = [
    { id: 'all' as const, name: 'All Shortcuts', icon: <Keyboard className="w-4 h-4" /> },
    { id: 'file' as const, name: 'File Operations', icon: <Save className="w-4 h-4" /> },
    { id: 'navigation' as const, name: 'Navigation', icon: <List className="w-4 h-4" /> },
    { id: 'execution' as const, name: 'Execution', icon: <Play className="w-4 h-4" /> },
    { id: 'tools' as const, name: 'Tools', icon: <Settings className="w-4 h-4" /> },
    { id: 'view' as const, name: 'View', icon: <Command className="w-4 h-4" /> },
  ];

  const filteredShortcuts = shortcuts.filter((shortcut) => {
    const matchesSearch =
      shortcut.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
      shortcut.keys.some((key) => key.toLowerCase().includes(searchQuery.toLowerCase()));
    const matchesCategory = selectedCategory === 'all' || shortcut.category === selectedCategory;
    return matchesSearch && matchesCategory;
  });

  const getKeyDisplay = (key: string) => {
    // Replace Ctrl with Cmd on Mac
    const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
    if (isMac && key === 'Ctrl') return '⌘';
    if (isMac && key === 'Alt') return '⌥';
    if (isMac && key === 'Shift') return '⇧';
    return key;
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-4xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900 flex items-center gap-2">
              <Keyboard className="w-6 h-6" />
              Keyboard Shortcuts & Help
            </h2>
            <p className="text-sm text-gray-500 mt-1">Master Loco with keyboard shortcuts</p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            title="Close"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Search and Categories */}
        <div className="px-6 py-4 border-b border-gray-200 space-y-4">
          {/* Search */}
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search shortcuts..."
              className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
            />
          </div>

          {/* Category Tabs */}
          <div className="flex gap-2 overflow-x-auto pb-2">
            {categories.map((category) => (
              <button
                key={category.id}
                onClick={() => setSelectedCategory(category.id)}
                className={`flex items-center gap-2 px-4 py-2 rounded-lg whitespace-nowrap transition-colors ${
                  selectedCategory === category.id
                    ? 'bg-loco-primary text-white'
                    : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                }`}
              >
                {category.icon}
                <span className="text-sm font-medium">{category.name}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Shortcuts List */}
        <div className="flex-1 overflow-y-auto p-6">
          {filteredShortcuts.length === 0 ? (
            <div className="text-center py-12">
              <Keyboard className="w-16 h-16 text-gray-300 mx-auto mb-4" />
              <p className="text-gray-500">No shortcuts found</p>
            </div>
          ) : (
            <div className="space-y-2">
              {filteredShortcuts.map((shortcut) => (
                <div
                  key={shortcut.id}
                  className="flex items-center justify-between p-4 bg-white border border-gray-200 rounded-lg hover:shadow-sm transition-shadow"
                >
                  <div className="flex items-center gap-3">
                    {shortcut.icon && (
                      <div className="w-8 h-8 bg-blue-50 rounded-lg flex items-center justify-center text-blue-600">
                        {shortcut.icon}
                      </div>
                    )}
                    <p className="text-sm text-gray-900">{shortcut.description}</p>
                  </div>
                  <div className="flex items-center gap-1">
                    {shortcut.keys.map((key, index) => (
                      <span key={index} className="flex items-center">
                        <kbd className="px-3 py-1.5 text-sm font-semibold text-gray-700 bg-gray-100 border border-gray-300 rounded-lg shadow-sm">
                          {getKeyDisplay(key)}
                        </kbd>
                        {index < shortcut.keys.length - 1 && (
                          <span className="mx-1 text-gray-400">+</span>
                        )}
                      </span>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50">
          <div className="flex items-start gap-3">
            <Info className="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" />
            <div className="text-sm text-gray-700">
              <p className="font-semibold mb-1">Pro Tip</p>
              <p>
                Press <kbd className="px-2 py-0.5 text-xs bg-gray-100 border border-gray-300 rounded">?</kbd> or{' '}
                <kbd className="px-2 py-0.5 text-xs bg-gray-100 border border-gray-300 rounded">Ctrl</kbd>{' '}
                <kbd className="px-2 py-0.5 text-xs bg-gray-100 border border-gray-300 rounded">/</kbd>{' '}
                anytime to view this help panel. Use keyboard shortcuts to boost your productivity!
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
