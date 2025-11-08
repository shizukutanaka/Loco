/**
 * Commit Dialog Component
 *
 * Allows users to commit workflow changes with a message.
 * Shows current changes and provides Git-style commit interface.
 */

import { useState } from 'react';
import { X, GitCommit, AlertCircle, CheckCircle2 } from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface CommitDialogProps {
  workflowId: string;
  workflowName: string;
  isOpen: boolean;
  onClose: () => void;
  onCommit?: (commitMessage: string) => void;
  changes?: {
    nodesAdded: number;
    nodesRemoved: number;
    nodesModified: number;
    edgesAdded: number;
    edgesRemoved: number;
  };
}

// ============================================================================
// Commit Dialog Component
// ============================================================================

export function CommitDialog({
  workflowId,
  workflowName,
  isOpen,
  onClose,
  onCommit,
  changes = {
    nodesAdded: 0,
    nodesRemoved: 0,
    nodesModified: 0,
    edgesAdded: 0,
    edgesRemoved: 0,
  },
}: CommitDialogProps) {
  const [commitMessage, setCommitMessage] = useState('');
  const [authorName, setAuthorName] = useState('');
  const [isCommitting, setIsCommitting] = useState(false);
  const toast = useToast();

  const hasChanges =
    changes.nodesAdded > 0 ||
    changes.nodesRemoved > 0 ||
    changes.nodesModified > 0 ||
    changes.edgesAdded > 0 ||
    changes.edgesRemoved > 0;

  const handleCommit = async () => {
    if (!commitMessage.trim()) {
      toast.error('Please enter a commit message');
      return;
    }

    if (!authorName.trim()) {
      toast.error('Please enter your name');
      return;
    }

    if (!hasChanges) {
      toast.error('No changes to commit');
      return;
    }

    setIsCommitting(true);

    try {
      // TODO: Call API to commit workflow
      // const response = await commitWorkflow(workflowId, {
      //   message: commitMessage,
      //   author: authorName,
      //   changes,
      // });
      console.log('Committing workflow:', workflowId, { message: commitMessage, author: authorName });

      await new Promise((resolve) => setTimeout(resolve, 1000));

      toast.success('Workflow committed successfully!');

      if (onCommit) {
        onCommit(commitMessage);
      }

      // Reset form
      setCommitMessage('');
      setAuthorName('');
      onClose();
    } catch (error) {
      console.error('Failed to commit workflow:', error);
      toast.error('Failed to commit workflow');
    } finally {
      setIsCommitting(false);
    }
  };

  const getChangesList = () => {
    const changeItems = [];

    if (changes.nodesAdded > 0) {
      changeItems.push({
        type: 'added',
        text: `${changes.nodesAdded} node${changes.nodesAdded > 1 ? 's' : ''} added`,
        color: 'text-green-700',
        bg: 'bg-green-50',
      });
    }

    if (changes.nodesModified > 0) {
      changeItems.push({
        type: 'modified',
        text: `${changes.nodesModified} node${changes.nodesModified > 1 ? 's' : ''} modified`,
        color: 'text-blue-700',
        bg: 'bg-blue-50',
      });
    }

    if (changes.nodesRemoved > 0) {
      changeItems.push({
        type: 'removed',
        text: `${changes.nodesRemoved} node${changes.nodesRemoved > 1 ? 's' : ''} removed`,
        color: 'text-red-700',
        bg: 'bg-red-50',
      });
    }

    if (changes.edgesAdded > 0) {
      changeItems.push({
        type: 'added',
        text: `${changes.edgesAdded} connection${changes.edgesAdded > 1 ? 's' : ''} added`,
        color: 'text-green-700',
        bg: 'bg-green-50',
      });
    }

    if (changes.edgesRemoved > 0) {
      changeItems.push({
        type: 'removed',
        text: `${changes.edgesRemoved} connection${changes.edgesRemoved > 1 ? 's' : ''} removed`,
        color: 'text-red-700',
        bg: 'bg-red-50',
      });
    }

    return changeItems;
  };

  if (!isOpen) return null;

  const changesList = getChangesList();

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Commit Changes</h2>
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
          {/* Info Box */}
          <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
            <div className="flex items-start gap-2">
              <AlertCircle className="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" />
              <div className="text-sm text-blue-700">
                <p className="font-semibold mb-1">Version Control</p>
                <p>
                  Commit your workflow changes to create a snapshot in the version history.
                  You can restore previous versions at any time.
                </p>
              </div>
            </div>
          </div>

          {/* Changes Summary */}
          <div>
            <label className="block text-sm font-semibold text-gray-900 mb-3">
              Changes to Commit
            </label>

            {!hasChanges ? (
              <div className="p-4 bg-gray-50 border border-gray-200 rounded-lg text-center">
                <p className="text-sm text-gray-600">No changes detected</p>
              </div>
            ) : (
              <div className="space-y-2">
                {changesList.map((change, index) => (
                  <div
                    key={index}
                    className={`flex items-center gap-3 px-4 py-3 rounded-lg border ${change.bg} border-gray-200`}
                  >
                    <CheckCircle2 className={`w-4 h-4 ${change.color}`} />
                    <span className={`text-sm font-medium ${change.color}`}>
                      {change.text}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Author Name */}
          <div>
            <label className="block text-sm font-semibold text-gray-900 mb-2">
              Author Name
            </label>
            <input
              type="text"
              value={authorName}
              onChange={(e) => setAuthorName(e.target.value)}
              placeholder="Your name"
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
              disabled={!hasChanges}
            />
            <p className="text-xs text-gray-500 mt-1">
              This will be recorded as the author of this commit
            </p>
          </div>

          {/* Commit Message */}
          <div>
            <label className="block text-sm font-semibold text-gray-900 mb-2">
              Commit Message *
            </label>
            <textarea
              value={commitMessage}
              onChange={(e) => setCommitMessage(e.target.value)}
              placeholder="Describe your changes... (e.g., 'Add email notification step')"
              rows={4}
              className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent resize-none"
              disabled={!hasChanges}
            />
            <p className="text-xs text-gray-500 mt-1">
              Write a clear, concise message describing what changed and why
            </p>
          </div>

          {/* Example Messages */}
          <div>
            <label className="block text-sm font-semibold text-gray-900 mb-2">
              Example Commit Messages
            </label>
            <div className="space-y-1.5">
              {[
                'Add HTTP request validation',
                'Update error handling logic',
                'Remove deprecated transformation step',
                'Fix email template configuration',
                'Optimize workflow execution order',
              ].map((example, index) => (
                <button
                  key={index}
                  onClick={() => setCommitMessage(example)}
                  className="block w-full text-left px-3 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded transition-colors"
                  disabled={!hasChanges}
                >
                  {example}
                </button>
              ))}
            </div>
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
            onClick={handleCommit}
            disabled={!commitMessage.trim() || !authorName.trim() || !hasChanges || isCommitting}
            className="flex items-center gap-2 px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <GitCommit className="w-4 h-4" />
            <span>{isCommitting ? 'Committing...' : 'Commit Changes'}</span>
          </button>
        </div>
      </div>
    </div>
  );
}
