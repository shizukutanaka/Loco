/**
 * Quick Actions Context Menu Component
 *
 * Provides right-click context menu for nodes and canvas with quick actions.
 * Improves productivity by providing common actions without navigating toolbars.
 */

import { useEffect, useRef } from 'react';
import {
  Copy,
  Trash2,
  Edit,
  PlayCircle,
  Layers,
  Link,
  Unlink,
  Settings,
  Info,
  ChevronRight,
} from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface QuickActionsMenuProps {
  isOpen: boolean;
  position: { x: number; y: number };
  nodeId?: string | null;
  nodeType?: string | null;
  onClose: () => void;
  onAction: (action: ActionType) => void;
}

export type ActionType =
  | 'duplicate'
  | 'delete'
  | 'rename'
  | 'run'
  | 'group'
  | 'connect'
  | 'disconnect'
  | 'properties'
  | 'info'
  | 'add-trigger'
  | 'add-action'
  | 'add-condition'
  | 'add-transform'
  | 'add-loop';

interface MenuItem {
  id: ActionType;
  label: string;
  icon: React.ReactNode;
  shortcut?: string;
  separator?: boolean;
  disabled?: boolean;
  submenu?: MenuItem[];
}

// ============================================================================
// Quick Actions Menu Component
// ============================================================================

export function QuickActionsMenu({
  isOpen,
  position,
  nodeId,
  nodeType,
  onClose,
  onAction,
}: QuickActionsMenuProps) {
  const menuRef = useRef<HTMLDivElement>(null);

  // Close menu when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        onClose();
      }
    };

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      document.addEventListener('keydown', handleEscape);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isOpen, onClose]);

  // Menu items for nodes
  const nodeMenuItems: MenuItem[] = [
    {
      id: 'duplicate',
      label: 'Duplicate',
      icon: <Copy className="w-4 h-4" />,
      shortcut: 'Ctrl+D',
    },
    {
      id: 'rename',
      label: 'Rename',
      icon: <Edit className="w-4 h-4" />,
      shortcut: 'F2',
    },
    {
      id: 'run',
      label: 'Run from Here',
      icon: <PlayCircle className="w-4 h-4" />,
      disabled: nodeType === 'condition' || nodeType === 'loop',
    },
    {
      id: 'delete',
      label: 'Delete',
      icon: <Trash2 className="w-4 h-4" />,
      shortcut: 'Del',
      separator: true,
    },
    {
      id: 'group',
      label: 'Group Nodes',
      icon: <Layers className="w-4 h-4" />,
      shortcut: 'Ctrl+G',
    },
    {
      id: 'connect',
      label: 'Connect To...',
      icon: <Link className="w-4 h-4" />,
    },
    {
      id: 'disconnect',
      label: 'Disconnect',
      icon: <Unlink className="w-4 h-4" />,
      separator: true,
    },
    {
      id: 'properties',
      label: 'Properties',
      icon: <Settings className="w-4 h-4" />,
      shortcut: 'Alt+Enter',
    },
    {
      id: 'info',
      label: 'Node Info',
      icon: <Info className="w-4 h-4" />,
    },
  ];

  // Menu items for canvas (when no node is selected)
  const canvasMenuItems: MenuItem[] = [
    {
      id: 'add-trigger',
      label: 'Add Trigger',
      icon: <ChevronRight className="w-4 h-4" />,
    },
    {
      id: 'add-action',
      label: 'Add Action',
      icon: <ChevronRight className="w-4 h-4" />,
    },
    {
      id: 'add-condition',
      label: 'Add Condition',
      icon: <ChevronRight className="w-4 h-4" />,
    },
    {
      id: 'add-transform',
      label: 'Add Transform',
      icon: <ChevronRight className="w-4 h-4" />,
    },
    {
      id: 'add-loop',
      label: 'Add Loop',
      icon: <ChevronRight className="w-4 h-4" />,
    },
  ];

  const menuItems = nodeId ? nodeMenuItems : canvasMenuItems;

  if (!isOpen) return null;

  // Calculate position to keep menu within viewport
  const adjustedPosition = { ...position };
  const menuHeight = menuItems.length * 40 + 20; // Approximate height
  const menuWidth = 200;

  if (position.x + menuWidth > window.innerWidth) {
    adjustedPosition.x = window.innerWidth - menuWidth - 10;
  }
  if (position.y + menuHeight > window.innerHeight) {
    adjustedPosition.y = window.innerHeight - menuHeight - 10;
  }

  return (
    <div
      ref={menuRef}
      className="fixed z-50 bg-white rounded-lg shadow-xl border border-gray-200 py-2 min-w-[200px]"
      style={{
        left: `${adjustedPosition.x}px`,
        top: `${adjustedPosition.y}px`,
      }}
    >
      {menuItems.map((item, index) => (
        <div key={item.id}>
          {index > 0 && item.separator && (
            <div className="border-t border-gray-200 my-1" />
          )}
          <button
            onClick={() => {
              if (!item.disabled) {
                onAction(item.id);
                onClose();
              }
            }}
            disabled={item.disabled}
            className={`w-full px-3 py-2 flex items-center justify-between text-sm transition-colors ${
              item.disabled
                ? 'text-gray-400 cursor-not-allowed'
                : 'text-gray-700 hover:bg-gray-100'
            }`}
          >
            <div className="flex items-center gap-3">
              {item.icon}
              <span>{item.label}</span>
            </div>
            {item.shortcut && (
              <span className="text-xs text-gray-400">{item.shortcut}</span>
            )}
          </button>
        </div>
      ))}
    </div>
  );
}