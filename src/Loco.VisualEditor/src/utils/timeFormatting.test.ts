import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  formatRelativeTime,
  formatDuration,
  isToday,
  isThisWeek,
} from './timeFormatting';

// Anchor "now" so the relative-time and today/week checks are deterministic
const NOW = new Date('2026-07-22T12:00:00.000Z');

describe('timeFormatting', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  describe('formatRelativeTime', () => {
    const ago = (ms: number) => new Date(NOW.getTime() - ms);

    it('reports "just now" for sub-second differences', () => {
      expect(formatRelativeTime(ago(500))).toBe('just now');
    });
    it('reports seconds, minutes, hours, and days', () => {
      expect(formatRelativeTime(ago(5_000))).toBe('5s ago');
      expect(formatRelativeTime(ago(5 * 60_000))).toBe('5m ago');
      expect(formatRelativeTime(ago(3 * 3_600_000))).toBe('3h ago');
      expect(formatRelativeTime(ago(2 * 86_400_000))).toBe('2d ago');
    });
    it('falls back to a locale date beyond a week', () => {
      const result = formatRelativeTime(ago(10 * 86_400_000));
      expect(result).not.toMatch(/ago/);
    });
  });

  describe('formatDuration', () => {
    it('scales ms -> s -> m -> h', () => {
      expect(formatDuration(500)).toBe('500ms');
      expect(formatDuration(2000)).toBe('2s');
      expect(formatDuration(90_000)).toBe('2m'); // 90s rounds to 2m
      expect(formatDuration(3_600_000)).toBe('1h'); // 60m is not < 60, so rolls to 1h
      expect(formatDuration(4 * 3_600_000)).toBe('4h');
    });
  });

  describe('isToday', () => {
    it('is true for a timestamp on the same calendar day', () => {
      expect(isToday(new Date('2026-07-22T01:00:00.000Z'))).toBe(true);
    });
    it('is false for another day', () => {
      expect(isToday(new Date('2026-07-21T23:59:00.000Z'))).toBe(false);
    });
  });

  describe('isThisWeek', () => {
    it('is true within the last 7 days and false beyond', () => {
      expect(isThisWeek(new Date(NOW.getTime() - 3 * 86_400_000))).toBe(true);
      expect(isThisWeek(new Date(NOW.getTime() - 8 * 86_400_000))).toBe(false);
    });
  });
});
