import { memo, useCallback } from 'react';
import { Eye, EyeOff } from 'lucide-react';

interface ApiSettingsTabProps {
  apiBaseUrl: string;
  apiKey: string;
  showSecrets: boolean;
  onApiBaseUrlChange: (url: string) => void;
  onApiKeyChange: (key: string) => void;
  onShowSecretsChange: () => void;
}

function ApiSettingsTabComponent({
  apiBaseUrl,
  apiKey,
  showSecrets,
  onApiBaseUrlChange,
  onApiKeyChange,
  onShowSecretsChange,
}: ApiSettingsTabProps) {
  const handleBaseUrlChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onApiBaseUrlChange(e.target.value);
    },
    [onApiBaseUrlChange]
  );

  const handleApiKeyChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onApiKeyChange(e.target.value);
    },
    [onApiKeyChange]
  );

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">API Configuration</h3>
        
        <div className="mb-4">
          <label className="block text-sm font-medium text-gray-700 mb-2">
            API Base URL
          </label>
          <input
            type="text"
            value={apiBaseUrl}
            onChange={handleBaseUrlChange}
            placeholder="https://api.example.com"
            className="w-full px-3 py-2 border border-gray-300 rounded-lg"
          />
          <p className="text-xs text-gray-500 mt-1">
            The base URL for all API calls
          </p>
        </div>

        <div>
          <div className="flex items-center justify-between mb-2">
            <label className="block text-sm font-medium text-gray-700">
              API Key
            </label>
            <button
              onClick={onShowSecretsChange}
              className="p-1 hover:bg-gray-100 rounded transition-colors"
              title={showSecrets ? 'Hide' : 'Show'}
            >
              {showSecrets ? (
                <EyeOff className="w-4 h-4 text-gray-500" />
              ) : (
                <Eye className="w-4 h-4 text-gray-500" />
              )}
            </button>
          </div>
          <input
            type={showSecrets ? 'text' : 'password'}
            value={apiKey}
            onChange={handleApiKeyChange}
            placeholder="Your API key"
            className="w-full px-3 py-2 border border-gray-300 rounded-lg font-mono text-sm"
          />
          <p className="text-xs text-gray-500 mt-1">
            Keep this secret - never share your API key
          </p>
        </div>
      </div>

      <div className="p-3 bg-blue-50 rounded-lg">
        <p className="text-xs text-blue-700">
          API settings are stored locally and never sent to external servers.
        </p>
      </div>
    </div>
  );
}

export const ApiSettingsTab = memo(ApiSettingsTabComponent);
ApiSettingsTab.displayName = 'ApiSettingsTab';
