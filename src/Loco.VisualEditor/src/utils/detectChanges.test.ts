import { describe, it, expect } from 'vitest';
import { detectChanged, hasChanges, groupChanges } from './detectChanges';

interface Item {
  id: string;
  value?: number;
  name?: string;
}

const snap = (items: Item[]) => ({ items });

describe('detectChanges', () => {
  describe('detectChanged', () => {
    it('returns an empty set when nothing changed', () => {
      const a = snap([{ id: '1', value: 1 }, { id: '2', value: 2 }]);
      const b = snap([{ id: '1', value: 1 }, { id: '2', value: 2 }]);
      expect(detectChanged(a, b).size).toBe(0);
    });

    it('detects added, removed, and modified ids together', () => {
      const prev = snap([{ id: 'keep', value: 1 }, { id: 'gone', value: 2 }, { id: 'edit', value: 3 }]);
      const curr = snap([{ id: 'keep', value: 1 }, { id: 'edit', value: 99 }, { id: 'new', value: 4 }]);

      const changed = detectChanged(prev, curr);
      expect(changed.has('keep')).toBe(false);
      expect(changed.has('gone')).toBe(true); // removed
      expect(changed.has('edit')).toBe(true); // modified
      expect(changed.has('new')).toBe(true); // added
      expect(changed.size).toBe(3);
    });

    it('honors a custom key function for grouping', () => {
      const prev = snap([{ id: '1', name: 'x' }]);
      const curr = snap([{ id: '1', name: 'y' }]);
      // Keyed by id -> same key '1', content differs -> 1 modified
      expect([...detectChanged(prev, curr)]).toEqual(['1']);
      // Keyed by name -> 'x' removed, 'y' added -> 2 changed
      expect([...detectChanged(prev, curr, (i) => i.name!)].sort()).toEqual(['x', 'y']);
    });
  });

  describe('hasChanges', () => {
    it('short-circuits true on differing lengths', () => {
      expect(hasChanges(snap([{ id: '1' }]), snap([{ id: '1' }, { id: '2' }]))).toBe(true);
    });

    it('returns false for equal snapshots and true for a modification', () => {
      const base = snap([{ id: '1', value: 1 }]);
      expect(hasChanges(base, snap([{ id: '1', value: 1 }]))).toBe(false);
      expect(hasChanges(base, snap([{ id: '1', value: 2 }]))).toBe(true);
    });
  });

  describe('groupChanges', () => {
    it('buckets ids into added / removed / modified', () => {
      const prev = snap([{ id: 'keep', value: 1 }, { id: 'gone', value: 2 }, { id: 'edit', value: 3 }]);
      const curr = snap([{ id: 'keep', value: 1 }, { id: 'edit', value: 99 }, { id: 'new', value: 4 }]);

      const { added, removed, modified } = groupChanges(prev, curr);
      expect([...added]).toEqual(['new']);
      expect([...removed]).toEqual(['gone']);
      expect([...modified]).toEqual(['edit']);
    });

    it('returns three empty sets when identical', () => {
      const base = snap([{ id: '1', value: 1 }]);
      const { added, removed, modified } = groupChanges(base, snap([{ id: '1', value: 1 }]));
      expect(added.size + removed.size + modified.size).toBe(0);
    });
  });
});
