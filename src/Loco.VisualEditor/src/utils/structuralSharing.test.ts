import { describe, it, expect } from 'vitest';
import type { Node, Edge } from 'reactflow';
import { createOptimizedHistorySnapshot } from './structuralSharing';

const node = (id: string, x = 0, y = 0, data: Record<string, unknown> = {}): Node => ({
  id,
  position: { x, y },
  data,
});
const edge = (id: string, source: string, target: string, data?: Record<string, unknown>): Edge => ({
  id,
  source,
  target,
  ...(data ? { data } : {}),
});

// When the snapshot detects a change it returns a fresh nodes.slice() - a new
// array that shares neither the input nor the previous reference. When nothing
// changed it hands back the *previous* array (structural sharing). So "copied"
// means "not the previous reference", and "shared" means "is the previous
// reference".
describe('structuralSharing / createOptimizedHistorySnapshot', () => {
  it('shallow-copies when there is no previous snapshot', () => {
    const nodes = [node('n1')];
    const edges = [edge('e1', 'n1', 'n2')];

    const snap = createOptimizedHistorySnapshot(nodes, edges);

    expect(snap.nodes).toEqual(nodes);
    expect(snap.nodes).not.toBe(nodes); // new array
    expect(snap.nodes[0]).toBe(nodes[0]); // same elements
    expect(snap.edges).not.toBe(edges);
  });

  it('reuses the previous references when nothing changed (structural sharing)', () => {
    const prevNodes = [node('n1', 10, 20)];
    const prevEdges = [edge('e1', 'n1', 'n2')];
    const nodes = [prevNodes[0]];
    const edges = [prevEdges[0]];

    const snap = createOptimizedHistorySnapshot(nodes, edges, prevNodes, prevEdges);

    expect(snap.nodes).toBe(prevNodes);
    expect(snap.edges).toBe(prevEdges);
  });

  it('copies nodes but reuses edges when only a node moved', () => {
    const prevNodes = [node('n1', 0, 0)];
    const prevEdges = [edge('e1', 'n1', 'n2')];
    const nodes = [node('n1', 100, 0)]; // moved
    const edges = [prevEdges[0]]; // unchanged

    const snap = createOptimizedHistorySnapshot(nodes, edges, prevNodes, prevEdges);

    expect(snap.nodes).not.toBe(prevNodes); // copied (new content)
    expect(snap.nodes).toEqual(nodes);
    expect(snap.edges).toBe(prevEdges); // shared
  });

  it('copies when the node count changed', () => {
    const prevNodes = [node('n1')];
    const nodes = [node('n1'), node('n2')];

    const snap = createOptimizedHistorySnapshot(nodes, [], prevNodes, []);
    expect(snap.nodes).not.toBe(prevNodes);
    expect(snap.nodes).toEqual(nodes);
  });

  it('detects a selection change on a node', () => {
    const prevNodes = [node('n1')];
    const nodes = [{ ...node('n1'), selected: true }];

    const snap = createOptimizedHistorySnapshot(nodes, [], prevNodes, []);
    expect(snap.nodes).not.toBe(prevNodes); // copied
    expect(snap.nodes[0].selected).toBe(true);
  });

  it('detects a deep data change on a node but ignores reference-only differences', () => {
    const prevNodes = [node('n1', 0, 0, { label: 'A' })];

    // Same content, different object reference -> treated as unchanged (shared)
    const sameContent = [node('n1', 0, 0, { label: 'A' })];
    const shared = createOptimizedHistorySnapshot(sameContent, [], prevNodes, []);
    expect(shared.nodes).toBe(prevNodes);

    // Different content -> copied
    const changed = [node('n1', 0, 0, { label: 'B' })];
    const copied = createOptimizedHistorySnapshot(changed, [], prevNodes, []);
    expect(copied.nodes).not.toBe(prevNodes);
    expect(copied.nodes).toEqual(changed);
  });

  it('detects edge source/target/data changes independently of nodes', () => {
    const prevNodes = [node('n1')];
    const prevEdges = [edge('e1', 'n1', 'n2', { condition: 'success' })];
    const nodes = [prevNodes[0]];

    // Retarget the edge; nodes unchanged
    const edges = [edge('e1', 'n1', 'n3', { condition: 'success' })];
    const snap = createOptimizedHistorySnapshot(nodes, edges, prevNodes, prevEdges);
    expect(snap.nodes).toBe(prevNodes); // shared
    expect(snap.edges).not.toBe(prevEdges); // copied
    expect(snap.edges).toEqual(edges);

    // Change only edge data content
    const dataEdges = [edge('e1', 'n1', 'n2', { condition: 'error' })];
    const snap2 = createOptimizedHistorySnapshot(nodes, dataEdges, prevNodes, prevEdges);
    expect(snap2.edges).not.toBe(prevEdges);
    expect(snap2.edges).toEqual(dataEdges);
  });
});
