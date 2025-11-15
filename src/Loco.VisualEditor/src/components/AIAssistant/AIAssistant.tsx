/**
 * AI Assistant Component
 *
 * Provides interactive AI-powered recommendations and analysis:
 * - Real-time workflow analysis
 * - Optimization suggestions
 * - Error explanations and fixes
 * - Performance recommendations
 * - Security analysis
 */

import { useState, useEffect } from 'react';
import {
  X,
  Send,
  Sparkles,
  AlertCircle,
  TrendingUp,
  Lock,
  Lightbulb,
  ChevronDown,
  ChevronUp,
  Copy,
  Check,
} from 'lucide-react';
import { useWorkflowStore } from '@/store/workflowStore';
import { AIInsight } from '@/utils/aiAnalyzer';
import { useToast } from '@/contexts/ToastContext';
import { COPY_FEEDBACK_DURATION } from '@/utils/constants';

// ============================================================================
// Types
// ============================================================================

interface Message {
  id: string;
  type: 'user' | 'assistant';
  content: string;
  timestamp: number;
  insights?: AIInsight[];
}

interface AIAssistantProps {
  isOpen: boolean;
  onClose: () => void;
}

// ============================================================================
// AI Assistant Component
// ============================================================================

export function AIAssistant({ isOpen, onClose }: AIAssistantProps) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [expandedInsights, setExpandedInsights] = useState<Set<string>>(new Set());
  const [copiedInsightId, setCopiedInsightId] = useState<string | null>(null);
  const { nodes, edges } = useWorkflowStore();
  const toast = useToast();

  // Perform initial analysis when opened
  useEffect(() => {
    if (isOpen && messages.length === 0) {
      performInitialAnalysis();
    }
  }, [isOpen]);

  const performInitialAnalysis = async () => {
    setIsAnalyzing(true);
    try {
      // Use unified analysis engine
      const { analysisEngine } = await import('@/utils/workflowAnalysisEngine');
      const analysisResult = analysisEngine.analyze(nodes, edges);
      const analysis = analysisResult.aiAnalysis;

      const assistantMessage: Message = {
        id: `msg-${Date.now()}`,
        type: 'assistant',
        content: `I've analyzed your workflow and found ${analysis.insights.length} optimization opportunities. ${analysisResult.cacheHit ? '(Using cached analysis)' : ''} Here are my key recommendations:`,
        timestamp: Date.now(),
        insights: analysis.insights.filter((i) => i.priority === 'high').slice(0, 3),
      };

      setMessages([assistantMessage]);
    } catch (error) {
      console.error('Analysis failed:', error);
      toast.error('Failed to analyze workflow');
    } finally {
      setIsAnalyzing(false);
    }
  };

  const handleSendMessage = async () => {
    if (!input.trim()) return;

    const userMessage: Message = {
      id: `msg-${Date.now()}`,
      type: 'user',
      content: input,
      timestamp: Date.now(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setInput('');
    setIsAnalyzing(true);

    try {
      // Use unified analysis engine
      const { analysisEngine } = await import('@/utils/workflowAnalysisEngine');
      const analysisResult = analysisEngine.analyze(nodes, edges);
      const analysis = analysisResult.aiAnalysis;

      let responseContent = '';
      let responseInsights: AIInsight[] = [];

      // Route based on user input
      const inputLower = input.toLowerCase();

      if (
        inputLower.includes('performance') ||
        inputLower.includes('optimize') ||
        inputLower.includes('speed')
      ) {
        responseContent =
          'Here are the main performance optimization opportunities I found:';
        responseInsights = analysis.insights.filter((i) => i.type === 'performance');
      } else if (
        inputLower.includes('security') ||
        inputLower.includes('safe') ||
        inputLower.includes('risk')
      ) {
        responseContent = 'Here are the security concerns I detected:';
        responseInsights = analysis.insights.filter((i) => i.type === 'security');
      } else if (
        inputLower.includes('error') ||
        inputLower.includes('fix') ||
        inputLower.includes('issue')
      ) {
        responseContent = 'Here are the critical issues that need fixing:';
        responseInsights = analysis.insights.filter((i) => i.type === 'error_fix');
      } else if (inputLower.includes('pattern')) {
        responseContent = 'Here are the workflow patterns I detected:';
        responseInsights = analysis.insights.filter((i) => i.type === 'pattern');
      } else {
        responseContent = `I found ${analysis.insights.length} insights about your workflow. Here are the most important ones:`;
        responseInsights = analysis.insights.slice(0, 5);
      }

      const assistantMessage: Message = {
        id: `msg-${Date.now()}`,
        type: 'assistant',
        content:
          responseInsights.length > 0
            ? responseContent
            : "I've analyzed your workflow. What specific aspect would you like me to help with? (e.g., 'performance', 'security', 'errors')",
        timestamp: Date.now(),
        insights: responseInsights.length > 0 ? responseInsights : undefined,
      };

      setMessages((prev) => [...prev, assistantMessage]);
    } catch (error) {
      console.error('Message processing failed:', error);
      const errorMessage: Message = {
        id: `msg-${Date.now()}`,
        type: 'assistant',
        content: 'Sorry, I encountered an error analyzing your workflow. Please try again.',
        timestamp: Date.now(),
      };
      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setIsAnalyzing(false);
    }
  };

  const toggleInsightExpanded = (insightId: string) => {
    setExpandedInsights((prev) => {
      const next = new Set(prev);
      if (next.has(insightId)) {
        next.delete(insightId);
      } else {
        next.add(insightId);
      }
      return next;
    });
  };

  const copyInsightToClipboard = (insight: AIInsight) => {
    const text = `${insight.title}\n${insight.description}\n\n${insight.explanation}\n\nActions:\n${insight.suggestedActions.map((a) => `- ${a.action}: ${a.impact}`).join('\n')}`;
    navigator.clipboard.writeText(text);
    setCopiedInsightId(insight.id);
    setTimeout(() => setCopiedInsightId(null), COPY_FEEDBACK_DURATION);
    toast.success('Insight copied to clipboard');
  };

  const getInsightIcon = (type: string) => {
    switch (type) {
      case 'performance':
        return <TrendingUp className="w-5 h-5 text-blue-600" />;
      case 'security':
        return <Lock className="w-5 h-5 text-red-600" />;
      case 'optimization':
        return <Sparkles className="w-5 h-5 text-purple-600" />;
      case 'pattern':
        return <Lightbulb className="w-5 h-5 text-yellow-600" />;
      default:
        return <AlertCircle className="w-5 h-5 text-gray-600" />;
    }
  };

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'high':
        return 'bg-red-100 text-red-800';
      case 'medium':
        return 'bg-yellow-100 text-yellow-800';
      case 'low':
        return 'bg-blue-100 text-blue-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed bottom-4 right-4 w-96 h-[600px] bg-white rounded-lg shadow-2xl border border-gray-200 z-40 flex flex-col">
      {/* Header */}
      <div className="px-4 py-4 border-b border-gray-200 bg-gradient-to-r from-blue-50 to-purple-50 rounded-t-lg">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Sparkles className="w-5 h-5 text-blue-600" />
            <h3 className="font-semibold text-gray-900">AI Assistant</h3>
          </div>
          <button
            onClick={onClose}
            className="p-1 hover:bg-gray-200 rounded transition-colors"
            title="Close"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>
        <p className="text-xs text-gray-600 mt-2">Intelligent workflow analysis and recommendations</p>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full text-center">
            <Sparkles className="w-12 h-12 text-blue-300 mb-4" />
            <p className="text-sm text-gray-600">
              Tell me what you'd like help with, or ask about performance, security, or workflow
              patterns.
            </p>
          </div>
        ) : (
          messages.map((msg) => (
            <div
              key={msg.id}
              className={`space-y-2 ${msg.type === 'user' ? 'text-right' : 'text-left'}`}
            >
              {/* Message text */}
              <div
                className={`inline-block px-4 py-2 rounded-lg max-w-xs ${
                  msg.type === 'user'
                    ? 'bg-blue-600 text-white rounded-br-none'
                    : 'bg-gray-100 text-gray-900 rounded-bl-none'
                }`}
              >
                <p className="text-sm">{msg.content}</p>
              </div>

              {/* Insights */}
              {msg.insights && msg.insights.length > 0 && (
                <div className="space-y-2 text-left">
                  {msg.insights.map((insight) => (
                    <div
                      key={insight.id}
                      className="bg-gradient-to-r from-blue-50 to-purple-50 rounded-lg border border-blue-200 overflow-hidden"
                    >
                      <button
                        onClick={() => toggleInsightExpanded(insight.id)}
                        className="w-full p-3 hover:bg-blue-100 transition-colors flex items-start justify-between"
                      >
                        <div className="flex items-start gap-2 flex-1 text-left">
                          {getInsightIcon(insight.type)}
                          <div className="flex-1">
                            <div className="flex items-center gap-2">
                              <h4 className="text-sm font-semibold text-gray-900">{insight.title}</h4>
                              <span
                                className={`px-2 py-0.5 text-xs font-medium rounded ${getPriorityColor(
                                  insight.priority
                                )}`}
                              >
                                {insight.priority}
                              </span>
                            </div>
                            <p className="text-xs text-gray-600">{insight.description}</p>
                          </div>
                        </div>
                        {expandedInsights.has(insight.id) ? (
                          <ChevronUp className="w-4 h-4 text-gray-400 mt-1" />
                        ) : (
                          <ChevronDown className="w-4 h-4 text-gray-400 mt-1" />
                        )}
                      </button>

                      {/* Expanded content */}
                      {expandedInsights.has(insight.id) && (
                        <div className="px-3 pb-3 border-t border-blue-200 space-y-2">
                          <p className="text-xs text-gray-700">{insight.explanation}</p>

                          {insight.suggestedActions.length > 0 && (
                            <div className="space-y-1">
                              <p className="text-xs font-semibold text-gray-900">Suggested Actions:</p>
                              {insight.suggestedActions.map((action, idx) => (
                                <div key={idx} className="text-xs text-gray-700 ml-2">
                                  <p className="font-medium">
                                    • {action.action}
                                    <span className="text-gray-500 ml-1">
                                      ({action.difficulty})
                                    </span>
                                  </p>
                                  <p className="text-gray-600 ml-4">💡 {action.impact}</p>
                                </div>
                              ))}
                            </div>
                          )}

                          {insight.estimatedBenefit && (
                            <p className="text-xs text-green-700 font-medium">
                              ✨ {insight.estimatedBenefit}
                            </p>
                          )}

                          <button
                            onClick={() => copyInsightToClipboard(insight)}
                            className="text-xs text-blue-600 hover:text-blue-700 font-medium flex items-center gap-1 mt-2"
                          >
                            {copiedInsightId === insight.id ? (
                              <>
                                <Check className="w-3 h-3" /> Copied
                              </>
                            ) : (
                              <>
                                <Copy className="w-3 h-3" /> Copy
                              </>
                            )}
                          </button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          ))
        )}

        {isAnalyzing && (
          <div className="flex items-center justify-center py-4">
            <div className="animate-spin">
              <Sparkles className="w-5 h-5 text-blue-600" />
            </div>
            <p className="text-sm text-gray-600 ml-2">Analyzing workflow...</p>
          </div>
        )}
      </div>

      {/* Input */}
      <div className="px-4 py-3 border-t border-gray-200 bg-gray-50 rounded-b-lg">
        <div className="flex gap-2">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyPress={(e) => {
              if (e.key === 'Enter' && !isAnalyzing) {
                handleSendMessage();
              }
            }}
            placeholder="Ask about performance, security, errors..."
            className="flex-1 px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:border-blue-500"
            disabled={isAnalyzing}
          />
          <button
            onClick={handleSendMessage}
            disabled={isAnalyzing || !input.trim()}
            className="p-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            title="Send"
          >
            <Send className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
