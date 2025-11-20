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

  const filterByLevelMemo = useCallback(
    (level: LogLevel): LogEntry[] => {
      return filterLogsByLevel(logs, level);
    },
    [logs]
  );

  const filterByNodeMemo = useCallback(
    (nodeId: string): LogEntry[] => {
      return filterLogsByNode(logs, nodeId);
    },
    [logs]
  );

  const filterBySearchMemo = useCallback(
    (query: string): LogEntry[] => {
      return searchLogs(logs, query);
    },
    [logs]
  );

  const groupByLevelMemo = useCallback((): Record<LogLevel, LogEntry[]> => {
    return groupLogsByLevel(logs);
  }, [logs]);

  const getStatsMemo = useCallback((): Record<LogLevel, number> => {
    return getLogStats(logs);
  }, [logs]);

  const getErrorCount = useCallback((): number => {
    return filterLogsByLevel(logs, 'error').length;
  }, [logs]);

  const getWarningCount = useCallback((): number => {
    return filterLogsByLevel(logs, 'warn').length;
  }, [logs]);

  const hasErrors = useCallback((): boolean => {
    return getErrorCount() > 0;
  }, [getErrorCount]);

  const hasWarnings = useCallback((): boolean => {
    return getWarningCount() > 0;
  }, [getWarningCount]);

  // Memoize counts for efficient access
  const errorCount = useMemo(() => getErrorCount(), [getErrorCount]);
  const warningCount = useMemo(() => getWarningCount(), [getWarningCount]);

  return {
    logs,
    addLog,
    addLogs,
    clearLogs,
    filterByLevel: filterByLevelMemo,
    filterByNode: filterByNodeMemo,
    filterBySearch: filterBySearchMemo,
    groupByLevel: groupByLevelMemo,
    getStats: getStatsMemo,
    getErrorCount: () => errorCount,
    getWarningCount: () => warningCount,
    hasErrors,
    hasWarnings,
  };
}
