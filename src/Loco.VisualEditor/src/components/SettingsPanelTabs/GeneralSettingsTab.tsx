import { memo, useCallback } from 'react';

interface GeneralSettingsTabProps {
  enableAutoSave: boolean;
  autoSaveInterval: number;
  showValidationPanel: boolean;
  onAutoSaveChange: (enabled: boolean) => void;
  onAutoSaveIntervalChange: (interval: number) => void;
  onValidationPanelChange: (enabled: boolean) => void;
}

function GeneralSettingsTabComponent({
  enableAutoSave,
  autoSaveInterval,
  showValidationPanel,
  onAutoSaveChange,
  onAutoSaveIntervalChange,
  onValidationPanelChange,
}: GeneralSettingsTabProps) {
  const handleAutoSaveChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onAutoSaveChange(e.target.checked);
    },
    [onAutoSaveChange]
  );

  const handleIntervalChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onAutoSaveIntervalChange(Number(e.target.value));
    },
    [onAutoSaveIntervalChange]
  );

  const handleValidationChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onValidationPanelChange(e.target.checked);
    },
    [onValidationPanelChange]
  );

  return (
    <div className="space-y-6">
      {/* Auto-save Settings */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Auto-Save</h3>
        
        <label className="flex items-center gap-3 mb-4">
          <input
            type="checkbox"
            checked={enableAutoSave}
            onChange={handleAutoSaveChange}
            className="rounded border-gray-300"
          />
          <span className="text-sm font-medium text-gray-700">
            Enable auto-save
          </span>
        </label>

        {enableAutoSave && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Auto-save interval (seconds)
            </label>
            <input
              type="number"
              min="5"
              max="300"
              value={autoSaveInterval}
              onChange={handleIntervalChange}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg"
            />
            <p className="text-xs text-gray-500 mt-1">
              Minimum 5 seconds, Maximum 5 minutes
            </p>
          </div>
        )}
      </div>

      {/* Validation Settings */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Validation</h3>
        
        <label className="flex items-center gap-3">
          <input
            type="checkbox"
            checked={showValidationPanel}
            onChange={handleValidationChange}
            className="rounded border-gray-300"
          />
          <span className="text-sm font-medium text-gray-700">
            Show validation panel
          </span>
        </label>
        <p className="text-xs text-gray-500 mt-2">
          Display validation errors and warnings in the workflow editor
        </p>
      </div>
    </div>
  );
}

export const GeneralSettingsTab = memo(GeneralSettingsTabComponent);
GeneralSettingsTab.displayName = 'GeneralSettingsTab';
