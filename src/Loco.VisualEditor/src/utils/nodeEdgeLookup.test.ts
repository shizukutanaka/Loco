import { describe, it, expect } from 'vitest';
import type { Node, Edge } from 'reactflow';
import {
  createNodeIndex,
  createEdgeIndex,
  findNodeById,
  findEdgeById,
  getConnectedEdges,
  getOutgoingEdges,
  getIncomingEdges,
  getConnectedNodes,
} from './nodeEdgeLookup';

const n = (id: string): Node => ({ id, position: { x: 0, y: 0 }, data: {} });
const e = (id: string, source: string, target: string): Edge => ({ id, source, target });

// A small graph: n1 -> n2 -> n3, plus n1 -> n3
const nodes: Node[] = [n('n1'), n('n2'), n('n3')];
const edges: Edge[] = [e('e12', 'n1', 'n2'), e('e23', 'n2', 'n3'), e('e13', 'n1', 'n3')];

describe('nodeEdgeLookup', () => {
  describe('createNodeIndex / createEdgeIndex', () => {
    it('indexes nodes by id', () => {
      const idx = createNodeIndex(nodes);
      expect(idx.size).toBe(3);
      expect(idx.get('n2')?.id).toBe('n2');
      expect(idx.get('missing')).toBeUndefined();
    });

    it('indexes edges by id', () => {
      const idx = createEdgeIndex(edges);
      expect(idx.size).toBe(3);
      expect(idx.get('e23')?.source).toBe('n2');
    });

    it('later duplicate ids overwrite earlier ones', () => {
      const idx = createNodeIndex([n('dup'), { ...n('dup'), data: { tag: 'second' } }]);
      expect(idx.size).toBe(1);
      expect(idx.get('dup')?.data).toEqual({ tag: 'second' });
    });
  });

  describe('findNodeById / findEdgeById', () => {
    it('finds a node from an array', () => {
      expect(findNodeById(nodes, 'n3')?.id).toBe('n3');
      expect(findNodeById(nodes, 'nope')).toBeUndefined();
    });

    it('finds a node from a Map', () => {
      const idx = createNodeIndex(nodes);
      expect(findNodeById(idx, 'n1')?.id).toBe('n1');
      expect(findNodeById(idx, 'nope')).toBeUndefined();
    });

    it('finds an edge from both array and Map', () => {
      expect(findEdgeById(edges, 'e13')?.target).toBe('n3');
      expect(findEdgeById(createEdgeIndex(edges), 'e13')?.target).toBe('n3');
      expect(findEdgeById(edges, 'nope')).toBeUndefined();
    });
  });

  describe('getConnectedEdges / getOutgoing / getIncoming', () => {
    it('gets every edge touching a node (source or target)', () => {
      const ids = getConnectedEdges(edges, 'n1').map((x) => x.id).sort();
      expect(ids).toEqual(['e12', 'e13']);

      const n3ids = getConnectedEdges(edges, 'n3').map((x) => x.id).sort();
      expect(n3ids).toEqual(['e13', 'e23']);
    });

    it('separates outgoing from incoming', () => {
      expect(getOutgoingEdges(edges, 'n1').map((x) => x.id).sort()).toEqual(['e12', 'e13']);
      expect(getIncomingEdges(edges, 'n1')).toEqual([]);

      expect(getOutgoingEdges(edges, 'n3')).toEqual([]);
      expect(getIncomingEdges(edges, 'n3').map((x) => x.id).sort()).toEqual(['e13', 'e23']);
    });

    it('accepts a Map as well as an array', () => {
      const idx = createEdgeIndex(edges);
      expect(getConnectedEdges(idx, 'n2').map((x) => x.id).sort()).toEqual(['e12', 'e23']);
    });
  });

  describe('getConnectedNodes', () => {
    it('returns downstream nodes for direction "out"', () => {
      const ids = getConnectedNodes(nodes, edges, 'n1', 'out').map((x) => x.id).sort();
      expect(ids).toEqual(['n2', 'n3']);
    });

    it('returns upstream nodes for direction "in"', () => {
      const ids = getConnectedNodes(nodes, edges, 'n3', 'in').map((x) => x.id).sort();
      expect(ids).toEqual(['n1', 'n2']);
    });

    it('returns both directions by default', () => {
      const ids = getConnectedNodes(nodes, edges, 'n2').map((x) => x.id).sort();
      expect(ids).toEqual(['n1', 'n3']);
    });

    it('deduplicates and skips edges pointing at unknown nodes', () => {
      const withDangling = [...edges, e('eX', 'n1', 'ghost')];
      const ids = getConnectedNodes(nodes, withDangling, 'n1', 'out').map((x) => x.id).sort();
      // ghost has no node, so it is dropped; n2/n3 are not duplicated
      expect(ids).toEqual(['n2', 'n3']);
    });

    it('returns an empty array for an isolated node', () => {
      expect(getConnectedNodes([...nodes, n('lonely')], edges, 'lonely')).toEqual([]);
    });
  });
});
