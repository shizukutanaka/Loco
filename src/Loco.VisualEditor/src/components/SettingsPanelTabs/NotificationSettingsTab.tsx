import { memo, useCallback } from 'react';

interface NotificationSettingsTabProps {
  enableNotifications: boolean;
  notifyOnSuccess: boolean;
  notifyOnError: boolean;
  onEnableNotificationsChange: (enabled: boolean) => void;
  onNotifyOnSuccessChange: (notify: boolean) => void;
  onNotifyOnErrorChange: (notify: boolean) => void;
}

function NotificationSettingsTabComponent({
  enableNotifications,
  notifyOnSuccess,
  notifyOnError,
  onEnableNotificationsChange,
  onNotifyOnSuccessChange,
  onNotifyOnErrorChange,
}: NotificationSettingsTabProps) {
  const handleEnableChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onEnableNotificationsChange(e.target.checked);
    },
    [onEnableNotificationsChange]
  );

  const handleSuccessChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onNotifyOnSuccessChange(e.target.checked);
    },
    [onNotifyOnSuccessChange]
  );

  const handleErrorChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onNotifyOnErrorChange(e.target.checked);
    },
    [onNotifyOnErrorChange]
  );

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Notifications</h3>
        
        <label className="flex items-center gap-3 mb-6">
          <input
            type="checkbox"
            checked={enableNotifications}
            onChange={handleEnableChange}
            className="rounded border-gray-300"
          />
          <span className="text-sm font-medium text-gray-700">
            Enable notifications
          </span>
        </label>

        {enableNotifications && (
          <div className="space-y-4">
            <label className="flex items-center gap-3">
              <input
                type="checkbox"
                checked={notifyOnSuccess}
                onChange={handleSuccessChange}
                className="rounded border-gray-300"
              />
              <div>
                <span className="text-sm font-medium text-gray-700">
                  Notify on success
                </span>
                <p className="text-xs text-gray-500 mt-1">
                  Get notified when workflows complete successfully
                </p>
              </div>
            </label>

            <label className="flex items-center gap-3">
              <input
                type="checkbox"
                checked={notifyOnError}
                onChange={handleErrorChange}
                className="rounded border-gray-300"
              />
              <div>
                <span className="text-sm font-medium text-gray-700">
                  Notify on errors
                </span>
                <p className="text-xs text-gray-500 mt-1">
                  Get notified when workflows fail or encounter errors
                </p>
              </div>
            </label>
          </div>
        )}
      </div>

      <div className="p-3 bg-amber-50 rounded-lg">
        <p className="text-xs text-amber-700">
          Notifications require browser permission. You may be prompted to allow notifications.
        </p>
      </div>
    </div>
  );
}

export const NotificationSettingsTab = memo(NotificationSettingsTabComponent);
NotificationSettingsTab.displayName = 'NotificationSettingsTab';
