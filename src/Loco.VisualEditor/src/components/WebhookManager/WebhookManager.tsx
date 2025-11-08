/**
 * Webhook Manager Component
 *
 * Manages workflow webhooks for HTTP-triggered automation.
 * Allows viewing, creating, testing, and deleting webhook endpoints.
 */

import { useState, useEffect } from 'react';
import { Trash2, Copy, RefreshCw, Play, Globe, X, AlertCircle, CheckCircle2 } from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface WebhookManagerProps {
  isOpen: boolean;
  onClose: () => void;
}

export interface Webhook {
  id: string;
  workflowId: string;
  workflowName: string;
  url: string;
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  enabled: boolean;
  createdAt: string;
  lastTriggered?: string;
  triggerCount: number;
  secret?: string;
}

interface WebhookLog {
  id: string;
  webhookId: string;
  timestamp: string;
  method: string;
  statusCode: number;
  duration: number;
  body?: string;
}

// ============================================================================
// Webhook Manager Component
// ============================================================================

export function WebhookManager({ isOpen, onClose }: WebhookManagerProps) {
  const [webhooks, setWebhooks] = useState<Webhook[]>([]);
  const [selectedWebhook, setSelectedWebhook] = useState<Webhook | null>(null);
  const [logs, setLogs] = useState<WebhookLog[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [showLogs, setShowLogs] = useState(false);
  const toast = useToast();

  // Fetch webhooks on mount
  useEffect(() => {
    if (!isOpen) return;

    const fetchWebhooks = async () => {
      setIsLoading(true);
      try {
        // TODO: Replace with actual API call
        // const response = await listWebhooks();

        // Mock data for now
        await new Promise((resolve) => setTimeout(resolve, 500));
        setWebhooks([
          {
            id: 'wh-1',
            workflowId: 'workflow-1',
            workflowName: 'Process Payment',
            url: 'https://api.loco.dev/webhooks/abc123def456',
            method: 'POST',
            enabled: true,
            createdAt: new Date(Date.now() - 86400000 * 7).toISOString(),
            lastTriggered: new Date(Date.now() - 3600000).toISOString(),
            triggerCount: 342,
            secret: 'wh_sec_xyz789',
          },
          {
            id: 'wh-2',
            workflowId: 'workflow-2',
            workflowName: 'Send Notification',
            url: 'https://api.loco.dev/webhooks/ghi789jkl012',
            method: 'POST',
            enabled: false,
            createdAt: new Date(Date.now() - 86400000 * 3).toISOString(),
            triggerCount: 0,
          },
        ]);
      } catch (error) {
        console.error('Failed to fetch webhooks:', error);
        toast.error('Failed to load webhooks');
      } finally {
        setIsLoading(false);
      }
    };

    fetchWebhooks();
  }, [isOpen, toast]);

  // Fetch logs for selected webhook
  useEffect(() => {
    if (!selectedWebhook || !showLogs) return;

    const fetchLogs = async () => {
      try {
        // TODO: Replace with actual API call
        // const response = await getWebhookLogs(selectedWebhook.id);

        // Mock data
        await new Promise((resolve) => setTimeout(resolve, 300));
        setLogs([
          {
            id: 'log-1',
            webhookId: selectedWebhook.id,
            timestamp: new Date(Date.now() - 3600000).toISOString(),
            method: 'POST',
            statusCode: 200,
            duration: 142,
            body: '{"user_id": "12345", "amount": 99.99}',
          },
          {
            id: 'log-2',
            webhookId: selectedWebhook.id,
            timestamp: new Date(Date.now() - 7200000).toISOString(),
            method: 'POST',
            statusCode: 200,
            duration: 98,
            body: '{"user_id": "67890", "amount": 149.99}',
          },
        ]);
      } catch (error) {
        console.error('Failed to fetch logs:', error);
      }
    };

    fetchLogs();
  }, [selectedWebhook, showLogs]);

  const handleCopyUrl = (url: string) => {
    navigator.clipboard.writeText(url);
    toast.success('Webhook URL copied to clipboard!');
  };

  const handleCopySecret = (secret: string) => {
    navigator.clipboard.writeText(secret);
    toast.success('Webhook secret copied to clipboard!');
  };

  const handleToggleEnabled = async (webhookId: string, currentEnabled: boolean) => {
    try {
      // TODO: Call API to toggle webhook
      // const response = await updateWebhook(webhookId, { enabled: !currentEnabled });

      setWebhooks((prev) =>
        prev.map((w) =>
          w.id === webhookId ? { ...w, enabled: !currentEnabled } : w
        )
      );

      toast.success(
        currentEnabled
          ? 'Webhook disabled successfully'
          : 'Webhook enabled successfully'
      );
    } catch (error) {
      console.error('Failed to toggle webhook:', error);
      toast.error('Failed to update webhook');
    }
  };

  const handleDelete = async (webhookId: string) => {
    if (!confirm('Are you sure you want to delete this webhook?')) return;

    try {
      // TODO: Call API to delete webhook
      // const response = await deleteWebhook(webhookId);

      setWebhooks((prev) => prev.filter((w) => w.id !== webhookId));
      toast.success('Webhook deleted successfully');
    } catch (error) {
      console.error('Failed to delete webhook:', error);
      toast.error('Failed to delete webhook');
    }
  };

  const handleRegenerateUrl = async (webhookId: string) => {
    if (!confirm('Regenerate webhook URL? The old URL will stop working.')) return;

    try {
      // TODO: Call API to regenerate webhook
      // const response = await regenerateWebhook(webhookId);

      const newUrl = `https://api.loco.dev/webhooks/${crypto.randomUUID().slice(0, 12)}`;

      setWebhooks((prev) =>
        prev.map((w) =>
          w.id === webhookId ? { ...w, url: newUrl } : w
        )
      );

      toast.success('Webhook URL regenerated successfully');
    } catch (error) {
      console.error('Failed to regenerate webhook:', error);
      toast.error('Failed to regenerate webhook');
    }
  };

  const handleTestWebhook = async (webhookId: string) => {
    try {
      // TODO: Call API to test webhook
      // const response = await testWebhook(webhookId);
      console.log('Testing webhook:', webhookId);

      toast.info('Sending test request...');

      await new Promise((resolve) => setTimeout(resolve, 1000));

      toast.success('Test request sent successfully! Check execution panel.');
    } catch (error) {
      console.error('Failed to test webhook:', error);
      toast.error('Failed to test webhook');
    }
  };

  const handleViewLogs = (webhook: Webhook) => {
    setSelectedWebhook(webhook);
    setShowLogs(true);
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-6xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Workflow Webhooks</h2>
            <p className="text-sm text-gray-500 mt-1">
              {webhooks.length} webhook{webhooks.length !== 1 ? 's' : ''} configured
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
          ) : webhooks.length === 0 ? (
            <div className="text-center py-12">
              <Globe className="w-12 h-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-600 mb-2">No webhooks found</p>
              <p className="text-sm text-gray-500">
                Create a webhook from the workflow list to get started
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {webhooks.map((webhook) => (
                <div
                  key={webhook.id}
                  className={`border rounded-lg p-4 transition-all ${
                    webhook.enabled
                      ? 'border-gray-200 bg-white'
                      : 'border-gray-200 bg-gray-50 opacity-75'
                  }`}
                >
                  <div className="flex items-start justify-between mb-3">
                    {/* Webhook Info */}
                    <div className="flex-1">
                      <div className="flex items-center gap-3 mb-2">
                        <h3 className="font-semibold text-gray-900">
                          {webhook.workflowName}
                        </h3>
                        <span
                          className={`px-2 py-0.5 rounded text-xs font-medium ${
                            webhook.enabled
                              ? 'bg-green-100 text-green-700'
                              : 'bg-gray-200 text-gray-700'
                          }`}
                        >
                          {webhook.enabled ? 'Active' : 'Disabled'}
                        </span>
                        <span className="px-2 py-0.5 bg-blue-100 text-blue-700 rounded text-xs font-medium">
                          {webhook.method}
                        </span>
                      </div>

                      {/* URL */}
                      <div className="flex items-center gap-2 mb-3">
                        <code className="flex-1 text-xs bg-gray-100 px-3 py-2 rounded font-mono text-gray-700 break-all">
                          {webhook.url}
                        </code>
                        <button
                          onClick={() => handleCopyUrl(webhook.url)}
                          className="p-2 text-gray-600 hover:bg-gray-100 rounded transition-colors flex-shrink-0"
                          title="Copy URL"
                        >
                          <Copy className="w-4 h-4" />
                        </button>
                      </div>

                      {/* Secret (if exists) */}
                      {webhook.secret && (
                        <div className="flex items-center gap-2 mb-3">
                          <div className="flex-1 flex items-center gap-2 text-xs bg-yellow-50 px-3 py-2 rounded">
                            <AlertCircle className="w-3 h-3 text-yellow-600 flex-shrink-0" />
                            <span className="text-yellow-700">Secret:</span>
                            <code className="font-mono text-yellow-800">{webhook.secret.slice(0, 20)}...</code>
                          </div>
                          <button
                            onClick={() => handleCopySecret(webhook.secret!)}
                            className="p-2 text-gray-600 hover:bg-gray-100 rounded transition-colors flex-shrink-0"
                            title="Copy Secret"
                          >
                            <Copy className="w-4 h-4" />
                          </button>
                        </div>
                      )}

                      {/* Stats */}
                      <div className="flex items-center gap-6 text-xs text-gray-600">
                        <div>
                          <span className="text-gray-500">Triggers: </span>
                          <span className="font-medium">{webhook.triggerCount.toLocaleString()}</span>
                        </div>
                        {webhook.lastTriggered && (
                          <div>
                            <span className="text-gray-500">Last: </span>
                            <span className="font-medium">
                              {new Date(webhook.lastTriggered).toLocaleString()}
                            </span>
                          </div>
                        )}
                        <div>
                          <span className="text-gray-500">Created: </span>
                          <span className="font-medium">
                            {new Date(webhook.createdAt).toLocaleDateString()}
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Actions */}
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => handleTestWebhook(webhook.id)}
                      className="flex items-center gap-1 px-3 py-1.5 text-xs bg-blue-100 text-blue-700 hover:bg-blue-200 rounded transition-colors"
                      disabled={!webhook.enabled}
                    >
                      <Play className="w-3 h-3" />
                      Test
                    </button>
                    <button
                      onClick={() => handleViewLogs(webhook)}
                      className="flex items-center gap-1 px-3 py-1.5 text-xs bg-gray-100 text-gray-700 hover:bg-gray-200 rounded transition-colors"
                    >
                      <CheckCircle2 className="w-3 h-3" />
                      Logs
                    </button>
                    <button
                      onClick={() => handleRegenerateUrl(webhook.id)}
                      className="p-1.5 text-gray-600 hover:bg-gray-100 rounded transition-colors"
                      title="Regenerate URL"
                    >
                      <RefreshCw className="w-3.5 h-3.5" />
                    </button>
                    <button
                      onClick={() => handleToggleEnabled(webhook.id, webhook.enabled)}
                      className={`px-3 py-1.5 text-xs rounded transition-colors ${
                        webhook.enabled
                          ? 'bg-orange-100 text-orange-700 hover:bg-orange-200'
                          : 'bg-green-100 text-green-700 hover:bg-green-200'
                      }`}
                    >
                      {webhook.enabled ? 'Disable' : 'Enable'}
                    </button>
                    <button
                      onClick={() => handleDelete(webhook.id)}
                      className="p-1.5 text-red-600 hover:bg-red-50 rounded transition-colors ml-auto"
                      title="Delete webhook"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Logs Modal */}
        {showLogs && selectedWebhook && (
          <div className="absolute inset-0 bg-white rounded-xl z-10 flex flex-col">
            {/* Logs Header */}
            <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
              <div>
                <h3 className="text-lg font-bold text-gray-900">Request Logs</h3>
                <p className="text-sm text-gray-500 mt-1">{selectedWebhook.workflowName}</p>
              </div>
              <button
                onClick={() => {
                  setShowLogs(false);
                  setSelectedWebhook(null);
                }}
                className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
              >
                <X className="w-5 h-5 text-gray-500" />
              </button>
            </div>

            {/* Logs Content */}
            <div className="flex-1 overflow-y-auto p-6">
              {logs.length === 0 ? (
                <div className="text-center py-12 text-gray-500">
                  <p>No request logs yet</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {logs.map((log) => (
                    <div
                      key={log.id}
                      className="border border-gray-200 rounded-lg p-4 hover:shadow-sm transition-shadow"
                    >
                      <div className="flex items-center justify-between mb-2">
                        <div className="flex items-center gap-3">
                          <span className="px-2 py-0.5 bg-blue-100 text-blue-700 rounded text-xs font-medium">
                            {log.method}
                          </span>
                          <span
                            className={`px-2 py-0.5 rounded text-xs font-medium ${
                              log.statusCode < 300
                                ? 'bg-green-100 text-green-700'
                                : 'bg-red-100 text-red-700'
                            }`}
                          >
                            {log.statusCode}
                          </span>
                          <span className="text-xs text-gray-600">
                            {log.duration}ms
                          </span>
                        </div>
                        <span className="text-xs text-gray-500">
                          {new Date(log.timestamp).toLocaleString()}
                        </span>
                      </div>
                      {log.body && (
                        <details className="mt-2">
                          <summary className="text-xs text-gray-600 cursor-pointer hover:text-gray-900">
                            Request Body
                          </summary>
                          <pre className="mt-2 text-xs bg-gray-50 p-3 rounded overflow-x-auto">
                            {JSON.stringify(JSON.parse(log.body), null, 2)}
                          </pre>
                        </details>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
