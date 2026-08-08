import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  logger,
  getRecentLogs,
  clearLogs,
  exportLogsAsJson,
  createComponentLogger,
} from './logger';

describe('logger', () => {
  beforeEach(() => {
    clearLogs();
    vi.spyOn(console, 'info').mockImplementation(() => {});
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(console, 'debug').mockImplementation(() => {});
  });
  afterEach(() => {
    vi.restoreAllMocks();
    clearLogs();
  });

  it('buffers info/warn/error entries and echoes to the console', () => {
    logger.info('hello', { a: 1 }, 'Comp');
    logger.warn('careful');
    logger.error('boom');

    expect(console.info).toHaveBeenCalledOnce();
    expect(console.warn).toHaveBeenCalledOnce();
    expect(console.error).toHaveBeenCalledOnce();

    const logs = getRecentLogs();
    expect(logs.map((l) => l.level)).toEqual(['info', 'warn', 'error']);
    expect(logs[0].component).toBe('Comp');
    expect(logs[0].data).toEqual({ a: 1 });
  });

  it('wraps a non-Error error argument into an Error with name/stack data', () => {
    logger.error('failed', 'a string reason');
    const [entry] = getRecentLogs();
    expect(entry.error).toBeInstanceOf(Error);
    expect(entry.error?.message).toBe('a string reason');
    expect((entry.data as { name: string }).name).toBe('Error');
  });

  it('getRecentLogs returns only the last N', () => {
    for (let i = 0; i < 5; i++) logger.info(`m${i}`);
    expect(getRecentLogs(2).map((l) => l.message)).toEqual(['m3', 'm4']);
  });

  it('clearLogs empties the buffer', () => {
    logger.info('x');
    expect(getRecentLogs()).toHaveLength(1);
    clearLogs();
    expect(getRecentLogs()).toHaveLength(0);
  });

  it('exportLogsAsJson returns a JSON array of the buffered entries', () => {
    logger.info('one');
    const parsed = JSON.parse(exportLogsAsJson());
    expect(Array.isArray(parsed)).toBe(true);
    expect(parsed[0].message).toBe('one');
  });

  it('createComponentLogger stamps the component name onto every entry', () => {
    const log = createComponentLogger('PropertyPanel');
    log.info('changed');
    log.warn('hmm');
    const logs = getRecentLogs();
    expect(logs.every((l) => l.component === 'PropertyPanel')).toBe(true);
  });
});
