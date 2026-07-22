import { describe, it, expect } from 'vitest';
import {
  formatBytes,
  formatJSON,
  parseJSON,
  truncateString,
  capitalizeFirst,
  capitalize,
  toKebabCase,
  toCamelCase,
  escapeHtml,
  unescapeHtml,
} from './dataFormatting';

describe('dataFormatting', () => {
  describe('formatBytes', () => {
    it('formats zero and scales by 1024', () => {
      expect(formatBytes(0)).toBe('0 Bytes');
      expect(formatBytes(1024)).toBe('1 KB');
      expect(formatBytes(1024 * 1024)).toBe('1 MB');
      expect(formatBytes(1536)).toBe('1.5 KB');
    });
    it('honors the decimals argument', () => {
      expect(formatBytes(1536, 0)).toBe('2 KB');
      expect(formatBytes(1536, 1)).toBe('1.5 KB');
    });
  });

  describe('formatJSON / parseJSON', () => {
    it('pretty-prints and round-trips', () => {
      const obj = { a: 1, b: [2, 3] };
      const text = formatJSON(obj);
      expect(text).toContain('\n');
      expect(parseJSON(text)).toEqual(obj);
    });
    it('parseJSON throws a clear error on invalid input', () => {
      expect(() => parseJSON('{nope')).toThrow(/Invalid JSON/);
    });
  });

  describe('truncateString', () => {
    it('leaves short strings untouched', () => {
      expect(truncateString('hello', 10)).toBe('hello');
    });
    it('truncates and appends the suffix within the max length', () => {
      expect(truncateString('hello world', 8)).toBe('hello...');
      expect(truncateString('hello world', 8)).toHaveLength(8);
    });
  });

  describe('capitalize helpers', () => {
    it('capitalizeFirst upcases only the first char and handles empty', () => {
      expect(capitalizeFirst('hello')).toBe('Hello');
      expect(capitalizeFirst('')).toBe('');
    });
    it('capitalize upcases the whole string', () => {
      expect(capitalize('hello')).toBe('HELLO');
    });
  });

  describe('case conversion', () => {
    it('toKebabCase splits camelCase boundaries', () => {
      expect(toKebabCase('camelCase')).toBe('camel-case');
      // Characterization: a leading capital yields a leading dash (current behavior)
      expect(toKebabCase('MyLongName')).toBe('-my-long-name');
    });
    it('toCamelCase strips spaces (characterization of current behavior)', () => {
      // The current implementation lowercases and removes whitespace rather than
      // upper-casing word boundaries; captured here to catch unintended changes.
      expect(toCamelCase('hello world')).toBe('helloworld');
    });
  });

  describe('escapeHtml / unescapeHtml', () => {
    it('escapes the five special characters', () => {
      expect(escapeHtml('<a href="x">&\'</a>')).toBe(
        '&lt;a href=&quot;x&quot;&gt;&amp;&#039;&lt;/a&gt;'
      );
    });
    it('unescapes the named entities (but not the numeric apostrophe)', () => {
      // unescapeHtml only matches /&[a-z]+;/i, so &#039; is left as-is - a known
      // asymmetry with escapeHtml. Documented so a future fix is a deliberate change.
      expect(unescapeHtml('&lt;b&gt;&amp;&quot;')).toBe('<b>&"');
      expect(unescapeHtml('&#039;')).toBe('&#039;');
    });
  });
});
