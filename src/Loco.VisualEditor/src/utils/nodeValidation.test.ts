import { describe, it, expect } from 'vitest';
import {
  validateLabel,
  validateCondition,
  validateCode,
  validateNodeField,
} from './nodeValidation';

describe('Node Validation Utilities', () => {
  describe('validateLabel', () => {
    it('should accept valid labels', () => {
      expect(validateLabel('Start Node')).toBeNull();
      expect(validateLabel('Process Data')).toBeNull();
      expect(validateLabel('A')).toBeNull();
    });

    it('should reject empty labels', () => {
      expect(validateLabel('')).not.toBeNull();
      expect(validateLabel('   ')).not.toBeNull();
    });

    it('should reject labels exceeding max length', () => {
      const longLabel = 'a'.repeat(101);
      expect(validateLabel(longLabel, 100)).not.toBeNull();
    });

    it('should accept labels at max length', () => {
      const maxLabel = 'a'.repeat(100);
      expect(validateLabel(maxLabel, 100)).toBeNull();
    });

    it('should respect custom max length', () => {
      expect(validateLabel('Hello World', 5)).not.toBeNull();
      expect(validateLabel('Hello', 5)).toBeNull();
    });

    it('should return specific error message for empty', () => {
      const error = validateLabel('');
      expect(error).toBe('Node label is required');
    });

    it('should return specific error message for length', () => {
      const longLabel = 'a'.repeat(101);
      const error = validateLabel(longLabel, 100);
      expect(error).toContain('100');
    });
  });

  describe('validateCondition', () => {
    it('should accept valid JavaScript expressions', () => {
      expect(validateCondition('x > 5')).toBeNull();
      expect(validateCondition('name === "test"')).toBeNull();
      expect(validateCondition('true')).toBeNull();
      expect(validateCondition('false')).toBeNull();
      expect(validateCondition('1 + 1 === 2')).toBeNull();
      expect(validateCondition('arr.length > 0')).toBeNull();
      expect(validateCondition('obj.prop !== undefined')).toBeNull();
    });

    it('should reject empty conditions', () => {
      expect(validateCondition('')).not.toBeNull();
      expect(validateCondition('   ')).not.toBeNull();
    });

    it('should reject invalid JavaScript syntax', () => {
      expect(validateCondition('x >')).not.toBeNull();
      expect(validateCondition('x && &&')).not.toBeNull();
      expect(validateCondition('function{}')).not.toBeNull();
    });

    it('should return specific error messages', () => {
      expect(validateCondition('')).toBe('Condition expression is required');
      expect(validateCondition('x >')).toBe('Invalid condition expression');
    });

    it('should accept complex logical expressions', () => {
      expect(validateCondition('(x > 5 && y < 10) || z === null')).toBeNull();
      expect(validateCondition('!isActive && count > 0')).toBeNull();
    });

    it('should accept method calls', () => {
      expect(validateCondition('str.includes("test")')).toBeNull();
      expect(validateCondition('arr.some(item => item > 5)')).toBeNull();
    });
  });

  describe('validateCode', () => {
    it('should accept valid code', () => {
      expect(validateCode('const x = 5;')).toBeNull();
      expect(validateCode('return data.map(d => d.value);')).toBeNull();
      expect(validateCode('x = x + 1')).toBeNull();
    });

    it('should reject empty code', () => {
      expect(validateCode('')).not.toBeNull();
      expect(validateCode('   ')).not.toBeNull();
    });

    it('should return specific error messages', () => {
      expect(validateCode('')).toBe('Transform code is required');
    });

    it('should accept multiline code', () => {
      const multilineCode = `const result = data.map(item => ({
        ...item,
        processed: true
      }));
      return result;`;
      expect(validateCode(multilineCode)).toBeNull();
    });

    it('should accept code with various syntax', () => {
      expect(validateCode('if (x > 5) { y = 10; }')).toBeNull();
      expect(validateCode('for (let i = 0; i < 10; i++) { }')).toBeNull();
      expect(validateCode('[1, 2, 3].forEach(v => console.log(v));')).toBeNull();
    });
  });

  describe('validateNodeField', () => {
    it('should route to validateLabel for label field', () => {
      expect(validateNodeField('label', 'Valid Label')).toBeNull();
      expect(validateNodeField('label', '')).not.toBeNull();
    });

    it('should route to validateCondition for condition field', () => {
      expect(validateNodeField('condition', 'x > 5')).toBeNull();
      expect(validateNodeField('condition', '')).not.toBeNull();
    });

    it('should route to validateCode for code field', () => {
      expect(validateNodeField('code', 'x = 5;')).toBeNull();
      expect(validateNodeField('code', '')).not.toBeNull();
    });

    it('should return null for unknown fields', () => {
      expect(validateNodeField('unknownField', 'value')).toBeNull();
      expect(validateNodeField('customProp', '')).toBeNull();
    });

    it('should handle numeric values', () => {
      expect(validateNodeField('label', 123)).toBeNull();
      // Numbers convert to strings which are valid expressions (e.g., "456" is a valid number literal)
      expect(validateNodeField('condition', 456)).toBeNull();
    });

    it('should validate label with all max length checks', () => {
      const longLabel = 'a'.repeat(101);
      const error = validateNodeField('label', longLabel);
      expect(error).not.toBeNull();
    });
  });
});
