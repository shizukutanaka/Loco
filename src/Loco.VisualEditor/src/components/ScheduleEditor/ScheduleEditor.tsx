/**
 * Schedule Editor Component
 *
 * Provides a user-friendly interface for creating workflow schedules with cron expressions.
 * Supports common patterns (hourly, daily, weekly, monthly) and custom cron expressions.
 */

import { useState } from 'react';
import { Calendar, Clock, X } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface ScheduleEditorProps {
  workflowId: string;
  workflowName: string;
  isOpen: boolean;
  onClose: () => void;
  onSave: (schedule: WorkflowSchedule) => void;
  existingSchedule?: WorkflowSchedule;
}

export interface WorkflowSchedule {
  id?: string;
  workflowId: string;
  cronExpression: string;
  timezone: string;
  enabled: boolean;
  description?: string;
  nextRun?: string;
}

type SchedulePreset = 'every-minute' | 'hourly' | 'daily' | 'weekly' | 'monthly' | 'custom';

// ============================================================================
// Preset Cron Expressions
// ============================================================================

const CRON_PRESETS: Record<SchedulePreset, string> = {
  'every-minute': '* * * * *',
  'hourly': '0 * * * *',
  'daily': '0 9 * * *', // 9 AM daily
  'weekly': '0 9 * * 1', // 9 AM every Monday
  'monthly': '0 9 1 * *', // 9 AM on 1st of month
  'custom': '',
};

const PRESET_LABELS: Record<SchedulePreset, string> = {
  'every-minute': 'Every Minute',
  'hourly': 'Every Hour',
  'daily': 'Daily',
  'weekly': 'Weekly',
  'monthly': 'Monthly',
  'custom': 'Custom Cron',
};

// Common timezones
const TIMEZONES = [
  'UTC',
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'Europe/London',
  'Europe/Paris',
  'Europe/Berlin',
  'Asia/Tokyo',
  'Asia/Shanghai',
  'Asia/Singapore',
  'Australia/Sydney',
];

// ============================================================================
// Schedule Editor Component
// ============================================================================

