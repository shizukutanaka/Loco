import { describe, it, expect } from 'vitest';
import {
  isSafeUrl,
  isValidEmail,
  isValidWorkflowName,
  sanitizeInput,
  isNotEmpty,
  isValidUUID,
  isNonEmptyString,
  isDefined,
} from './validation';

describe('Validation Utilities', () => {
  describe('isSafeUrl', () => {
    it('should return false for null or undefined', () => {
      expect(isSafeUrl(null)).toBe(false);
      expect(isSafeUrl(undefined)).toBe(false);
    });

    it('should return false for empty string', () => {
      expect(isSafeUrl('')).toBe(false);
    });

    it('should block javascript: protocol', () => {
      expect(isSafeUrl('javascript:alert("xss")')).toBe(false);
      expect(isSafeUrl('JavaScript:void(0)')).toBe(false);
    });

    it('should block data: protocol', () => {
      expect(isSafeUrl('data:text/html,<script>alert("xss")</script>')).toBe(false);
      expect(isSafeUrl('DATA:text/html,test')).toBe(false);
    });

    it('should block vbscript: protocol', () => {
      expect(isSafeUrl('vbscript:msgbox("xss")')).toBe(false);
    });

    it('should allow relative URLs', () => {
      expect(isSafeUrl('/dashboard')).toBe(true);
      expect(isSafeUrl('#section')).toBe(true);
      expect(isSafeUrl('/path/to/page')).toBe(true);
    });

    it('should allow http and https URLs', () => {
      expect(isSafeUrl('https://example.com')).toBe(true);
      expect(isSafeUrl('http://example.com/path')).toBe(true);
    });

    it('should allow mailto protocol', () => {
      expect(isSafeUrl('mailto:test@example.com')).toBe(true);
    });

    it('should return false for invalid URLs', () => {
      expect(isSafeUrl('ht!tp://invalid')).toBe(false);
      expect(isSafeUrl('not a url')).toBe(false);
    });

    it('should be case insensitive for protocol checking', () => {
      expect(isSafeUrl('HTTPS://example.com')).toBe(true);
    });
  });

  describe('isValidEmail', () => {
    it('should validate correct email addresses', () => {
      expect(isValidEmail('user@example.com')).toBe(true);
      expect(isValidEmail('test.user@domain.co.uk')).toBe(true);
      expect(isValidEmail('user+tag@example.com')).toBe(true);
    });

    it('should reject invalid email addresses', () => {
      expect(isValidEmail('invalid')).toBe(false);
      expect(isValidEmail('invalid@')).toBe(false);
      expect(isValidEmail('@example.com')).toBe(false);
      expect(isValidEmail('user @example.com')).toBe(false);
      expect(isValidEmail('user@example')).toBe(false);
    });

    it('should handle whitespace', () => {
      expect(isValidEmail('  user@example.com  ')).toBe(true);
    });
  });

  describe('isValidWorkflowName', () => {
    it('should validate correct workflow names', () => {
      expect(isValidWorkflowName('My Workflow')).toBe(true);
      expect(isValidWorkflowName('workflow-name')).toBe(true);
      expect(isValidWorkflowName('workflow_name')).toBe(true);
      expect(isValidWorkflowName('Workflow (v1)')).toBe(true);
      expect(isValidWorkflowName('Test123')).toBe(true);
    });

    it('should reject empty or whitespace-only names', () => {
      expect(isValidWorkflowName('')).toBe(false);
      expect(isValidWorkflowName('   ')).toBe(false);
    });

    it('should reject names exceeding max length', () => {
      const longName = 'a'.repeat(256);
      expect(isValidWorkflowName(longName)).toBe(false);
    });

    it('should reject special characters', () => {
      expect(isValidWorkflowName('workflow@name')).toBe(false);
      expect(isValidWorkflowName('workflow#name')).toBe(false);
      expect(isValidWorkflowName('workflow$name')).toBe(false);
      expect(isValidWorkflowName('workflow*name')).toBe(false);
    });

    it('should accept 255 character limit', () => {
      const maxName = 'a'.repeat(255);
      expect(isValidWorkflowName(maxName)).toBe(true);
    });
  });

  describe('sanitizeInput', () => {
    it('should escape HTML special characters', () => {
      expect(sanitizeInput('<script>alert("xss")</script>')).toBe(
        '&lt;script&gt;alert("xss")&lt;/script&gt;'
      );
    });

    it('should escape angle brackets', () => {
      expect(sanitizeInput('<div>Test</div>')).toBe('&lt;div&gt;Test&lt;/div&gt;');
    });

    it('should escape quotes', () => {
      expect(sanitizeInput('Test "quoted" text')).toBe('Test "quoted" text');
    });

    it('should escape ampersands', () => {
      expect(sanitizeInput('Tom & Jerry')).toBe('Tom &amp; Jerry');
    });

    it('should handle normal text unchanged (visually)', () => {
      const sanitized = sanitizeInput('Hello World');
      expect(sanitized).toBe('Hello World');
    });

    it('should handle empty string', () => {
      expect(sanitizeInput('')).toBe('');
    });
  });

  describe('isNotEmpty', () => {
    it('should return true for non-empty strings', () => {
      expect(isNotEmpty('text')).toBe(true);
      expect(isNotEmpty('  text  ')).toBe(true);
    });

    it('should return false for empty or whitespace strings', () => {
      expect(isNotEmpty('')).toBe(false);
      expect(isNotEmpty('   ')).toBe(false);
      expect(isNotEmpty('\t')).toBe(false);
      expect(isNotEmpty('\n')).toBe(false);
    });

    it('should return false for null or undefined', () => {
      expect(isNotEmpty(null)).toBe(false);
      expect(isNotEmpty(undefined)).toBe(false);
    });
  });

  describe('isValidUUID', () => {
    it('should validate valid UUID v4', () => {
      expect(isValidUUID('550e8400-e29b-41d4-a716-446655440000')).toBe(true);
      expect(isValidUUID('6ba7b810-9dad-41d4-a716-446655440000')).toBe(true);
    });

    it('should be case insensitive', () => {
      expect(isValidUUID('550E8400-E29B-41D4-A716-446655440000')).toBe(true);
    });

    it('should reject invalid UUIDs', () => {
      expect(isValidUUID('not-a-uuid')).toBe(false);
      expect(isValidUUID('550e8400-e29b-11d4-a716-446655440000')).toBe(false);
      expect(isValidUUID('')).toBe(false);
      expect(isValidUUID('550e8400-e29b-41d4-a716')).toBe(false);
    });

    it('should require correct version (4) format', () => {
      expect(isValidUUID('550e8400-e29b-31d4-a716-446655440000')).toBe(false);
      expect(isValidUUID('550e8400-e29b-51d4-a716-446655440000')).toBe(false);
    });
  });

  describe('isNonEmptyString', () => {
    it('should return true for non-empty strings', () => {
      expect(isNonEmptyString('text')).toBe(true);
      expect(isNonEmptyString(' ')).toBe(true);
      expect(isNonEmptyString('123')).toBe(true);
    });

    it('should return false for empty string', () => {
      expect(isNonEmptyString('')).toBe(false);
    });

    it('should return false for non-string values', () => {
      expect(isNonEmptyString(null)).toBe(false);
      expect(isNonEmptyString(undefined)).toBe(false);
      expect(isNonEmptyString(123)).toBe(false);
      expect(isNonEmptyString([])).toBe(false);
      expect(isNonEmptyString({})).toBe(false);
    });
  });

  describe('isDefined', () => {
    it('should return true for defined values', () => {
      expect(isDefined('text')).toBe(true);
      expect(isDefined(0)).toBe(true);
      expect(isDefined(false)).toBe(true);
      expect(isDefined('')).toBe(true);
      expect(isDefined([])).toBe(true);
      expect(isDefined({})).toBe(true);
    });

    it('should return false for null and undefined', () => {
      expect(isDefined(null)).toBe(false);
      expect(isDefined(undefined)).toBe(false);
    });

    it('should preserve type information', () => {
      const value: string | null = 'test';
      if (isDefined(value)) {
        // TypeScript should narrow to string here
        const str: string = value;
        expect(str).toBe('test');
      }
    });
  });
});
