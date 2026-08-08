import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  errorLogger,
  logNetworkError,
  logApiError,
  logValidationError,
  logWorkflowError,
  logCriticalError,
} from './errorLogger';

describe('errorLogger', () => {
  beforeEach(() => {
    errorLogger.clearErrors();
    // Silence the grouped console output the logger emits
    errorLogger.setConsoleLogging(false);
  });
  afterEach(() => {
    errorLogger.clearErrors();
    errorLogger.setConsoleLogging(true);
    vi.restoreAllMocks();
  });

  it('log() returns a unique id and stores the error with defaults', () => {
    const id = errorLogger.log('something went wrong');
    expect(id).toMatch(/^err-/);

    const stored = errorLogger.getErrorById(id);
    expect(stored?.message).toBe('something went wrong');
    expect(stored?.severity).toBe('medium'); // default
    expect(stored?.category).toBe('unknown'); // default
  });

  it('records severity, category, error, and context from options', () => {
    const err = new Error('kaboom');
    const id = errorLogger.log('failure', {
      error: err,
      severity: 'critical',
      category: 'workflow',
      context: { nodeId: 'n1' },
    });

    const stored = errorLogger.getErrorById(id)!;
    expect(stored.severity).toBe('critical');
    expect(stored.category).toBe('workflow');
    expect(stored.error).toBe(err);
    expect(stored.stackTrace).toBe(err.stack);
    expect(stored.context).toEqual({ nodeId: 'n1' });
  });

  it('getErrors filters by severity, category, and limit', () => {
    errorLogger.log('a', { severity: 'low', category: 'validation' });
    errorLogger.log('b', { severity: 'high', category: 'network' });
    errorLogger.log('c', { severity: 'high', category: 'api' });

    expect(errorLogger.getErrors({ severity: 'high' })).toHaveLength(2);
    expect(errorLogger.getErrors({ category: 'validation' })).toHaveLength(1);
    expect(errorLogger.getErrors({ limit: 1 })[0].message).toBe('c'); // last N
  });

  it('clearErrors empties storage and exportErrors returns JSON', () => {
    errorLogger.log('x');
    expect(JSON.parse(errorLogger.exportErrors())).toHaveLength(1);

    errorLogger.clearErrors();
    expect(errorLogger.getErrors()).toHaveLength(0);
    expect(JSON.parse(errorLogger.exportErrors())).toHaveLength(0);
  });

  describe('convenience functions set the right severity/category', () => {
    const cases: Array<[string, () => string, string, string]> = [
      ['network', () => logNetworkError('n'), 'high', 'network'],
      ['api', () => logApiError('a'), 'medium', 'api'],
      ['validation', () => logValidationError('v'), 'low', 'validation'],
      ['workflow', () => logWorkflowError('w'), 'high', 'workflow'],
      ['critical', () => logCriticalError('c'), 'critical', 'unknown'],
    ];

    it.each(cases)('log%s -> %s severity, %s category', (_name, fn, severity, category) => {
      const id = fn();
      const stored = errorLogger.getErrorById(id)!;
      expect(stored.severity).toBe(severity);
      expect(stored.category).toBe(category);
    });
  });
});
