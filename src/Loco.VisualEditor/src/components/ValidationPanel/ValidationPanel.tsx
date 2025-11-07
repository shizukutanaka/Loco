import { useEffect, useState } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import {
  validateWorkflow,
  ValidationResult,
  formatValidationError,
  formatValidationWarning,
} from '@/utils/validation';
import { AlertCircle, AlertTriangle, CheckCircle, X } from 'lucide-react';

export function ValidationPanel() {
  const { workflow } = useWorkflowStore();
  const [validationResult, setValidationResult] = useState<ValidationResult | null>(
    null
  );
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    if (workflow) {
      const result = validateWorkflow(workflow);
      setValidationResult(result);

      // Auto-show panel if there are errors
      if (result.errors.length > 0) {
        setIsVisible(true);
      }
    }
  }, [workflow]);

  if (!validationResult) return null;

  const { isValid, errors, warnings } = validationResult;
  const hasIssues = errors.length > 0 || warnings.length > 0;

  if (!isVisible && hasIssues) {
    // Show compact indicator when panel is hidden
    return (
      <button
        onClick={() => setIsVisible(true)}
        className="fixed bottom-4 right-4 p-3 bg-white border-2 border-gray-200 rounded-lg shadow-lg hover:shadow-xl transition-shadow"
      >
        <div className="flex items-center gap-2">
          {errors.length > 0 ? (
            <>
              <AlertCircle className="w-5 h-5 text-red-500" />
              <span className="text-sm font-medium text-red-600">
                {errors.length} error{errors.length !== 1 ? 's' : ''}
              </span>
            </>
          ) : warnings.length > 0 ? (
            <>
              <AlertTriangle className="w-5 h-5 text-yellow-500" />
              <span className="text-sm font-medium text-yellow-600">
                {warnings.length} warning{warnings.length !== 1 ? 's' : ''}
              </span>
            </>
          ) : (
            <>
              <CheckCircle className="w-5 h-5 text-green-500" />
              <span className="text-sm font-medium text-green-600">Valid</span>
            </>
          )}
        </div>
      </button>
    );
  }

  if (!isVisible) return null;

  return (
    <div className="fixed bottom-4 right-4 w-96 bg-white border-2 border-gray-200 rounded-lg shadow-xl max-h-96 flex flex-col">
      {/* Header */}
      <div className="p-3 border-b border-gray-200 flex items-center justify-between">
        <div className="flex items-center gap-2">
          {isValid ? (
            <>
              <CheckCircle className="w-5 h-5 text-green-500" />
              <span className="font-semibold text-green-600">Workflow Valid</span>
            </>
          ) : (
            <>
              <AlertCircle className="w-5 h-5 text-red-500" />
              <span className="font-semibold text-red-600">Validation Issues</span>
            </>
          )}
        </div>
        <button
          onClick={() => setIsVisible(false)}
          className="p-1 hover:bg-gray-100 rounded transition-colors"
        >
          <X className="w-4 h-4 text-gray-500" />
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-3">
        {/* Errors */}
        {errors.length > 0 && (
          <div className="mb-4">
            <div className="flex items-center gap-2 mb-2">
              <AlertCircle className="w-4 h-4 text-red-500" />
              <span className="text-sm font-semibold text-red-600">
                Errors ({errors.length})
              </span>
            </div>
            <div className="space-y-2">
              {errors.map((error, index) => (
                <div
                  key={index}
                  className="p-2 bg-red-50 border border-red-200 rounded text-sm text-red-700"
                >
                  {formatValidationError(error)}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Warnings */}
        {warnings.length > 0 && (
          <div>
            <div className="flex items-center gap-2 mb-2">
              <AlertTriangle className="w-4 h-4 text-yellow-500" />
              <span className="text-sm font-semibold text-yellow-600">
                Warnings ({warnings.length})
              </span>
            </div>
            <div className="space-y-2">
              {warnings.map((warning, index) => (
                <div
                  key={index}
                  className="p-2 bg-yellow-50 border border-yellow-200 rounded text-sm text-yellow-700"
                >
                  {formatValidationWarning(warning)}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Success message */}
        {isValid && (
          <div className="flex items-center gap-2 p-3 bg-green-50 border border-green-200 rounded">
            <CheckCircle className="w-5 h-5 text-green-500" />
            <div>
              <div className="text-sm font-medium text-green-700">
                Workflow is valid
              </div>
              <div className="text-xs text-green-600">
                All checks passed successfully
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
