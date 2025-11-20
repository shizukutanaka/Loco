import { memo, useCallback } from 'react';

interface AppearanceSettingsTabProps {
  theme: 'light' | 'dark' | 'auto';
  gridSize: number;
  showMinimap: boolean;
  onThemeChange: (theme: 'light' | 'dark' | 'auto') => void;
  onGridSizeChange: (size: number) => void;
  onShowMinimapChange: (show: boolean) => void;
}

function AppearanceSettingsTabComponent({
  theme,
  gridSize,
  showMinimap,
  onThemeChange,
  onGridSizeChange,
  onShowMinimapChange,
}: AppearanceSettingsTabProps) {
  const handleThemeChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      onThemeChange(e.target.value as typeof theme);
    },
    [onThemeChange]
  );

  const handleGridSizeChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onGridSizeChange(Number(e.target.value));
    },
    [onGridSizeChange]
  );

  const handleMinimapChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onShowMinimapChange(e.target.checked);
    },
    [onShowMinimapChange]
  );

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Theme</h3>
        
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Color Theme
          </label>
          <select
            value={theme}
            onChange={handleThemeChange}
            className="w-full px-3 py-2 border border-gray-300 rounded-lg"
          >
            <option value="auto">Auto (System)</option>
            <option value="light">Light</option>
            <option value="dark">Dark</option>
          </select>
          <p className="text-xs text-gray-500 mt-1">
            Choose how the interface should appear
          </p>
        </div>
      </div>

      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Canvas</h3>
        
        <div className="mb-4">
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Grid Size: {gridSize}px
          </label>
          <input
            type="range"
            min="5"
            max="50"
            step="5"
            value={gridSize}
            onChange={handleGridSizeChange}
            className="w-full"
          />
          <p className="text-xs text-gray-500 mt-1">
            Snap-to-grid size for node positioning
          </p>
        </div>

        <label className="flex items-center gap-3">
          <input
            type="checkbox"
            checked={showMinimap}
            onChange={handleMinimapChange}
            className="rounded border-gray-300"
          />
          <span className="text-sm font-medium text-gray-700">
            Show minimap
          </span>
        </label>
        <p className="text-xs text-gray-500 mt-1">
          Display minimap in the bottom-right corner of the canvas
        </p>
      </div>
    </div>
  );
}

export const AppearanceSettingsTab = memo(AppearanceSettingsTabComponent);
AppearanceSettingsTab.displayName = 'AppearanceSettingsTab';
