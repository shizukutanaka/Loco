import { describe, it, expect } from 'vitest';
import { deepClone } from './deepClone';

describe('deepClone', () => {
  it('produces a value-equal but reference-distinct copy', () => {
    const original = { a: 1, nested: { b: [1, 2, 3] } };
    const clone = deepClone(original);

    expect(clone).toEqual(original);
    expect(clone).not.toBe(original);
    expect(clone.nested).not.toBe(original.nested);
    expect(clone.nested.b).not.toBe(original.nested.b);
  });

  it('mutating the clone does not affect the original (deep independence)', () => {
    const original = { list: [{ x: 1 }] };
    const clone = deepClone(original);

    clone.list[0].x = 999;
    clone.list.push({ x: 2 });

    expect(original.list).toHaveLength(1);
    expect(original.list[0].x).toBe(1);
  });

  it('clones arrays of objects', () => {
    const original = [{ id: 'a' }, { id: 'b' }];
    const clone = deepClone(original);
    expect(clone).toEqual(original);
    expect(clone[0]).not.toBe(original[0]);
  });

  it('preserves primitives and null', () => {
    expect(deepClone(42)).toBe(42);
    expect(deepClone('hi')).toBe('hi');
    expect(deepClone(null)).toBeNull();
    expect(deepClone(true)).toBe(true);
  });
});
