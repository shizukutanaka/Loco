/**
 * Execution Replay Component
 *
 * Replay and debug past workflow executions with step-by-step controls.
 * Allows time-travel debugging and inspection of node-by-node execution.
 */

import { useState, useEffect } from 'react';
import {
  X,
  Play,
  Pause,
  SkipForward,
  SkipBack,
  RotateCcw,
  Clock,
  CheckCircle,
  XCircle,
  AlertCircle,
  ChevronRight,
  FastForward,
} from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface ExecutionReplayProps {
  executionId: string;
  isOpen: boolean;
  onClose: () => void;
}

interface ReplayStep {
  nodeId: string;
  nodeName: string;
  nodeType: string;
  status: 'pending' | 'running' | 'completed' | 'failed';
  startTime?: string;
  endTime?: string;
  duration?: number;
  input?: any;
  output?: any;
  error?: string;
}

interface ExecutionReplayData {
  id: string;
  workflowName: string;
  startedAt: string;
  completedAt?: string;
  status: 'completed' | 'failed' | 'running';
  totalSteps: number;
  steps: ReplayStep[];
}

type PlaybackSpeed = 0.5 | 1 | 2 | 4;

// ============================================================================
// Execution Replay Component
// ============================================================================

export function ExecutionReplay({ executionId, isOpen, onClose }: ExecutionReplayProps) {
  const [execution, setExecution] = useState<ExecutionReplayData | null>(null);
  const [currentStep, setCurrentStep] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackSpeed, setPlaybackSpeed] = useState<PlaybackSpeed>(1);
  const [isLoading, setIsLoading] = useState(false);
  const toast = useToast();

  // Fetch execution data
  useEffect(() => {
    if (!isOpen || !executionId) return;

    const fetchExecution = async () => {
      setIsLoading(true);
      try {
        // TODO: Replace with actual API call
        // const response = await getExecutionDetails(executionId);

        // Mock data for demonstration
        await new Promise((resolve) => setTimeout(resolve, 500));
        setExecution({
          id: executionId,
          workflowName: 'Process Payment Workflow',
          startedAt: new Date(Date.now() - 300000).toISOString(),
          completedAt: new Date(Date.now() - 240000).toISOString(),
          status: 'failed',
          totalSteps: 5,
          steps: [
            {
              nodeId: 'node-1',
              nodeName: 'Validate Request',
              nodeType: 'validation',
              status: 'completed',
              startTime: new Date(Date.now() - 300000).toISOString(),
              endTime: new Date(Date.now() - 298000).toISOString(),
              duration: 2000,
              input: { amount: 99.99, currency: 'USD' },
              output: { valid: true },
            },
            {
              nodeId: 'node-2',
              nodeName: 'Check Balance',
              nodeType: 'api',
              status: 'completed',
              startTime: new Date(Date.now() - 298000).toISOString(),
              endTime: new Date(Date.now() - 295000).toISOString(),
              duration: 3000,
              input: { userId: '12345' },
              output: { balance: 150.0, available: true },
            },
            {
              nodeId: 'node-3',
              nodeName: 'Process Payment',
              nodeType: 'api',
              status: 'failed',
              startTime: new Date(Date.now() - 295000).toISOString(),
              endTime: new Date(Date.now() - 290000).toISOString(),
              duration: 5000,
              input: { amount: 99.99, method: 'credit_card' },
              error: 'Payment gateway timeout after 5000ms',
            },
            {
              nodeId: 'node-4',
              nodeName: 'Send Confirmation',
              nodeType: 'notification',
              status: 'pending',
            },
            {
              nodeId: 'node-5',
              nodeName: 'Update Database',
              nodeType: 'database',
              status: 'pending',
            },
          ],
        });

        setCurrentStep(0);
      } catch (error) {
        console.error('Failed to fetch execution:', error);
        toast.error('Failed to load execution data');
      } finally {
        setIsLoading(false);
      }
    };

    fetchExecution();
  }, [isOpen, executionId, toast]);

  // Auto-play functionality
  useEffect(() => {
    if (!isPlaying || !execution) return;

    const interval = setInterval(() => {
      setCurrentStep((prev) => {
        if (prev >= execution.totalSteps - 1) {
          setIsPlaying(false);
          return prev;
        }
        return prev + 1;
      });
    }, 1000 / playbackSpeed);

    return () => clearInterval(interval);
  }, [isPlaying, execution, playbackSpeed]);

  const handlePlayPause = () => {
    if (currentStep >= (execution?.totalSteps || 0) - 1) {
      setCurrentStep(0);
      setIsPlaying(true);
    } else {
      setIsPlaying(!isPlaying);
    }
  };

  const handleStepForward = () => {
    if (!execution) return;
    setCurrentStep((prev) => Math.min(prev + 1, execution.totalSteps - 1));
    setIsPlaying(false);
  };

  const handleStepBackward = () => {
    setCurrentStep((prev) => Math.max(prev - 1, 0));
    setIsPlaying(false);
  };

  const handleReset = () => {
    setCurrentStep(0);
    setIsPlaying(false);
  };

  const handleStepClick = (_step: ReplayStep, index: number) => {
    setCurrentStep(index);
    setIsPlaying(false);
  };

  const handleReplay = async () => {
    try {
      // TODO: Call API to replay execution
      // const response = await replayExecution(executionId);

      toast.info('Starting execution replay...');
      handleReset();
      setIsPlaying(true);

      // Simulate replay completion
      setTimeout(() => {
        toast.success('Execution replay completed!');
      }, (execution?.totalSteps || 0) * 1000 / playbackSpeed);
    } catch (error) {
      console.error('Failed to replay execution:', error);
      toast.error('Failed to replay execution');
    }
  };

  const getStatusIcon = (status: ReplayStep['status']) => {
    switch (status) {
      case 'completed':
        return <CheckCircle className="w-4 h-4 text-green-600" />;
      case 'failed':
        return <XCircle className="w-4 h-4 text-red-600" />;
      case 'running':
        return <div className="w-4 h-4 border-2 border-blue-600 border-t-transparent rounded-full animate-spin" />;
      default:
        return <AlertCircle className="w-4 h-4 text-gray-400" />;
    }
  };

  const getStatusColor = (status: ReplayStep['status']) => {
    switch (status) {
      case 'completed':
        return 'bg-green-100 text-green-700 border-green-200';
      case 'failed':
        return 'bg-red-100 text-red-700 border-red-200';
      case 'running':
        return 'bg-blue-100 text-blue-700 border-blue-200';
      default:
        return 'bg-gray-100 text-gray-700 border-gray-200';
    }
  };

  if (!isOpen) return null;

  const currentStepData = execution?.steps[currentStep];

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-6xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Execution Replay</h2>
            <p className="text-sm text-gray-500 mt-1">
              {execution?.workflowName || 'Loading...'}
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
        <div className="flex-1 overflow-hidden flex">
          {isLoading ? (
            <div className="flex-1 flex items-center justify-center">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-loco-primary"></div>
            </div>
          ) : (
            <>
              {/* Timeline Panel */}
              <div className="w-80 border-r border-gray-200 flex flex-col">
                <div className="p-4 border-b border-gray-200">
                  <h3 className="font-semibold text-gray-900 mb-2">Execution Steps</h3>
                  <div className="flex items-center gap-2 text-xs text-gray-600">
                    <Clock className="w-3 h-3" />
                    <span>Step {currentStep + 1} of {execution?.totalSteps || 0}</span>
                  </div>
                </div>

                <div className="flex-1 overflow-y-auto p-4">
                  <div className="space-y-2">
                    {execution?.steps.map((step, index) => (
                      <button
                        key={step.nodeId}
                        onClick={() => handleStepClick(step, index)}
                        className={`w-full text-left p-3 rounded-lg border-2 transition-all ${
                          index === currentStep
                            ? 'border-loco-primary bg-blue-50'
                            : index < currentStep
                            ? 'border-gray-200 bg-gray-50'
                            : 'border-gray-200 bg-white'
                        }`}
                      >
                        <div className="flex items-center gap-2 mb-2">
                          {getStatusIcon(index < currentStep ? step.status : index === currentStep ? 'running' : 'pending')}
                          <span className="font-medium text-sm text-gray-900">
                            {step.nodeName}
                          </span>
                        </div>
                        <div className="text-xs text-gray-600">
                          {step.nodeType}
                          {step.duration && ` • ${step.duration}ms`}
                        </div>
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Details Panel */}
              <div className="flex-1 flex flex-col">
                {currentStepData ? (
                  <>
                    <div className="p-6 border-b border-gray-200">
                      <div className="flex items-center justify-between mb-4">
                        <div>
                          <h3 className="text-lg font-semibold text-gray-900">
                            {currentStepData.nodeName}
                          </h3>
                          <p className="text-sm text-gray-600 mt-1">
                            {currentStepData.nodeType}
                          </p>
                        </div>
                        <span className={`px-3 py-1 rounded-full text-xs font-medium border ${getStatusColor(currentStepData.status)}`}>
                          {currentStepData.status}
                        </span>
                      </div>

                      {currentStepData.duration && (
                        <div className="flex items-center gap-2 text-sm text-gray-600">
                          <Clock className="w-4 h-4" />
                          <span>Duration: {currentStepData.duration}ms</span>
                        </div>
                      )}
                    </div>

                    <div className="flex-1 overflow-y-auto p-6 space-y-6">
                      {/* Input */}
                      {currentStepData.input && (
                        <div>
                          <h4 className="font-medium text-gray-900 mb-2 flex items-center gap-2">
                            <ChevronRight className="w-4 h-4 text-blue-600" />
                            Input Data
                          </h4>
                          <pre className="bg-gray-50 p-4 rounded-lg text-xs overflow-x-auto border border-gray-200">
                            {JSON.stringify(currentStepData.input, null, 2)}
                          </pre>
                        </div>
                      )}

                      {/* Output */}
                      {currentStepData.output && (
                        <div>
                          <h4 className="font-medium text-gray-900 mb-2 flex items-center gap-2">
                            <ChevronRight className="w-4 h-4 text-green-600" />
                            Output Data
                          </h4>
                          <pre className="bg-green-50 p-4 rounded-lg text-xs overflow-x-auto border border-green-200">
                            {JSON.stringify(currentStepData.output, null, 2)}
                          </pre>
                        </div>
                      )}

                      {/* Error */}
                      {currentStepData.error && (
                        <div>
                          <h4 className="font-medium text-gray-900 mb-2 flex items-center gap-2">
                            <XCircle className="w-4 h-4 text-red-600" />
                            Error Details
                          </h4>
                          <div className="bg-red-50 p-4 rounded-lg border border-red-200">
                            <p className="text-sm text-red-700">{currentStepData.error}</p>
                          </div>
                        </div>
                      )}

                      {/* Timestamps */}
                      {currentStepData.startTime && (
                        <div>
                          <h4 className="font-medium text-gray-900 mb-2">Timestamps</h4>
                          <div className="bg-gray-50 p-4 rounded-lg border border-gray-200 space-y-2 text-sm">
                            <div className="flex justify-between">
                              <span className="text-gray-600">Started:</span>
                              <span className="font-mono text-gray-900">
                                {new Date(currentStepData.startTime).toLocaleString()}
                              </span>
                            </div>
                            {currentStepData.endTime && (
                              <div className="flex justify-between">
                                <span className="text-gray-600">Ended:</span>
                                <span className="font-mono text-gray-900">
                                  {new Date(currentStepData.endTime).toLocaleString()}
                                </span>
                              </div>
                            )}
                          </div>
                        </div>
                      )}
                    </div>
                  </>
                ) : (
                  <div className="flex-1 flex items-center justify-center text-gray-500">
                    Select a step to view details
                  </div>
                )}
              </div>
            </>
          )}
        </div>

        {/* Playback Controls */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <button
                onClick={handleReset}
                className="p-2 text-gray-700 hover:bg-gray-200 rounded-lg transition-colors"
                title="Reset"
                disabled={isLoading}
              >
                <RotateCcw className="w-5 h-5" />
              </button>

              <button
                onClick={handleStepBackward}
                className="p-2 text-gray-700 hover:bg-gray-200 rounded-lg transition-colors"
                title="Previous Step"
                disabled={currentStep === 0 || isLoading}
              >
                <SkipBack className="w-5 h-5" />
              </button>

              <button
                onClick={handlePlayPause}
                className="p-3 bg-loco-primary text-white hover:bg-blue-700 rounded-lg transition-colors"
                title={isPlaying ? 'Pause' : 'Play'}
                disabled={isLoading}
              >
                {isPlaying ? <Pause className="w-5 h-5" /> : <Play className="w-5 h-5" />}
              </button>

              <button
                onClick={handleStepForward}
                className="p-2 text-gray-700 hover:bg-gray-200 rounded-lg transition-colors"
                title="Next Step"
                disabled={currentStep >= (execution?.totalSteps || 0) - 1 || isLoading}
              >
                <SkipForward className="w-5 h-5" />
              </button>

              <div className="ml-4 flex items-center gap-2">
                <FastForward className="w-4 h-4 text-gray-600" />
                <select
                  value={playbackSpeed}
                  onChange={(e) => setPlaybackSpeed(Number(e.target.value) as PlaybackSpeed)}
                  className="px-2 py-1 text-sm border border-gray-300 rounded bg-white"
                >
                  <option value={0.5}>0.5x</option>
                  <option value={1}>1x</option>
                  <option value={2}>2x</option>
                  <option value={4}>4x</option>
                </select>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <button
                onClick={handleReplay}
                className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors"
                disabled={isLoading}
              >
                Replay Execution
              </button>
              <button
                onClick={onClose}
                className="px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 transition-colors"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
