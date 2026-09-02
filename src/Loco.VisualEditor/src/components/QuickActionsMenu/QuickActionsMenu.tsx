/**
 * Quick Actions Context Menu Component
 *
 * Provides right-click context menu for nodes and canvas with quick actions.
 * Improves productivity by providing common actions without navigating toolbars.
 */

import { useEffect, useRef, useState, memo, useCallback, useMemo } from 'react';
import {
  Copy,
  Trash2,
  Edit,
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
  onClose: () => void;
  onAction: (action: ActionType) => void;
}

/**
 * Actions this menu offers.
 *
 * Three were removed rather than left in place, because nothing backed them:
 *
 *   'run' ("Run from Here") showed "Running workflow from this node..." and
 *   ran nothing. The API executes a whole workflow - POST
 *   /workflows/{id}/execute - and exposes no partial run, so the menu item was
 *   claiming an action that had no way to happen. Worse than an unimplemented
 *   feature, because it reported success.
 *
 *   'group' ("Group Nodes") and 'connect' ("Connect To...") each showed
 *   "feature coming soon". A menu entry is a promise that something will
 *   happen; these were advertising to the user that the product does something
 *   it does not.
 *
 * 'disconnect' was in the same state - "feature coming soon" - but is kept,
 * because the store has had deleteEdge all along and the comment saying
 * otherwise was stale. It now works.
 */
export type ActionType =
  | 'duplicate'
  | 'delete'
  | 'rename'
  | 'disconnect'
  | 'properties'
  | 'info'
  | 'add-trigger'
  | 'add-action'
  | 'add-condition'
  | 'add-transform'
  | 'add-loop'
  | 'add-delay';

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
// Static Menu Items (memoized outside component)
// ============================================================================

const NODE_MENU_ITEMS_BASE: MenuItem[] = [
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
    id: 'delete',
    label: 'Delete',
    icon: <Trash2 className="w-4 h-4" />,
    shortcut: 'Del',
    separator: true,
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

const CANVAS_MENU_ITEMS: MenuItem[] = [
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
  {
    // Delay is a full node type - renderer, property editor, engine handler -
    // and the palette offers it, but this menu did not.
    id: 'add-delay',
    label: 'Add Delay',
    icon: <ChevronRight className="w-4 h-4" />,
  },
];

// ============================================================================
// Quick Actions Menu Component
// ============================================================================

function QuickActionsMenuComponent({
  isOpen,
  position,
  nodeId,
  onClose,
  onAction,
}: QuickActionsMenuProps) {
  const menuRef = useRef<HTMLDivElement>(null);
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(0);

  // The only per-node disabling here was for 'run', which has been removed:
  // it disabled "Run from Here" on condition and loop nodes, which was the one
  // honest part of an item that ran nothing on any node type.
  const menuItems = useMemo(
    () => (nodeId ? NODE_MENU_ITEMS_BASE : CANVAS_MENU_ITEMS),
    [nodeId]
  );

  // Focus management for keyboard navigation
  useEffect(() => {
    if (itemRefs.current[selectedIndex]) {
      itemRefs.current[selectedIndex]?.focus();
    }
  }, [selectedIndex, isOpen]);

  // Memoized keyboard and click outside handlers
  const handleClickOutside = useCallback((event: MouseEvent) => {
    if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
      onClose();
    }
  }, [onClose]);

  const handleKeyDown = useCallback((event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      onClose();
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      setSelectedIndex((prev) => (prev + 1) % menuItems.length);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setSelectedIndex((prev) => (prev - 1 + menuItems.length) % menuItems.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const selectedItem = menuItems[selectedIndex];
      if (selectedItem && !selectedItem.disabled) {
        onAction(selectedItem.id);
        onClose();
      }
    }
  }, [onClose, menuItems, selectedIndex, onAction]);

  // Close menu when clicking outside
  useEffect(() => {
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      document.addEventListener('keydown', handleKeyDown);
      setSelectedIndex(0);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen, handleClickOutside, handleKeyDown]);

  // Memoized position adjustment
  const adjustedPosition = useMemo(() => {
    const adjusted = { ...position };
    const menuHeight = menuItems.length * 40 + 20;
    const menuWidth = 200;

    if (position.x + menuWidth > window.innerWidth) {
      adjusted.x = window.innerWidth - menuWidth - 10;
    }
    if (position.y + menuHeight > window.innerHeight) {
      adjusted.y = window.innerHeight - menuHeight - 10;
    }

    return adjusted;
  }, [position, menuItems.length]);

  // Memoized button click handler
  const handleItemClick = useCallback((item: MenuItem) => {
    if (!item.disabled) {
      onAction(item.id);
      onClose();
    }
  }, [onAction, onClose]);

  if (!isOpen) return null;

  return (
    <div
      ref={menuRef}
      role="menu"
      aria-label={nodeId ? 'Node context menu' : 'Canvas context menu'}
      className="fixed z-50 bg-white rounded-lg shadow-xl border border-gray-200 py-2 min-w-[200px]"
      style={{
        left: `${adjustedPosition.x}px`,
        top: `${adjustedPosition.y}px`,
      }}
    >
      {menuItems.map((item, index) => (
        <div key={item.id}>
          {index > 0 && item.separator && (
            <div className="border-t border-gray-200 my-1" aria-hidden="true" />
          )}
          <button
            ref={(el) => {
              itemRefs.current[index] = el;
            }}
            role="menuitem"
            aria-disabled={item.disabled}
            onClick={() => handleItemClick(item)}
            onMouseEnter={() => setSelectedIndex(index)}
            disabled={item.disabled}
            className={`w-full px-3 py-2 flex items-center justify-between text-sm transition-colors ${
              selectedIndex === index ? 'bg-loco-primary text-white' : ''
            } ${
              item.disabled
                ? 'text-gray-400 cursor-not-allowed'
                : 'text-gray-700 hover:bg-gray-100'
            }`}
          >
            <div className="flex items-center gap-3">
              <span aria-hidden="true">{item.icon}</span>
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

export const QuickActionsMenu = memo(QuickActionsMenuComponent);
QuickActionsMenu.displayName = 'QuickActionsMenu';
