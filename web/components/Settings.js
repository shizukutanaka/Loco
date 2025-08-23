import { useState } from 'react';
import { FiSave, FiRefreshCw, FiDatabase, FiGlobe, FiShield, FiBell } from 'react-icons/fi';
import toast from 'react-hot-toast';

export default function Settings() {
  const [settings, setSettings] = useState({
    general: {
      language: 'en',
      timezone: 'UTC',
      autoStart: true,
      minimizeToTray: false,
    },
    execution: {
      maxConcurrent: 5,
      timeout: 30000,
      retryAttempts: 3,
      logLevel: 'info',
    },
    database: {
      type: 'sqlite',
      path: './data/loco.db',
      backupEnabled: true,
      backupInterval: 'daily',
    },
    notifications: {
      emailEnabled: false,
      email: '',
      discordEnabled: false,
      discordWebhook: '',
    },
  });

  const handleSave = () => {
    console.log('Saving settings:', settings);
    toast.success('Settings saved successfully');
  };

  const handleReset = () => {
    toast.success('Settings reset to defaults');
  };

  const SettingSection = ({ icon: Icon, title, children }) => (
    <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 mb-6">
      <div className="flex items-center mb-4">
        <Icon className="mr-3 text-blue-500" size={24} />
        <h3 className="text-xl font-semibold text-gray-800 dark:text-white">{title}</h3>
      </div>
      {children}
    </div>
  );

  return (
    <div>
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-3xl font-bold text-gray-800 dark:text-white">Settings</h2>
        <div className="flex gap-3">
          <button
            onClick={handleReset}
            className="flex items-center px-4 py-2 bg-gray-500 text-white rounded-lg hover:bg-gray-600 transition-colors"
          >
            <FiRefreshCw className="mr-2" />
            Reset
          </button>
          <button
            onClick={handleSave}
            className="flex items-center px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors"
          >
            <FiSave className="mr-2" />
            Save Changes
          </button>
        </div>
      </div>

      <SettingSection icon={FiGlobe} title="General">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Language
            </label>
            <select
              value={settings.general.language}
              onChange={(e) => setSettings({
                ...settings,
                general: { ...settings.general, language: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="en">English</option>
              <option value="ja">Japanese</option>
              <option value="es">Spanish</option>
              <option value="fr">French</option>
              <option value="de">German</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Timezone
            </label>
            <select
              value={settings.general.timezone}
              onChange={(e) => setSettings({
                ...settings,
                general: { ...settings.general, timezone: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="UTC">UTC</option>
              <option value="America/New_York">Eastern Time</option>
              <option value="America/Los_Angeles">Pacific Time</option>
              <option value="Europe/London">London</option>
              <option value="Asia/Tokyo">Tokyo</option>
            </select>
          </div>
          <div className="flex items-center">
            <input
              type="checkbox"
              checked={settings.general.autoStart}
              onChange={(e) => setSettings({
                ...settings,
                general: { ...settings.general, autoStart: e.target.checked }
              })}
              className="mr-2"
            />
            <label className="text-sm text-gray-700 dark:text-gray-300">
              Start with system
            </label>
          </div>
          <div className="flex items-center">
            <input
              type="checkbox"
              checked={settings.general.minimizeToTray}
              onChange={(e) => setSettings({
                ...settings,
                general: { ...settings.general, minimizeToTray: e.target.checked }
              })}
              className="mr-2"
            />
            <label className="text-sm text-gray-700 dark:text-gray-300">
              Minimize to system tray
            </label>
          </div>
        </div>
      </SettingSection>

      <SettingSection icon={FiShield} title="Execution">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Max Concurrent Flows
            </label>
            <input
              type="number"
              value={settings.execution.maxConcurrent}
              onChange={(e) => setSettings({
                ...settings,
                execution: { ...settings.execution, maxConcurrent: parseInt(e.target.value) }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Timeout (ms)
            </label>
            <input
              type="number"
              value={settings.execution.timeout}
              onChange={(e) => setSettings({
                ...settings,
                execution: { ...settings.execution, timeout: parseInt(e.target.value) }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Retry Attempts
            </label>
            <input
              type="number"
              value={settings.execution.retryAttempts}
              onChange={(e) => setSettings({
                ...settings,
                execution: { ...settings.execution, retryAttempts: parseInt(e.target.value) }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Log Level
            </label>
            <select
              value={settings.execution.logLevel}
              onChange={(e) => setSettings({
                ...settings,
                execution: { ...settings.execution, logLevel: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="error">Error</option>
              <option value="warn">Warning</option>
              <option value="info">Info</option>
              <option value="debug">Debug</option>
            </select>
          </div>
        </div>
      </SettingSection>

      <SettingSection icon={FiDatabase} title="Database">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Database Type
            </label>
            <select
              value={settings.database.type}
              onChange={(e) => setSettings({
                ...settings,
                database: { ...settings.database, type: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="sqlite">SQLite</option>
              <option value="postgresql">PostgreSQL</option>
              <option value="mysql">MySQL</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Database Path
            </label>
            <input
              type="text"
              value={settings.database.path}
              onChange={(e) => setSettings({
                ...settings,
                database: { ...settings.database, path: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
          <div className="flex items-center">
            <input
              type="checkbox"
              checked={settings.database.backupEnabled}
              onChange={(e) => setSettings({
                ...settings,
                database: { ...settings.database, backupEnabled: e.target.checked }
              })}
              className="mr-2"
            />
            <label className="text-sm text-gray-700 dark:text-gray-300">
              Enable automatic backups
            </label>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Backup Interval
            </label>
            <select
              value={settings.database.backupInterval}
              onChange={(e) => setSettings({
                ...settings,
                database: { ...settings.database, backupInterval: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="hourly">Hourly</option>
              <option value="daily">Daily</option>
              <option value="weekly">Weekly</option>
              <option value="monthly">Monthly</option>
            </select>
          </div>
        </div>
      </SettingSection>

      <SettingSection icon={FiBell} title="Notifications">
        <div className="space-y-4">
          <div>
            <div className="flex items-center mb-2">
              <input
                type="checkbox"
                checked={settings.notifications.emailEnabled}
                onChange={(e) => setSettings({
                  ...settings,
                  notifications: { ...settings.notifications, emailEnabled: e.target.checked }
                })}
                className="mr-2"
              />
              <label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                Email Notifications
              </label>
            </div>
            {settings.notifications.emailEnabled && (
              <input
                type="email"
                placeholder="Enter email address"
                value={settings.notifications.email}
                onChange={(e) => setSettings({
                  ...settings,
                  notifications: { ...settings.notifications, email: e.target.value }
                })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              />
            )}
          </div>
          <div>
            <div className="flex items-center mb-2">
              <input
                type="checkbox"
                checked={settings.notifications.discordEnabled}
                onChange={(e) => setSettings({
                  ...settings,
                  notifications: { ...settings.notifications, discordEnabled: e.target.checked }
                })}
                className="mr-2"
              />
              <label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                Discord Notifications
              </label>
            </div>
            {settings.notifications.discordEnabled && (
              <input
                type="text"
                placeholder="Enter Discord webhook URL"
                value={settings.notifications.discordWebhook}
                onChange={(e) => setSettings({
                  ...settings,
                  notifications: { ...settings.notifications, discordWebhook: e.target.value }
                })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              />
            )}
          </div>
        </div>
      </SettingSection>
    </div>
  );
}