export function ScheduleEditor({
  workflowId,
  workflowName,
  isOpen,
  onClose,
  onSave,
  existingSchedule,
}: ScheduleEditorProps) {
  const [preset, setPreset] = useState<SchedulePreset>(
    existingSchedule ? 'custom' : 'daily'
  );
  const [customCron, setCustomCron] = useState(
    existingSchedule?.cronExpression || CRON_PRESETS.daily
  );
  const [timezone, setTimezone] = useState(
    existingSchedule?.timezone || 'UTC'
  );
  const [enabled, setEnabled] = useState(
    existingSchedule?.enabled ?? true
  );
  const [description, setDescription] = useState(
    existingSchedule?.description || ''
  );

  // Daily options
  const [dailyHour, setDailyHour] = useState(9);
  const [dailyMinute, setDailyMinute] = useState(0);

  // Weekly options
  const [weeklyDay, setWeeklyDay] = useState(1); // Monday
  const [weeklyHour, setWeeklyHour] = useState(9);
  const [weeklyMinute, setWeeklyMinute] = useState(0);

  // Monthly options
  const [monthlyDay, setMonthlyDay] = useState(1);
  const [monthlyHour, setMonthlyHour] = useState(9);
  const [monthlyMinute, setMonthlyMinute] = useState(0);

  if (!isOpen) return null;

  const handlePresetChange = (newPreset: SchedulePreset) => {
    setPreset(newPreset);
    if (newPreset !== 'custom') {
      setCustomCron(CRON_PRESETS[newPreset]);
    }
  };

  const handleSave = () => {
    let finalCron = '';

    switch (preset) {
      case 'every-minute':
      case 'hourly':
        finalCron = CRON_PRESETS[preset];
        break;
      case 'daily':
        finalCron = `${dailyMinute} ${dailyHour} * * *`;
        break;
      case 'weekly':
        finalCron = `${weeklyMinute} ${weeklyHour} * * ${weeklyDay}`;
        break;
      case 'monthly':
        finalCron = `${monthlyMinute} ${monthlyHour} ${monthlyDay} * *`;
        break;
      case 'custom':
        finalCron = customCron;
        break;
    }

    const schedule: WorkflowSchedule = {
      id: existingSchedule?.id,
      workflowId,
      cronExpression: finalCron,
      timezone,
      enabled,
      description: description.trim() || undefined,
    };

    onSave(schedule);
    onClose();
  };

  const getCronDescription = (cron: string): string => {
    const parts = cron.split(' ');
    if (parts.length !== 5) return 'Invalid cron expression';

    const [minute, hour, dayOfMonth, month, dayOfWeek] = parts;

    if (cron === '* * * * *') return 'Every minute';
    if (cron === '0 * * * *') return 'Every hour';
    if (minute !== '*' && hour !== '*' && dayOfMonth === '*' && month === '*' && dayOfWeek === '*') {
      return `Daily at ${hour.padStart(2, '0')}:${minute.padStart(2, '0')}`;
    }
    if (minute !== '*' && hour !== '*' && dayOfMonth === '*' && month === '*' && dayOfWeek !== '*') {
      const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
      return `Weekly on ${days[parseInt(dayOfWeek)]} at ${hour.padStart(2, '0')}:${minute.padStart(2, '0')}`;
    }
    if (minute !== '*' && hour !== '*' && dayOfMonth !== '*' && month === '*' && dayOfWeek === '*') {
      return `Monthly on day ${dayOfMonth} at ${hour.padStart(2, '0')}:${minute.padStart(2, '0')}`;
    }

    return `Cron: ${cron}`;
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between sticky top-0 bg-white">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Schedule Workflow</h2>
            <p className="text-sm text-gray-500 mt-1">{workflowName}</p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            title="Close"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-6">
          {/* Enable/Disable Toggle */}
          <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
            <div>
              <h3 className="font-semibold text-gray-900">Schedule Enabled</h3>
              <p className="text-sm text-gray-600 mt-1">
                {enabled ? 'Workflow will run automatically' : 'Schedule is paused'}
              </p>
            </div>
            <button
              onClick={() => setEnabled(!enabled)}
              className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                enabled ? 'bg-loco-primary' : 'bg-gray-300'
              }`}
            >
              <span
                className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                  enabled ? 'translate-x-6' : 'translate-x-1'
                }`}
              />
            </button>
          </div>

          {/* Schedule Preset Selector */}
          <div>
            <label className="block text-sm font-semibold text-gray-900 mb-3">
              Schedule Type
            </label>
            <div className="grid grid-cols-3 gap-2">
              {(Object.keys(CRON_PRESETS) as SchedulePreset[]).map((presetKey) => (
                <button
                  key={presetKey}
                  onClick={() => handlePresetChange(presetKey)}
                  className={`px-4 py-3 rounded-lg border-2 text-sm font-medium transition-colors ${
                    preset === presetKey
                      ? 'border-loco-primary bg-loco-primary/10 text-loco-primary'
                      : 'border-gray-200 hover:border-gray-300 text-gray-700'
                  }`}
                >
                  {PRESET_LABELS[presetKey]}
                </button>
              ))}
            </div>
          </div>

          {/* Schedule Configuration */}
          <div className="space-y-4">
            {preset === 'daily' && (
              <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                <div className="flex items-center gap-2 mb-3">
                  <Clock className="w-4 h-4 text-blue-600" />
                  <h4 className="font-semibold text-gray-900">Daily Schedule</h4>
                </div>
                <div className="flex items-center gap-4">
                  <div className="flex-1">
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Hour (0-23)
                    </label>
                    <input
                      type="number"
                      min="0"
                      max="23"
                      value={dailyHour}
                      onChange={(e) => setDailyHour(parseInt(e.target.value) || 0)}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                    />
                  </div>
                  <div className="flex-1">
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Minute (0-59)
                    </label>
                    <input
                      type="number"
                      min="0"
                      max="59"
                      value={dailyMinute}
                      onChange={(e) => setDailyMinute(parseInt(e.target.value) || 0)}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                    />
                  </div>
                </div>
                <p className="text-xs text-gray-600 mt-2">
                  Runs at {dailyHour.toString().padStart(2, '0')}:{dailyMinute.toString().padStart(2, '0')} every day
                </p>
              </div>
            )}

            {preset === 'weekly' && (
              <div className="p-4 bg-purple-50 border border-purple-200 rounded-lg">
                <div className="flex items-center gap-2 mb-3">
                  <Calendar className="w-4 h-4 text-purple-600" />
                  <h4 className="font-semibold text-gray-900">Weekly Schedule</h4>
                </div>
                <div className="space-y-3">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Day of Week
                    </label>
                    <select
                      value={weeklyDay}
                      onChange={(e) => setWeeklyDay(parseInt(e.target.value))}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                    >
                      <option value="0">Sunday</option>
                      <option value="1">Monday</option>
                      <option value="2">Tuesday</option>
                      <option value="3">Wednesday</option>
                      <option value="4">Thursday</option>
                      <option value="5">Friday</option>
                      <option value="6">Saturday</option>
                    </select>
                  </div>
                  <div className="flex items-center gap-4">
                    <div className="flex-1">
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        Hour (0-23)
                      </label>
                      <input
                        type="number"
                        min="0"
                        max="23"
                        value={weeklyHour}
                        onChange={(e) => setWeeklyHour(parseInt(e.target.value) || 0)}
                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                      />
                    </div>
                    <div className="flex-1">
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        Minute (0-59)
                      </label>
                      <input
                        type="number"
                        min="0"
                        max="59"
                        value={weeklyMinute}
                        onChange={(e) => setWeeklyMinute(parseInt(e.target.value) || 0)}
                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                      />
                    </div>
                  </div>
                </div>
              </div>
            )}

            {preset === 'monthly' && (
              <div className="p-4 bg-green-50 border border-green-200 rounded-lg">
                <div className="flex items-center gap-2 mb-3">
                  <Calendar className="w-4 h-4 text-green-600" />
                  <h4 className="font-semibold text-gray-900">Monthly Schedule</h4>
                </div>
                <div className="space-y-3">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Day of Month (1-31)
                    </label>
                    <input
                      type="number"
                      min="1"
                      max="31"
                      value={monthlyDay}
                      onChange={(e) => setMonthlyDay(parseInt(e.target.value) || 1)}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                    />
                  </div>
                  <div className="flex items-center gap-4">
                    <div className="flex-1">
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        Hour (0-23)
                      </label>
                      <input
                        type="number"
                        min="0"
                        max="23"
                        value={monthlyHour}
                        onChange={(e) => setMonthlyHour(parseInt(e.target.value) || 0)}
                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                      />
                    </div>
                    <div className="flex-1">
                      <label className="block text-xs font-medium text-gray-700 mb-1">
                        Minute (0-59)
                      </label>
                      <input
                        type="number"
                        min="0"
                        max="59"
                        value={monthlyMinute}
                        onChange={(e) => setMonthlyMinute(parseInt(e.target.value) || 0)}
                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
                      />
                    </div>
                  </div>
                </div>
              </div>
            )}

            {preset === 'custom' && (
              <div className="p-4 bg-gray-50 border border-gray-200 rounded-lg">
                <label className="block text-sm font-semibold text-gray-900 mb-2">
                  Custom Cron Expression
                </label>
                <input
                  type="text"
                  value={customCron}
                  onChange={(e) => setCustomCron(e.target.value)}
                  placeholder="* * * * *"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary font-mono text-sm"
                />
                <p className="text-xs text-gray-500 mt-2">
                  Format: minute hour day-of-month month day-of-week
                </p>
                <p className="text-xs text-gray-600 mt-1 font-medium">
                  {getCronDescription(customCron)}
                </p>
              </div>
            )}
          </div>

          {/* Timezone Selector */}
          <div>
            <label className="block text-sm font-semibold text-gray-900 mb-2">
              Timezone
            </label>
            <select
              value={timezone}
              onChange={(e) => setTimezone(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary"
            >
              {TIMEZONES.map((tz) => (
                <option key={tz} value={tz}>
                  {tz}
                </option>
              ))}
            </select>
          </div>

          {/* Description (Optional) */}
          <div>
            <label className="block text-sm font-semibold text-gray-900 mb-2">
              Description (Optional)
            </label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="e.g., Daily report generation"
              rows={2}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary resize-none"
            />
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 flex items-center justify-end gap-3">
          <button
            onClick={onClose}
            className="px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            className="px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
          >
            {existingSchedule ? 'Update Schedule' : 'Create Schedule'}
          </button>
        </div>
      </div>
    </div>
  );
}
