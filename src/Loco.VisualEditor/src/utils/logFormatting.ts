export type LogLevel = 'debug' | 'info' | 'warn' | 'error' | 'success';

export interface LogEntry {
  timestamp: string;
  level: LogLevel;
  message: string;
  nodeId?: string;
  nodeName?: string;
  data?: unknown;
}

export function formatLogLevel(level: LogLevel): string {
  const labels: Record<LogLevel, string> = {
    debug: 'DEBUG',
    info: 'INFO',
    warn: 'WARN',
    error: 'ERROR',
    success: 'SUCCESS',
  };
  return labels[level] || 'UNKNOWN';
}

export function formatLogEntry(entry: LogEntry): string {
  const time = new Date(entry.timestamp).toLocaleTimeString();
  const level = formatLogLevel(entry.level);
  const nodeInfo = entry.nodeName ? ` [${entry.nodeName}]` : '';
  const data = entry.data ? ` - ${JSON.stringify(entry.data)}` : '';
  
  return `[${time}] ${level}${nodeInfo}: ${entry.message}${data}`;
}

export function filterLogsByLevel(logs: LogEntry[], level: LogLevel): LogEntry[] {
  const levels: Record<LogLevel, number> = {
    debug: 0,
    info: 1,
    warn: 2,
    error: 3,
    success: 1,
  };

  const selectedLevel = levels[level];
  return logs.filter((log) => levels[log.level] >= selectedLevel);
}

export function filterLogsByNode(logs: LogEntry[], nodeId: string): LogEntry[] {
  return logs.filter((log) => log.nodeId === nodeId);
}

export function groupLogsByNode(logs: LogEntry[]): Record<string, LogEntry[]> {
  return logs.reduce(
    (acc, log) => {
      const key = log.nodeName || log.nodeId || 'unknown';
      if (!acc[key]) acc[key] = [];
      acc[key].push(log);
      return acc;
    },
    {} as Record<string, LogEntry[]>
  );
}

export function groupLogsByLevel(logs: LogEntry[]): Record<LogLevel, LogEntry[]> {
  return logs.reduce(
    (acc, log) => {
      if (!acc[log.level]) acc[log.level] = [];
      acc[log.level].push(log);
      return acc;
    },
    {} as Record<LogLevel, LogEntry[]>
  );
}

export function searchLogs(logs: LogEntry[], query: string): LogEntry[] {
  const lowerQuery = query.toLowerCase();
  return logs.filter(
    (log) =>
      log.message.toLowerCase().includes(lowerQuery) ||
      log.nodeName?.toLowerCase().includes(lowerQuery) ||
      (log.data && JSON.stringify(log.data).toLowerCase().includes(lowerQuery))
  );
}

export function getLogStats(logs: LogEntry[]): Record<LogLevel, number> {
  const stats: Record<LogLevel, number> = {
    debug: 0,
    info: 0,
    warn: 0,
    error: 0,
    success: 0,
  };

  logs.forEach((log) => {
    stats[log.level]++;
  });

  return stats;
}

export function truncateLogs(logs: LogEntry[], maxEntries: number = 100): LogEntry[] {
  return logs.slice(-maxEntries);
}
