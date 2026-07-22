import { describe, it, expect } from 'vitest';
import {
  validateEnvKey,
  validateEnvValue,
  isDuplicateEnvKey,
  normalizeEnvKey,
  parseEnvValue,
  formatEnvValue,
  maskSecret,
  sortEnvVariables,
  validateEnvVariables,
  type EnvironmentVariable,
} from './environmentVariableValidation';

describe('environmentVariableValidation', () => {
  describe('validateEnvKey', () => {
    it('requires a value', () => {
      expect(validateEnvKey('')).toBe('Key is required');
    });
    it('accepts UPPER_SNAKE_CASE keys', () => {
      expect(validateEnvKey('API_KEY')).toBeNull();
      expect(validateEnvKey('_PRIVATE')).toBeNull();
      expect(validateEnvKey('PORT_2')).toBeNull();
    });
    it('rejects lowercase, leading digits, and punctuation', () => {
      expect(validateEnvKey('api_key')).not.toBeNull();
      expect(validateEnvKey('2FA')).not.toBeNull();
      expect(validateEnvKey('MY-KEY')).not.toBeNull();
    });
    it('rejects keys longer than the max length', () => {
      expect(validateEnvKey('A'.repeat(257))).toBe('Key must be at most 256 characters');
    });
  });

  describe('validateEnvValue', () => {
    it('requires a value and accepts anything non-empty', () => {
      expect(validateEnvValue('')).toBe('Value is required');
      expect(validateEnvValue('x')).toBeNull();
    });
  });

  describe('isDuplicateEnvKey', () => {
    it('detects a duplicate, honoring the exclude key (self-edit)', () => {
      expect(isDuplicateEnvKey('A', ['A', 'B'])).toBe(true);
      expect(isDuplicateEnvKey('C', ['A', 'B'])).toBe(false);
      // Editing the existing 'A' should not flag itself
      expect(isDuplicateEnvKey('A', ['A', 'B'], 'A')).toBe(false);
    });
  });

  describe('normalizeEnvKey', () => {
    it('uppercases and replaces invalid characters with underscores', () => {
      expect(normalizeEnvKey('my-api key')).toBe('MY_API_KEY');
      expect(normalizeEnvKey('already_ok')).toBe('ALREADY_OK');
    });
  });

  describe('parseEnvValue', () => {
    it('parses JSON objects and arrays', () => {
      expect(parseEnvValue('{"a":1}')).toEqual({ a: 1 });
      expect(parseEnvValue('[1,2]')).toEqual([1, 2]);
    });
    it('falls back to string for malformed JSON', () => {
      expect(parseEnvValue('{not json')).toBe('{not json');
    });
    it('coerces booleans, null, and numbers (case-insensitively)', () => {
      expect(parseEnvValue('true')).toBe(true);
      expect(parseEnvValue('FALSE')).toBe(false);
      expect(parseEnvValue('null')).toBeNull();
      expect(parseEnvValue('42')).toBe(42);
      expect(parseEnvValue('3.14')).toBe(3.14);
    });
    it('leaves plain strings and empty input as strings', () => {
      expect(parseEnvValue('hello')).toBe('hello');
      expect(parseEnvValue('')).toBe('');
    });
  });

  describe('formatEnvValue', () => {
    it('round-trips the primitive kinds parseEnvValue produces', () => {
      expect(formatEnvValue('hi')).toBe('hi');
      expect(formatEnvValue(true)).toBe('true');
      expect(formatEnvValue(false)).toBe('false');
      expect(formatEnvValue(null)).toBe('null');
      expect(formatEnvValue(42)).toBe('42');
      expect(formatEnvValue({ a: 1 })).toBe('{\n  "a": 1\n}');
    });
  });

  describe('maskSecret', () => {
    it('shows the first N characters and masks the rest', () => {
      expect(maskSecret('supersecret')).toBe('supe*******');
      expect(maskSecret('supersecret', 2)).toBe('su*********');
    });
    it('fully masks values shorter than the visible window', () => {
      expect(maskSecret('abc')).toBe('***');
      expect(maskSecret('ab', 4)).toBe('**');
    });
  });

  describe('sortEnvVariables', () => {
    it('sorts by key without mutating the input', () => {
      const input: EnvironmentVariable[] = [
        { key: 'B', value: '1' },
        { key: 'A', value: '2' },
      ];
      const sorted = sortEnvVariables(input);
      expect(sorted.map((e) => e.key)).toEqual(['A', 'B']);
      expect(input.map((e) => e.key)).toEqual(['B', 'A']); // untouched
    });
  });

  describe('validateEnvVariables', () => {
    it('collects key, value, and duplicate errors keyed by field', () => {
      const errors = validateEnvVariables([
        { key: 'GOOD', value: 'x' },
        { key: 'bad key', value: '' },
        { key: 'GOOD', value: 'y' }, // duplicate of the first
      ]);

      expect(errors['GOOD-key']).toBeUndefined();
      expect(errors['bad key-key']).toBeTruthy();
      expect(errors['bad key-value']).toBe('Value is required');
      expect(errors['GOOD-duplicate']).toBe('Duplicate key');
    });

    it('returns no errors for a clean set', () => {
      const errors = validateEnvVariables([
        { key: 'A', value: '1' },
        { key: 'B', value: '2' },
      ]);
      expect(Object.keys(errors)).toHaveLength(0);
    });
  });
});
