/**
 * Settings Panel Component
 *
 * Provides application settings, preferences, and configuration options.
 * Includes API keys, environment variables, and user preferences.
 */

import { useState } from 'react';
import {
  Settings,
  Key,
  Globe,
  Palette,
  Bell,
  Shield,
  Save,
  X,
  Eye,
  EyeOff,
  Plus,
  Trash2,
} from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface SettingsPanelProps {
  isOpen: boolean;
  onClose: () => void;
}

interface EnvironmentVariable {
  key: string;
  value: string;
  isSecret: boolean;
}

type SettingsTab = 'general' | 'api' | 'environment' | 'appearance' | 'notifications';

// ============================================================================
// Settings Panel Component
// ============================================================================

export function SettingsPanel({ isOpen, onClose }: SettingsPanelProps) {
  const [activeTab, setActiveTab] = useState<SettingsTab>('general');
  const [envVars, setEnvVars] = useState<EnvironmentVariable[]>([
    { key: 'API_BASE_URL', value: 'http://localhost:5000', isSecret: false },
  ]);
  const [newEnvKey, setNewEnvKey] = useState('');
  const [newEnvValue, setNewEnvValue] = useState('');
  const [showSecrets, setShowSecrets] = useState(false);

  // General settings
  const [autoSaveInterval, setAutoSaveInterval] = useState(30);
  const [enableAutoSave, setEnableAutoSave] = useState(true);
  const [showValidationPanel, setShowValidationPanel] = useState(true);

  // API settings
  const [apiKey, setApiKey] = useState('');
  const [apiBaseUrl, setApiBaseUrl] = useState('http://localhost:5000');

  // Appearance settings
  const [theme, setTheme] = useState<'light' | 'dark' | 'system'>('light');
  const [gridSize, setGridSize] = useState(15);
  const [showMinimap, setShowMinimap] = useState(true);

  // Notification settings
  const [enableNotifications, setEnableNotifications] = useState(true);
  const [notifyOnSuccess, setNotifyOnSuccess] = useState(true);
  const [notifyOnError, setNotifyOnError] = useState(true);

  const toast = useToast();

  // Add environment variable
  const handleAddEnvVar = () => {
    if (!newEnvKey || !newEnvValue) {
      toast.warning('Please enter both key and value');
      return;
    }

    if (envVars.some((v) => v.key === newEnvKey)) {
      toast.error('Environment variable already exists');
      return;
    }

    setEnvVars([...envVars, { key: newEnvKey, value: newEnvValue, isSecret: false }]);
    setNewEnvKey('');
    setNewEnvValue('');
    toast.success('Environment variable added');
  };

  // Remove environment variable
  const handleRemoveEnvVar = (key: string) => {
    setEnvVars(envVars.filter((v) => v.key !== key));
    toast.success('Environment variable removed');
  };

  // Save settings
  const handleSave = () => {
    // Save to localStorage
    const settings = {
      general: { autoSaveInterval, enableAutoSave, showValidationPanel },
      api: { apiKey, apiBaseUrl },
      appearance: { theme, gridSize, showMinimap },
      notifications: { enableNotifications, notifyOnSuccess, notifyOnError },
      environment: envVars,
    };

    localStorage.setItem('loco_settings', JSON.stringify(settings));
    toast.success('Settings saved successfully');
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-4xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Settings className="w-6 h-6 text-gray-700" />
            <h2 className="text-xl font-bold text-gray-900">Settings</h2>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            title="Close"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        <div className="flex-1 flex overflow-hidden">
          {/* Tabs Sidebar */}
          <div className="w-64 border-r border-gray-200 p-4">
            <nav className="space-y-1">
              <button
                onClick={() => setActiveTab('general')}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                  activeTab === 'general'
                    ? 'bg-loco-primary text-white'
                    : 'text-gray-700 hover:bg-gray-100'
                }`}
              >
                <Settings className="w-5 h-5" />
                <span className="font-medium">General</span>
              </button>

              <button
                onClick={() => setActiveTab('api')}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                  activeTab === 'api'
                    ? 'bg-loco-primary text-white'
                    : 'text-gray-700 hover:bg-gray-100'
                }`}
              >
                <Key className="w-5 h-5" />
                <span className="font-medium">API</span>
              </button>

              <button
                onClick={() => setActiveTab('environment')}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                  activeTab === 'environment'
                    ? 'bg-loco-primary text-white'
                    : 'text-gray-700 hover:bg-gray-100'
                }`}
              >
                <Globe className="w-5 h-5" />
                <span className="font-medium">Environment</span>
              </button>

              <button
                onClick={() => setActiveTab('appearance')}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                  activeTab === 'appearance'
                    ? 'bg-loco-primary text-white'
                    : 'text-gray-700 hover:bg-gray-100'
                }`}
              >
                <Palette className="w-5 h-5" />
                <span className="font-medium">Appearance</span>
              </button>

              <button
                onClick={() => setActiveTab('notifications')}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                  activeTab === 'notifications'
                    ? 'bg-loco-primary text-white'
                    : 'text-gray-700 hover:bg-gray-100'
                }`}
              >
                <Bell className="w-5 h-5" />
                <span className="font-medium">Notifications</span>
              </button>
            </nav>
          </div>

          {/* Content */}
          <div className="flex-1 overflow-y-auto p-6">
            {/* General Settings */}
            {activeTab === 'general' && (
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">General Settings</h3>

                  <div className="space-y-4">
                    {/* Auto-save */}
                    <div className="flex items-center justify-between">
                      <div>
                        <label className="text-sm font-medium text-gray-700">Enable Auto-save</label>
                        <p className="text-xs text-gray-500 mt-1">
                          Automatically save workflow drafts
                        </p>
                      </div>
                      <label className="relative inline-flex items-center cursor-pointer">
                        <input
                          type="checkbox"
                          checked={enableAutoSave}
                          onChange={(e) => setEnableAutoSave(e.target.checked)}
                          className="sr-only peer"
                        />
                        <div className="w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-blue-300 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-loco-primary"></div>
                      </label>
                    </div>

                    {/* Auto-save interval */}
                    {enableAutoSave && (
                      <div>
                        <label className="block text-sm font-medium text-gray-700 mb-2">
                          Auto-save Interval (seconds)
                        </label>
                        <input
                          type="number"
                          value={autoSaveInterval}
                          onChange={(e) => setAutoSaveInterval(Number(e.target.value))}
                          min="10"
                          max="300"
                          className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                        />
                      </div>
                    )}

                    {/* Show validation panel */}
                    <div className="flex items-center justify-between">
                      <div>
                        <label className="text-sm font-medium text-gray-700">Show Validation Panel</label>
                        <p className="text-xs text-gray-500 mt-1">
                          Display validation errors and warnings
                        </p>
                      </div>
                      <label className="relative inline-flex items-center cursor-pointer">
                        <input
                          type="checkbox"
                          checked={showValidationPanel}
                          onChange={(e) => setShowValidationPanel(e.target.checked)}
                          className="sr-only peer"
                        />
                        <div className="w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-blue-300 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-loco-primary"></div>
                      </label>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* API Settings */}
            {activeTab === 'api' && (
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">API Settings</h3>

                  <div className="space-y-4">
                    {/* API Base URL */}
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">
                        API Base URL
                      </label>
                      <input
                        type="url"
                        value={apiBaseUrl}
                        onChange={(e) => setApiBaseUrl(e.target.value)}
                        placeholder="http://localhost:5000"
                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                      />
                      <p className="text-xs text-gray-500 mt-1">
                        Base URL for the Loco API server
                      </p>
                    </div>

                    {/* API Key */}
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">
                        API Key (Optional)
                      </label>
                      <div className="relative">
                        <input
                          type={showSecrets ? 'text' : 'password'}
                          value={apiKey}
                          onChange={(e) => setApiKey(e.target.value)}
                          placeholder="Enter your API key"
                          className="w-full px-3 py-2 pr-10 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                        />
                        <button
                          onClick={() => setShowSecrets(!showSecrets)}
                          className="absolute right-2 top-1/2 transform -translate-y-1/2 p-1 hover:bg-gray-100 rounded"
                        >
                          {showSecrets ? (
                            <EyeOff className="w-4 h-4 text-gray-500" />
                          ) : (
                            <Eye className="w-4 h-4 text-gray-500" />
                          )}
                        </button>
                      </div>
                      <p className="text-xs text-gray-500 mt-1">
                        API key for authenticated requests
                      </p>
                    </div>

                    {/* Security Notice */}
                    <div className="flex items-start gap-2 p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
                      <Shield className="w-5 h-5 text-yellow-600 flex-shrink-0 mt-0.5" />
                      <div className="flex-1">
                        <p className="text-sm font-medium text-yellow-900">Security Notice</p>
                        <p className="text-xs text-yellow-700 mt-1">
                          API keys are stored in browser localStorage. Never commit API keys to version control.
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Environment Variables */}
            {activeTab === 'environment' && (
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Environment Variables</h3>

                  {/* Add New Variable */}
                  <div className="mb-6 p-4 bg-gray-50 rounded-lg">
                    <h4 className="text-sm font-semibold text-gray-700 mb-3">Add New Variable</h4>
                    <div className="flex gap-2">
                      <input
                        type="text"
                        value={newEnvKey}
                        onChange={(e) => setNewEnvKey(e.target.value)}
                        placeholder="KEY"
                        className="flex-1 px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                      />
                      <input
                        type="text"
                        value={newEnvValue}
                        onChange={(e) => setNewEnvValue(e.target.value)}
                        placeholder="value"
                        className="flex-1 px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                      />
                      <button
                        onClick={handleAddEnvVar}
                        className="flex items-center gap-2 px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
                      >
                        <Plus className="w-4 h-4" />
                        Add
                      </button>
                    </div>
                  </div>

                  {/* Variables List */}
                  <div className="space-y-2">
                    {envVars.map((envVar) => (
                      <div
                        key={envVar.key}
                        className="flex items-center gap-3 p-3 bg-white border border-gray-200 rounded-lg"
                      >
                        <div className="flex-1">
                          <p className="text-sm font-mono font-medium text-gray-900">{envVar.key}</p>
                          <p className="text-xs font-mono text-gray-600 mt-1">
                            {envVar.isSecret && !showSecrets ? '••••••••' : envVar.value}
                          </p>
                        </div>
                        <button
                          onClick={() => handleRemoveEnvVar(envVar.key)}
                          className="p-2 text-red-600 hover:bg-red-50 rounded transition-colors"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    ))}

                    {envVars.length === 0 && (
                      <p className="text-center text-gray-500 py-8">No environment variables configured</p>
                    )}
                  </div>
                </div>
              </div>
            )}

            {/* Appearance Settings */}
            {activeTab === 'appearance' && (
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Appearance Settings</h3>

                  <div className="space-y-4">
                    {/* Theme */}
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">
                        Theme
                      </label>
                      <select
                        value={theme}
                        onChange={(e) => setTheme(e.target.value as typeof theme)}
                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                      >
                        <option value="light">Light</option>
                        <option value="dark">Dark</option>
                        <option value="system">System</option>
                      </select>
                    </div>

                    {/* Grid Size */}
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-2">
                        Grid Size ({gridSize}px)
                      </label>
                      <input
                        type="range"
                        min="10"
                        max="30"
                        step="5"
                        value={gridSize}
                        onChange={(e) => setGridSize(Number(e.target.value))}
                        className="w-full"
                      />
                    </div>

                    {/* Show Minimap */}
                    <div className="flex items-center justify-between">
                      <div>
                        <label className="text-sm font-medium text-gray-700">Show Minimap</label>
                        <p className="text-xs text-gray-500 mt-1">
                          Display workflow minimap
                        </p>
                      </div>
                      <label className="relative inline-flex items-center cursor-pointer">
                        <input
                          type="checkbox"
                          checked={showMinimap}
                          onChange={(e) => setShowMinimap(e.target.checked)}
                          className="sr-only peer"
                        />
                        <div className="w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-blue-300 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-loco-primary"></div>
                      </label>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Notification Settings */}
            {activeTab === 'notifications' && (
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-4">Notification Settings</h3>

                  <div className="space-y-4">
                    {/* Enable Notifications */}
                    <div className="flex items-center justify-between">
                      <div>
                        <label className="text-sm font-medium text-gray-700">Enable Notifications</label>
                        <p className="text-xs text-gray-500 mt-1">
                          Show toast notifications
                        </p>
                      </div>
                      <label className="relative inline-flex items-center cursor-pointer">
                        <input
                          type="checkbox"
                          checked={enableNotifications}
                          onChange={(e) => setEnableNotifications(e.target.checked)}
                          className="sr-only peer"
                        />
                        <div className="w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-blue-300 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-loco-primary"></div>
                      </label>
                    </div>

                    {enableNotifications && (
                      <>
                        {/* Notify on Success */}
                        <div className="flex items-center justify-between pl-6">
                          <div>
                            <label className="text-sm font-medium text-gray-700">Notify on Success</label>
                            <p className="text-xs text-gray-500 mt-1">
                              Show notifications for successful operations
                            </p>
                          </div>
                          <label className="relative inline-flex items-center cursor-pointer">
                            <input
                              type="checkbox"
                              checked={notifyOnSuccess}
                              onChange={(e) => setNotifyOnSuccess(e.target.checked)}
                              className="sr-only peer"
                            />
                            <div className="w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-blue-300 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-loco-primary"></div>
                          </label>
                        </div>

                        {/* Notify on Error */}
                        <div className="flex items-center justify-between pl-6">
                          <div>
                            <label className="text-sm font-medium text-gray-700">Notify on Error</label>
                            <p className="text-xs text-gray-500 mt-1">
                              Show notifications for errors
                            </p>
                          </div>
                          <label className="relative inline-flex items-center cursor-pointer">
                            <input
                              type="checkbox"
                              checked={notifyOnError}
                              onChange={(e) => setNotifyOnError(e.target.checked)}
                              className="sr-only peer"
                            />
                            <div className="w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-blue-300 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-loco-primary"></div>
                          </label>
                        </div>
                      </>
                    )}
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 flex items-center justify-between">
          <button
            onClick={onClose}
            className="px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            className="flex items-center gap-2 px-6 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
          >
            <Save className="w-4 h-4" />
            Save Settings
          </button>
        </div>
      </div>
    </div>
  );
}
