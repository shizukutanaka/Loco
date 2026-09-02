import { describe, it, expect } from 'vitest';
import type { Node, Edge } from 'reactflow';
import { simulateWorkflow, evaluateCondition } from './workflowSimulator';
import { CONDITION_OPERATIONS } from './constants';

/**
 * The simulator is what runs when a user presses "Test Workflow". Its
 * condition handling used to be `Math.random() > 0.5` with both arguments
 * unused, and the caller then discarded even that and picked an outgoing
 * branch at random. The tester's "Steps Executed: N / Coverage: X/Y nodes"
 * therefore changed from one run to the next for the same workflow, and the
 * condition the user had configured was never evaluated at all.
 *
 * These pin the simulator to the engine's semantics
 * (VisualWorkflowEngine.RegisterDefaultHandlers, condition handler):
 * left/operation/right, equals / not_equals / greater_than / less_than /
 * contains, unknown operation -> false, and a true/false handle followed
 * only when the verdict matches.
 */

const node = (id: string, type: string, config: Record<string, unknown> = {}): Node => ({
  id,
  type,
  position: { x: 0, y: 0 },
  data: { label: id, config },
});

const edge = (source: string, target: string, sourceHandle?: string): Edge => ({
  id: `${source}-${target}`,
  source,
  target,
  ...(sourceHandle ? { sourceHandle } : {}),
});

/** trigger -> condition -> (yes | no), the shape every branching template uses. */
const branching = (config: Record<string, unknown>) => ({
  nodes: [
    node('trigger-1', 'trigger'),
    node('check', 'condition', config),
    node('yes', 'action', { action: 'a' }),
    node('no', 'action', { action: 'b' }),
  ],
  edges: [
    edge('trigger-1', 'check'),
    edge('check', 'yes', 'true'),
    edge('check', 'no', 'false'),
  ],
});

const executed = (nodes: Node[], edges: Edge[]) =>
  simulateWorkflow(nodes, edges, { injectErrors: false, mockDelay: false })
    .stepsExecuted.map((s) => s.nodeId);

describe('simulator condition branching', () => {
  it('follows the true handle when the condition holds', () => {
    const { nodes, edges } = branching({ left: '150', operation: 'greater_than', right: '100' });
    const ran = executed(nodes, edges);

    expect(ran).toContain('yes');
    expect(ran).not.toContain('no');
  });

  it('follows the false handle when it does not', () => {
    const { nodes, edges } = branching({ left: '10', operation: 'greater_than', right: '100' });
    const ran = executed(nodes, edges);

    expect(ran).toContain('no');
    expect(ran).not.toContain('yes');
  });

  it('is deterministic', () => {
    // Twenty runs, one answer. With the old coin flip the chance of twenty
    // identical outcomes was one in half a million.
    const { nodes, edges } = branching({ left: '10', operation: 'greater_than', right: '100' });
    const outcomes = new Set(
      Array.from({ length: 20 }, () => executed(nodes, edges).join(','))
    );

    expect(outcomes.size).toBe(1);
  });

  it('resolves a {{nodeId.field}} reference against that node\'s simulated output', () => {
    // The trigger's mock output carries a `timestamp`; a condition on
    // {{trigger-1.timestamp}} > 0 must see it, not the literal text.
    const { nodes, edges } = branching({
      left: '{{trigger-1.timestamp}}',
      operation: 'greater_than',
      right: '0',
    });

    expect(executed(nodes, edges)).toContain('yes');
  });

  it('reports the verdict in the condition step output, not a coin flip', () => {
    const { nodes, edges } = branching({ left: 'a', operation: 'equals', right: 'a' });
    const step = simulateWorkflow(nodes, edges, { injectErrors: false, mockDelay: false })
      .stepsExecuted.find((s) => s.nodeId === 'check')!;

    expect(step.outputData.matched).toBe(true);
    expect(step.outputData).not.toHaveProperty('expression');
  });
});

describe('evaluateCondition mirrors the engine', () => {
  const cond = (config: Record<string, unknown>) => node('c', 'condition', config);

  it.each([
    ['equals', 'a', 'a', true],
    ['equals', 'a', 'b', false],
    ['not_equals', 'a', 'b', true],
    ['greater_than', '5', '3', true],
    ['greater_than', '3', '5', false],
    ['less_than', '3', '5', true],
    ['contains', 'hello world', 'world', true],
    ['contains', 'hello', 'xyz', false],
  ])('%s(%s, %s) -> %s', (operation, left, right, expected) => {
    expect(evaluateCondition(cond({ left, operation, right }), {})).toBe(expected);
  });

  it('covers every operation the product offers', () => {
    // Keeps the table above honest: an operation added to the shared list
    // without a row here fails, rather than silently going untested.
    const tested = new Set(['equals', 'not_equals', 'greater_than', 'less_than', 'contains']);
    expect([...CONDITION_OPERATIONS].sort()).toEqual([...tested].sort());
  });

  it('treats an unknown operation as false, like the engine\'s switch default', () => {
    expect(evaluateCondition(cond({ left: 'a', operation: 'matches', right: 'a' }), {})).toBe(false);
  });

  it('defaults the operation to equals, like the engine', () => {
    expect(evaluateCondition(cond({ left: 'x', right: 'x' }), {})).toBe(true);
  });

  it('resolves a whole-string reference from the data with its type kept', () => {
    expect(
      evaluateCondition(cond({ left: '{{amount}}', operation: 'greater_than', right: '100' }), {
        amount: 150,
      })
    ).toBe(true);
  });

  it('resolves an unknown reference to null rather than its own text', () => {
    // "{{missing}}" as literal text would be non-empty and `contains` '' -> true.
    expect(
      evaluateCondition(cond({ left: '{{missing}}', operation: 'equals', right: '{{missing}}' }), {})
    ).toBe(true);
    expect(
      evaluateCondition(cond({ left: '{{missing}}', operation: 'contains', right: 'miss' }), {})
    ).toBe(false);
  });
});
