import { describe, it, expect } from 'vitest';
import {
  validateEmail,
  validateUrl,
  validateServerUrl,
  validateNonEmpty,
  validateNumber,
} from './formValidation';

describe('formValidation', () => {
  describe('validateEmail', () => {
    it('requires a value', () => {
      expect(validateEmail('')).toBe('Email is required');
    });
    it('accepts a well-formed address', () => {
      expect(validateEmail('a@b.co')).toBeNull();
      expect(validateEmail('first.last@example.com')).toBeNull();
    });
    it('rejects malformed addresses', () => {
      expect(validateEmail('not-an-email')).toBe('Invalid email format');
      expect(validateEmail('a@b')).toBe('Invalid email format');
      expect(validateEmail('a b@c.com')).toBe('Invalid email format');
    });
  });

  describe('validateUrl', () => {
    it('requires a value', () => {
      expect(validateUrl('')).toBe('URL is required');
    });
    it('accepts http(s) and bare domains', () => {
      expect(validateUrl('https://example.com')).toBeNull();
      expect(validateUrl('http://example.com/path')).toBeNull();
      expect(validateUrl('example.com')).toBeNull();
    });
    it('rejects clearly invalid input', () => {
      expect(validateUrl('not a url at all !!')).toBe('Invalid URL format');
    });
  });

  describe('validateServerUrl', () => {
    it('requires a value', () => {
      expect(validateServerUrl('')).toBe('Server URL is required');
    });
    it('accepts anything the URL constructor parses', () => {
      expect(validateServerUrl('http://localhost:5000')).toBeNull();
      expect(validateServerUrl('wss://collab.example.com/ws')).toBeNull();
    });
    it('rejects strings the URL constructor cannot parse', () => {
      // The WHATWG URL parser is lenient (it accepts e.g. "localhost:5000" as
      // scheme "localhost"), so only genuinely unparseable input is rejected
      expect(validateServerUrl('not a url')).toBe('Invalid URL format');
      expect(validateServerUrl('foo bar baz')).toBe('Invalid URL format');
    });
  });

  describe('validateNonEmpty', () => {
    it('rejects empty and whitespace-only values with the field name', () => {
      expect(validateNonEmpty('', 'Name')).toBe('Name is required');
      expect(validateNonEmpty('   ', 'Name')).toBe('Name is required');
    });
    it('accepts any non-blank value', () => {
      expect(validateNonEmpty('x', 'Name')).toBeNull();
    });
  });

  describe('validateNumber', () => {
    it('parses numeric strings and plain numbers', () => {
      expect(validateNumber('42')).toBeNull();
      expect(validateNumber(42)).toBeNull();
    });
    it('rejects non-numeric input', () => {
      expect(validateNumber('abc')).toBe('Must be a number');
    });
    it('enforces min and max bounds', () => {
      expect(validateNumber(5, 10)).toBe('Must be at least 10');
      expect(validateNumber(15, 0, 10)).toBe('Must be at most 10');
      expect(validateNumber(5, 0, 10)).toBeNull();
    });
    it('treats the bounds as inclusive', () => {
      expect(validateNumber(10, 10, 10)).toBeNull();
    });
  });
});
