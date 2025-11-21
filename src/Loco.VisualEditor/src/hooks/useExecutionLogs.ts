import { useState, useCallback, useMemo } from 'react';
import { LogEntry, LogLevel, filterLogsByLevel, filterLogsByNode, groupLogsByLevel, searchLogs, getLogStats, truncateLogs } from '@/utils/logFormatting';

interface UseExecutionLogsOptions {
  maxLogs?: number;
  onLogAdded?: (log: LogEntry) => void;
}

interface UseExecutionLogsReturn {
  logs: LogEntry[];
  addLog: (level: LogLevel, message: string, nodeId?: string, nodeName?: string, data?: unknown) => void;
  addLogs: (newLogs: LogEntry[]) => void;
  clearLogs: () => void;
  filterByLevel: (level: LogLevel) => LogEntry[];
  filterByNode: (nodeId: string) => LogEntry[];
  filterBySearch: (query: string) => LogEntry[];
  groupByLevel: () => Record<LogLevel, LogEntry[]>;
  getStats: () => Record<LogLevel, number>;
  getErrorCount: () => number;
  getWarningCount: () => number;
  hasErrors: () => boolean;
  hasWarnings: () => boolean;
}

export function useExecutionLogs(options: UseExecutionLogsOptions = {}): UseExecutionLogsReturn {
  const { maxLogs = 500, onLogAdded } = options;
  const [logs, setLogs] = useState<LogEntry[]>([]);

  const addLog = useCallback(
    (level: LogLevel, message: string, nodeId?: string, nodeName?: string, data?: unknown) => {
      const log: LogEntry = {
        timestamp: new Date().toISOString(),
        level,
        message,
        nodeId,
        nodeName,
        data,
      };

      setLogs((prev) => truncateLogs([log, ...prev], maxLogs));
      onLogAdded?.(log);
    },
    [maxLogs, onLogAdded]
  );

  const addLogs = useCallback(
    (newLogs: LogEntry[]) => {
      setLogs((prev) => truncateLogs([...newLogs, ...prev], maxLogs));
    },
    [maxLogs]
  );

  const clearLogs = useCallback(() => {
    setLogs([]);
  }, []);

  // Compute counts and filters based on current logs
  const { errorCount, warningCount } = useMemo(() => {
    return {
      errorCount: filterLogsByLevel(logs, 'error').length,
      warningCount: filterLogsByLevel(logs, 'warn').length,
    };
  }, [logs]);

  // Memoize return object to prevent unnecessary recreation
  return useMemo(
    () => ({
      logs,
      addLog,
      addLogs,
      clearLogs,
      filterByLevel: (level: LogLevel) => filterLogsByLevel(logs, level),
      filterByNode: (nodeId: string) => filterLogsByNode(logs, nodeId),
      filterBySearch: (query: string) => searchLogs(logs, query),
      groupByLevel: () => groupLogsByLevel(logs),
      getStats: () => getLogStats(logs),
      getErrorCount: () => errorCount,
      getWarningCount: () => warningCount,
      hasErrors: () => errorCount > 0,
      hasWarnings: () => warningCount > 0,
    }),
    [logs, addLog, addLogs, clearLogs, errorCount, warningCount]
  );
}
