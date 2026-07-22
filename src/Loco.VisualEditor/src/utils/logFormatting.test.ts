import { describe, it, expect } from 'vitest';
import {
  formatLogLevel,
  formatLogEntry,
  filterLogsByLevel,
  filterLogsByNode,
  groupLogsByNode,
  groupLogsByLevel,
  searchLogs,
  getLogStats,
  truncateLogs,
  type LogEntry,
  type LogLevel,
} from './logFormatting';

const entry = (over: Partial<LogEntry> = {}): LogEntry => ({
  timestamp: '2026-07-22T12:00:00.000Z',
  level: 'info',
  message: 'hello',
  ...over,
});

describe('logFormatting', () => {
  describe('formatLogLevel', () => {
    it('maps each level to an upper-case label', () => {
      expect(formatLogLevel('debug')).toBe('DEBUG');
      expect(formatLogLevel('error')).toBe('ERROR');
      expect(formatLogLevel('success')).toBe('SUCCESS');
    });
    it('falls back to UNKNOWN for an unexpected level', () => {
      expect(formatLogLevel('nope' as LogLevel)).toBe('UNKNOWN');
    });
  });

  describe('formatLogEntry', () => {
    it('includes level, node name, message, and serialized data', () => {
      const s = formatLogEntry(entry({ level: 'warn', nodeName: 'N1', message: 'oops', data: { a: 1 } }));
      expect(s).toContain('WARN');
      expect(s).toContain('[N1]');
      expect(s).toContain('oops');
      expect(s).toContain('{"a":1}');
    });
    it('omits the node and data segments when absent', () => {
      const s = formatLogEntry(entry({ message: 'plain' }));
      expect(s).toContain('plain');
      expect(s).not.toContain(' - '); // no data segment
      // The only bracketed segment is the timestamp; no [NodeName] segment
      expect(s.match(/\[/g) ?? []).toHaveLength(1);
    });
  });

  describe('filterLogsByLevel', () => {
    const logs = [
      entry({ level: 'debug', message: 'd' }),
      entry({ level: 'info', message: 'i' }),
      entry({ level: 'warn', message: 'w' }),
      entry({ level: 'error', message: 'e' }),
    ];
    it('includes entries at or above the threshold', () => {
      expect(filterLogsByLevel(logs, 'warn').map((l) => l.message)).toEqual(['w', 'e']);
      expect(filterLogsByLevel(logs, 'debug')).toHaveLength(4);
    });
    it('treats info as the threshold that drops debug', () => {
      expect(filterLogsByLevel(logs, 'info').map((l) => l.message)).toEqual(['i', 'w', 'e']);
    });
  });

  describe('filterLogsByNode', () => {
    it('keeps only entries for the given node id', () => {
      const logs = [entry({ nodeId: 'a' }), entry({ nodeId: 'b' }), entry({ nodeId: 'a' })];
      expect(filterLogsByNode(logs, 'a')).toHaveLength(2);
    });
  });

  describe('groupLogsByNode / groupLogsByLevel', () => {
    it('buckets by node name, falling back to id then "unknown"', () => {
      const groups = groupLogsByNode([
        entry({ nodeName: 'Alpha' }),
        entry({ nodeId: 'n2' }),
        entry({}),
      ]);
      expect(Object.keys(groups).sort()).toEqual(['Alpha', 'n2', 'unknown']);
    });
    it('buckets by level', () => {
      const groups = groupLogsByLevel([entry({ level: 'info' }), entry({ level: 'error' }), entry({ level: 'info' })]);
      expect(groups.info).toHaveLength(2);
      expect(groups.error).toHaveLength(1);
    });
  });

  describe('searchLogs', () => {
    const logs = [
      entry({ message: 'Connecting to server', nodeName: 'HTTP' }),
      entry({ message: 'done', data: { url: 'https://api.example.com' } }),
    ];
    it('matches message, node name, and serialized data case-insensitively', () => {
      expect(searchLogs(logs, 'connect')).toHaveLength(1);
      expect(searchLogs(logs, 'http')).toHaveLength(2); // nodeName + data url
      expect(searchLogs(logs, 'example.com')).toHaveLength(1);
      expect(searchLogs(logs, 'zzz')).toHaveLength(0);
    });
  });

  describe('getLogStats', () => {
    it('counts entries per level with all levels present', () => {
      const stats = getLogStats([entry({ level: 'info' }), entry({ level: 'info' }), entry({ level: 'error' })]);
      expect(stats.info).toBe(2);
      expect(stats.error).toBe(1);
      expect(stats.debug).toBe(0);
    });
  });

  describe('truncateLogs', () => {
    it('keeps only the last N entries', () => {
      const logs = Array.from({ length: 5 }, (_, i) => entry({ message: `m${i}` }));
      const kept = truncateLogs(logs, 2);
      expect(kept.map((l) => l.message)).toEqual(['m3', 'm4']);
    });
    it('returns everything when under the limit', () => {
      expect(truncateLogs([entry()], 100)).toHaveLength(1);
    });
  });
});
