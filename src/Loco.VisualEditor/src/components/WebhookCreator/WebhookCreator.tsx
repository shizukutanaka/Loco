/**
 * Webhook Creator Component
 *
 * Creates new webhook endpoints for workflow automation.
 */

import { useState } from 'react';
import { X, AlertCircle, Copy, CheckCircle2 } from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';
import type { Webhook } from '../WebhookManager/WebhookManager';

// ============================================================================
// Types
// ============================================================================

interface WebhookCreatorProps {
  workflowId: string;
  workflowName: string;
  isOpen: boolean;
  onClose: () => void;
  onSave: (webhook: Partial<Webhook>) => void;
}

// ============================================================================
// Webhook Creator Component
// ============================================================================

export function WebhookCreator({
  workflowId,
  workflowName,
  isOpen,
  onClose,
  onSave,
}: WebhookCreatorProps) {
  const [method, setMethod] = useState<'GET' | 'POST' | 'PUT' | 'DELETE'>('POST');
  const [requireSecret, setRequireSecret] = useState(true);
  const [generatedUrl, setGeneratedUrl] = useState('');
  const [generatedSecret, setGeneratedSecret] = useState('');
  const [isCreated, setIsCreated] = useState(false);
  const toast = useToast();

  if (!isOpen) return null;

  const handleCreate = () => {
    // Generate webhook URL and secret
    const webhookId = crypto.randomUUID().slice(0, 12);
    const url = `https://api.loco.dev/webhooks/${webhookId}`;
    const secret = requireSecret ? `wh_sec_${crypto.randomUUID().slice(0, 16)}` : undefined;

    setGeneratedUrl(url);
    setGeneratedSecret(secret || '');
    setIsCreated(true);

    const webhook: Partial<Webhook> = {
      workflowId,
      workflowName,
      url,
      method,
      enabled: true,
      secret,
    };

    onSave(webhook);
  };

  const handleCopyUrl = () => {
    navigator.clipboard.writeText(generatedUrl);
    toast.success('Webhook URL copied!');
  };

  const handleCopySecret = () => {
    navigator.clipboard.writeText(generatedSecret);
    toast.success('Webhook secret copied!');
  };

  const handleDone = () => {
    setIsCreated(false);
    setGeneratedUrl('');
    setGeneratedSecret('');
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Create Webhook</h2>
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
        <div className="p-6">
          {!isCreated ? (
            <div className="space-y-6">
              {/* Info Box */}
              <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                <div className="flex items-start gap-2">
                  <AlertCircle className="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" />
                  <div className="text-sm text-blue-700">
                    <p className="font-semibold mb-1">Webhook Trigger</p>
                    <p>
                      Webhooks allow external services to trigger this workflow via HTTP requests.
                      Once created, you'll receive a unique URL to use in your applications.
                    </p>
                  </div>
                </div>
              </div>

              {/* HTTP Method */}
              <div>
                <label className="block text-sm font-semibold text-gray-900 mb-3">
                  HTTP Method
                </label>
                <div className="grid grid-cols-4 gap-2">
                  {(['GET', 'POST', 'PUT', 'DELETE'] as const).map((m) => (
                    <button
                      key={m}
                      onClick={() => setMethod(m)}
                      className={`px-4 py-3 rounded-lg border-2 text-sm font-medium transition-colors ${
                        method === m
                          ? 'border-loco-primary bg-loco-primary/10 text-loco-primary'
                          : 'border-gray-200 hover:border-gray-300 text-gray-700'
                      }`}
                    >
                      {m}
                    </button>
                  ))}
                </div>
                <p className="text-xs text-gray-500 mt-2">
                  {method === 'GET' && 'Suitable for simple triggers with URL parameters'}
                  {method === 'POST' && 'Recommended for sending data in the request body'}
                  {method === 'PUT' && 'Used for updating resources'}
                  {method === 'DELETE' && 'Used for deletion triggers'}
                </p>
              </div>

              {/* Security Options */}
              <div>
                <label className="block text-sm font-semibold text-gray-900 mb-3">
                  Security
                </label>
                <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
                  <div>
                    <h3 className="font-medium text-gray-900">Require Secret</h3>
                    <p className="text-sm text-gray-600 mt-1">
                      Generate a secret key for request validation
                    </p>
                  </div>
                  <button
                    onClick={() => setRequireSecret(!requireSecret)}
                    className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                      requireSecret ? 'bg-loco-primary' : 'bg-gray-300'
                    }`}
                  >
                    <span
                      className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                        requireSecret ? 'translate-x-6' : 'translate-x-1'
                      }`}
                    />
                  </button>
                </div>
                {requireSecret && (
                  <div className="mt-3 p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
                    <p className="text-xs text-yellow-700">
                      <strong>Important:</strong> Include the secret in the{' '}
                      <code className="bg-yellow-100 px-1 rounded">X-Webhook-Secret</code> header
                      of your requests for verification.
                    </p>
                  </div>
                )}
              </div>

              {/* Example Request */}
              <div>
                <label className="block text-sm font-semibold text-gray-900 mb-2">
                  Example Request
                </label>
                <pre className="text-xs bg-gray-900 text-green-400 p-4 rounded-lg overflow-x-auto font-mono">
{`curl -X ${method} https://api.loco.dev/webhooks/YOUR_WEBHOOK_ID \\${requireSecret ? `\n  -H "X-Webhook-Secret: YOUR_SECRET" \\` : ''}
  -H "Content-Type: application/json" \\
  -d '{"key": "value"}'`}
                </pre>
              </div>
            </div>
          ) : (
            <div className="space-y-6">
              {/* Success Message */}
              <div className="p-4 bg-green-50 border border-green-200 rounded-lg">
                <div className="flex items-start gap-2">
                  <CheckCircle2 className="w-5 h-5 text-green-600 flex-shrink-0 mt-0.5" />
                  <div className="text-sm text-green-700">
                    <p className="font-semibold mb-1">Webhook Created Successfully!</p>
                    <p>
                      Your webhook is now active. Use the URL below to trigger this workflow
                      from external services.
                    </p>
                  </div>
                </div>
              </div>

              {/* Webhook URL */}
              <div>
                <label className="block text-sm font-semibold text-gray-900 mb-2">
                  Webhook URL
                </label>
                <div className="flex items-center gap-2">
                  <code className="flex-1 text-sm bg-gray-100 px-4 py-3 rounded-lg font-mono text-gray-700 break-all">
                    {generatedUrl}
                  </code>
                  <button
                    onClick={handleCopyUrl}
                    className="p-3 text-gray-600 hover:bg-gray-100 rounded-lg transition-colors flex-shrink-0"
                    title="Copy URL"
                  >
                    <Copy className="w-5 h-5" />
                  </button>
                </div>
              </div>

              {/* Webhook Secret */}
              {generatedSecret && (
                <div>
                  <label className="block text-sm font-semibold text-gray-900 mb-2">
                    Webhook Secret
                  </label>
                  <div className="flex items-center gap-2">
                    <code className="flex-1 text-sm bg-yellow-50 px-4 py-3 rounded-lg font-mono text-yellow-800 break-all border border-yellow-200">
                      {generatedSecret}
                    </code>
                    <button
                      onClick={handleCopySecret}
                      className="p-3 text-gray-600 hover:bg-gray-100 rounded-lg transition-colors flex-shrink-0"
                      title="Copy Secret"
                    >
                      <Copy className="w-5 h-5" />
                    </button>
                  </div>
                  <p className="text-xs text-yellow-700 mt-2">
                    ⚠️ Save this secret now. You won't be able to see it again after closing this dialog.
                  </p>
                </div>
              )}

              {/* Method Info */}
              <div className="p-4 bg-gray-50 rounded-lg">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-gray-600">HTTP Method:</span>
                  <span className="font-medium text-gray-900">{method}</span>
                </div>
              </div>

              {/* Test Command */}
              <div>
                <label className="block text-sm font-semibold text-gray-900 mb-2">
                  Test Command
                </label>
                <pre className="text-xs bg-gray-900 text-green-400 p-4 rounded-lg overflow-x-auto font-mono">
{`curl -X ${method} ${generatedUrl} \\${generatedSecret ? `\n  -H "X-Webhook-Secret: ${generatedSecret}" \\` : ''}
  -H "Content-Type: application/json" \\
  -d '{"test": true}'`}
                </pre>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 flex items-center justify-end gap-3">
          {!isCreated ? (
            <>
              <button
                onClick={onClose}
                className="px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleCreate}
                className="px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
              >
                Create Webhook
              </button>
            </>
          ) : (
            <button
              onClick={handleDone}
              className="px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
            >
              Done
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
