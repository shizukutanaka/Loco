import { useEffect, useState, useCallback, useMemo, memo } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { ValidationReport } from '@/utils/workflowValidationService';
import { AlertCircle, AlertTriangle, CheckCircle, X } from 'lucide-react';

// ============================================================================
// Validation Panel Component
// ============================================================================

function ValidationPanelComponent() {
  const { nodes, edges } = useWorkflowStore();
  const [validationReport, setValidationReport] = useState<ValidationReport | null>(null);
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    const validateWorkflow = async () => {
      try {
        // Use unified analysis engine for consistent results
        const { analysisEngine } = await import('@/utils/workflowAnalysisEngine');
        const analysisResult = analysisEngine.analyze(nodes, edges);
        const report = analysisResult.validation;
        setValidationReport(report);

        // Auto-show panel if there are errors
        if (report.issues.filter((i) => i.severity === 'error').length > 0) {
          setIsVisible(true);
        }
      } catch (error) {
        console.error('Validation failed:', error);
      }
    };

    if (nodes.length > 0) {
      validateWorkflow();
    }
  }, [nodes, edges]);

  // All hooks must run on every render (Rules of Hooks): they were previously
  // placed AFTER `if (!validationReport) return null`, so when the report
  // transitioned from null to non-null React threw "rendered more hooks than
  // during the previous render" and the panel crashed. Compute over a safe
  // empty issues list when there is no report yet, and gate rendering below.
  const { errors, warnings } = useMemo(() => {
    const errors = [];
    const warnings = [];
    for (const issue of validationReport?.issues ?? []) {
      if (issue.severity === 'error') {
        errors.push(issue);
      } else if (issue.severity === 'warning') {
        warnings.push(issue);
      }
    }
    return { errors, warnings };
  }, [validationReport]);

  const hasIssues = useMemo(
    () => errors.length > 0 || warnings.length > 0,
    [errors, warnings]
  );

  const isValid = useMemo(() => errors.length === 0, [errors]);

  // Memoize visibility toggle handlers
  const handleShowPanel = useCallback(() => {
    setIsVisible(true);
  }, []);

  const handleHidePanel = useCallback(() => {
    setIsVisible(false);
  }, []);

  if (!validationReport) return null;

  if (!isVisible && hasIssues) {
    // Show compact indicator when panel is hidden
    return (
      <button
        onClick={handleShowPanel}
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
          onClick={handleHidePanel}
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
              {errors.map((error) => (
                <div
                  key={error.id}
                  className="p-2 bg-red-50 border border-red-200 rounded text-sm text-red-700"
                >
                  <div className="font-medium">{error.title}</div>
                  <div className="text-xs">{error.description}</div>
                  {error.nodeId && (
                    <div className="text-xs mt-1 text-red-600">
                      Node: {error.nodeId}
                    </div>
                  )}
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
              {warnings.map((warning) => (
                <div
                  key={warning.id}
                  className="p-2 bg-yellow-50 border border-yellow-200 rounded text-sm text-yellow-700"
                >
                  <div className="font-medium">{warning.title}</div>
                  <div className="text-xs">{warning.description}</div>
                  {warning.nodeId && (
                    <div className="text-xs mt-1 text-yellow-600">
                      Node: {warning.nodeId}
                    </div>
                  )}
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

export const ValidationPanel = memo(ValidationPanelComponent);
ValidationPanel.displayName = 'ValidationPanel';
