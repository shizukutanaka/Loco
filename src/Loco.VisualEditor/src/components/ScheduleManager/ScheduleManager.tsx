/**
 * Schedule Manager Component
 *
 * Displays and manages all workflow schedules.
 * Allows viewing, editing, enabling/disabling, and deleting schedules.
 */

import { useState, useEffect } from 'react';
import { Calendar, Clock, Trash2, Edit2, Play, Pause, X, AlertCircle } from 'lucide-react';
import { ScheduleEditor, WorkflowSchedule } from '../ScheduleEditor/ScheduleEditor';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface ScheduleManagerProps {
  isOpen: boolean;
  onClose: () => void;
}

interface ScheduleListItem extends WorkflowSchedule {
  workflowName: string;
  lastRun?: string;
  nextRun?: string;
  runCount?: number;
}

// ============================================================================
// Schedule Manager Component
// ============================================================================

export function ScheduleManager({ isOpen, onClose }: ScheduleManagerProps) {
  const [schedules, setSchedules] = useState<ScheduleListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [editingSchedule, setEditingSchedule] = useState<ScheduleListItem | null>(null);
  const [isEditorOpen, setIsEditorOpen] = useState(false);
  const toast = useToast();

  // Fetch schedules on mount
  useEffect(() => {
    if (!isOpen) return;

    const fetchSchedules = async () => {
      setIsLoading(true);
      try {
        // TODO: Replace with actual API call
        // const response = await listSchedules();
        // if (response.success && response.data) {
        //   setSchedules(response.data.schedules);
        // }

        // Mock data for now
        await new Promise((resolve) => setTimeout(resolve, 500));
        setSchedules([
          {
            id: '1',
            workflowId: 'workflow-1',
            workflowName: 'Daily Report Generation',
            cronExpression: '0 9 * * *',
            timezone: 'UTC',
            enabled: true,
            description: 'Generates daily sales report',
            nextRun: new Date(Date.now() + 3600000).toISOString(),
            lastRun: new Date(Date.now() - 86400000).toISOString(),
            runCount: 42,
          },
          {
            id: '2',
            workflowId: 'workflow-2',
            workflowName: 'Weekly Backup',
            cronExpression: '0 0 * * 0',
            timezone: 'America/New_York',
            enabled: false,
            description: 'Weekly database backup',
            nextRun: new Date(Date.now() + 604800000).toISOString(),
            lastRun: new Date(Date.now() - 604800000).toISOString(),
            runCount: 8,
          },
        ]);
      } catch (error) {
        console.error('Failed to fetch schedules:', error);
        toast.error('Failed to load schedules');
      } finally {
        setIsLoading(false);
      }
    };

    fetchSchedules();
  }, [isOpen, toast]);

  const handleToggleEnabled = async (scheduleId: string, currentEnabled: boolean) => {
    try {
      // TODO: Call API to toggle schedule
      // const response = await updateSchedule(scheduleId, { enabled: !currentEnabled });

      setSchedules((prev) =>
        prev.map((s) =>
          s.id === scheduleId ? { ...s, enabled: !currentEnabled } : s
        )
      );

      toast.success(
        currentEnabled
          ? 'Schedule paused successfully'
          : 'Schedule enabled successfully'
      );
    } catch (error) {
      console.error('Failed to toggle schedule:', error);
      toast.error('Failed to update schedule');
    }
  };

  const handleDelete = async (scheduleId: string) => {
    if (!confirm('Are you sure you want to delete this schedule?')) return;

    try {
      // TODO: Call API to delete schedule
      // const response = await deleteSchedule(scheduleId);

      setSchedules((prev) => prev.filter((s) => s.id !== scheduleId));
      toast.success('Schedule deleted successfully');
    } catch (error) {
      console.error('Failed to delete schedule:', error);
      toast.error('Failed to delete schedule');
    }
  };

  const handleEdit = (schedule: ScheduleListItem) => {
    setEditingSchedule(schedule);
    setIsEditorOpen(true);
  };

  const handleSaveSchedule = async (schedule: WorkflowSchedule) => {
    try {
      // TODO: Call API to update schedule
      // const response = await updateSchedule(schedule.id!, schedule);

      setSchedules((prev) =>
        prev.map((s) =>
          s.id === schedule.id ? { ...s, ...schedule } : s
        )
      );

      toast.success('Schedule updated successfully');
      setIsEditorOpen(false);
      setEditingSchedule(null);
    } catch (error) {
      console.error('Failed to update schedule:', error);
      toast.error('Failed to update schedule');
    }
  };

  const getCronDescription = (cron: string): string => {
    const parts = cron.split(' ');
    if (parts.length !== 5) return 'Invalid';

    const [minute, hour, dayOfMonth, month, dayOfWeek] = parts;

    if (cron === '* * * * *') return 'Every minute';
    if (cron === '0 * * * *') return 'Every hour';
    if (minute !== '*' && hour !== '*' && dayOfMonth === '*' && month === '*' && dayOfWeek === '*') {
      return `Daily at ${hour.padStart(2, '0')}:${minute.padStart(2, '0')}`;
    }
    if (minute !== '*' && hour !== '*' && dayOfMonth === '*' && month === '*' && dayOfWeek !== '*') {
      const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
      return `Weekly on ${days[parseInt(dayOfWeek)]}`;
    }
    if (minute !== '*' && hour !== '*' && dayOfMonth !== '*' && month === '*' && dayOfWeek === '*') {
      return `Monthly on day ${dayOfMonth}`;
    }

    return `Cron: ${cron}`;
  };

  if (!isOpen) return null;

  return (
    <>
      <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
        <div className="bg-white rounded-xl shadow-2xl max-w-5xl w-full max-h-[90vh] flex flex-col">
          {/* Header */}
          <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
            <div>
              <h2 className="text-xl font-bold text-gray-900">Scheduled Workflows</h2>
              <p className="text-sm text-gray-500 mt-1">
                {schedules.length} schedule{schedules.length !== 1 ? 's' : ''} total
              </p>
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
          <div className="flex-1 overflow-y-auto p-6">
            {isLoading ? (
              <div className="flex items-center justify-center py-12">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-loco-primary"></div>
              </div>
            ) : schedules.length === 0 ? (
              <div className="text-center py-12">
                <Calendar className="w-12 h-12 mx-auto mb-4 text-gray-400" />
                <p className="text-gray-600 mb-2">No schedules found</p>
                <p className="text-sm text-gray-500">
                  Create a schedule from the workflow list
                </p>
              </div>
            ) : (
              <div className="space-y-4">
                {schedules.map((schedule) => (
                  <div
                    key={schedule.id}
                    className={`border rounded-lg p-4 transition-all ${
                      schedule.enabled
                        ? 'border-gray-200 bg-white'
                        : 'border-gray-200 bg-gray-50 opacity-75'
                    }`}
                  >
                    <div className="flex items-start justify-between">
                      {/* Schedule Info */}
                      <div className="flex-1">
                        <div className="flex items-center gap-3 mb-2">
                          <h3 className="font-semibold text-gray-900">
                            {schedule.workflowName}
                          </h3>
                          <span
                            className={`px-2 py-0.5 rounded text-xs font-medium ${
                              schedule.enabled
                                ? 'bg-green-100 text-green-700'
                                : 'bg-gray-200 text-gray-700'
                            }`}
                          >
                            {schedule.enabled ? 'Active' : 'Paused'}
                          </span>
                        </div>

                        {schedule.description && (
                          <p className="text-sm text-gray-600 mb-3">
                            {schedule.description}
                          </p>
                        )}

                        <div className="flex items-center gap-6 text-sm text-gray-600">
                          <div className="flex items-center gap-2">
                            <Clock className="w-4 h-4" />
                            <span>{getCronDescription(schedule.cronExpression)}</span>
                          </div>
                          <div className="flex items-center gap-2">
                            <Calendar className="w-4 h-4" />
                            <span>{schedule.timezone}</span>
                          </div>
                          {schedule.runCount !== undefined && (
                            <div>
                              <span className="text-gray-500">Runs: </span>
                              <span className="font-medium">{schedule.runCount}</span>
                            </div>
                          )}
                        </div>

                        {schedule.nextRun && (
                          <div className="mt-3 p-2 bg-blue-50 border border-blue-100 rounded text-xs">
                            <span className="text-blue-600 font-medium">Next run: </span>
                            <span className="text-blue-700">
                              {new Date(schedule.nextRun).toLocaleString()}
                            </span>
                          </div>
                        )}
                      </div>

                      {/* Actions */}
                      <div className="flex items-center gap-2 ml-4">
                        <button
                          onClick={() => handleToggleEnabled(schedule.id!, schedule.enabled)}
                          className={`p-2 rounded transition-colors ${
                            schedule.enabled
                              ? 'text-orange-600 hover:bg-orange-50'
                              : 'text-green-600 hover:bg-green-50'
                          }`}
                          title={schedule.enabled ? 'Pause schedule' : 'Enable schedule'}
                        >
                          {schedule.enabled ? (
                            <Pause className="w-4 h-4" />
                          ) : (
                            <Play className="w-4 h-4" />
                          )}
                        </button>
                        <button
                          onClick={() => handleEdit(schedule)}
                          className="p-2 text-gray-600 hover:bg-gray-100 rounded transition-colors"
                          title="Edit schedule"
                        >
                          <Edit2 className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleDelete(schedule.id!)}
                          className="p-2 text-red-600 hover:bg-red-50 rounded transition-colors"
                          title="Delete schedule"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </div>

                    {/* Warning for disabled schedules */}
                    {!schedule.enabled && (
                      <div className="mt-3 p-2 bg-yellow-50 border border-yellow-100 rounded flex items-start gap-2">
                        <AlertCircle className="w-4 h-4 text-yellow-600 flex-shrink-0 mt-0.5" />
                        <p className="text-xs text-yellow-700">
                          This schedule is paused and will not run automatically
                        </p>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Schedule Editor Modal */}
      {isEditorOpen && editingSchedule && (
        <ScheduleEditor
          workflowId={editingSchedule.workflowId}
          workflowName={editingSchedule.workflowName}
          isOpen={isEditorOpen}
          onClose={() => {
            setIsEditorOpen(false);
            setEditingSchedule(null);
          }}
          onSave={handleSaveSchedule}
          existingSchedule={editingSchedule}
        />
      )}
    </>
  );
}
