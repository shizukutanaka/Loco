import { describe, it, expect } from 'vitest';
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import type { Node, Edge } from 'reactflow';
import { shouldFollowConnection, RoutingError, SUPPORTED_CONDITIONS } from './connectionRouting';
import { simulateWorkflow } from './workflowSimulator';

/**
 * Runs tests/shared/connection-routing-table.json against this implementation.
 * Loco.Core's ConnectionRoutingTableTests runs the same file against the
 * engine's ConnectionRouter.
 *
 * The third of the shared tables and the starkest divergence: the simulator
 * was not mirroring the engine here at all. Its edge filter read only the
 * source handle and never the edge's condition, so an edge marked 'error' was
 * followed after the node SUCCEEDED.
 */

interface RoutingCase {
  sourceOutput: string | null;
  condition: string | null;
  sourceSucceeded: boolean;
  verdict: boolean | null;
  expect: 'follow' | 'skip' | 'error';
  why: string;
}

function repositoryRoot(): string {
  let dir = process.cwd();
  while (!existsSync(join(dir, 'Loco.sln'))) {
    const parent = dirname(dir);
    if (parent === dir) throw new Error('could not find the repository root');
    dir = parent;
  }
  return dir;
}

const cases: RoutingCase[] = JSON.parse(
  readFileSync(join(repositoryRoot(), 'tests', 'shared', 'connection-routing-table.json'), 'utf8')
).cases;

describe('connection routing against the shared table', () => {
  it('actually loaded the table', () => {
    expect(cases.length).toBeGreaterThan(15);
  });

  it.each(
    cases.map(
      (c) =>
        [
          `handle=${c.sourceOutput} condition=${c.condition} ok=${c.sourceSucceeded} verdict=${c.verdict}`,
          c,
        ] as const
    )
  )('%s', (_label, testCase) => {
    const act = () =>
      shouldFollowConnection(
        testCase.sourceOutput,
        testCase.condition,
        testCase.sourceSucceeded,
        testCase.verdict,
        'Check'
      );

    if (testCase.expect === 'error') {
      expect(act, testCase.why).toThrow(RoutingError);
      return;
    }

    expect(act(), testCase.why).toBe(testCase.expect === 'follow');
  });

  it('covers every supported condition and both branch handles', () => {
    const conditions = new Set(cases.map((c) => c.condition));
    const handles = new Set(cases.map((c) => c.sourceOutput));

    for (const condition of SUPPORTED_CONDITIONS) {
      // null stands in for 'default', which is why it is accepted here.
      const covered = conditions.has(condition) || (condition === 'default' && conditions.has(null));
      expect(covered, `no case exercises the '${condition}' condition`).toBe(true);
    }

    expect(handles.has('true'), 'no case exercises the true branch').toBe(true);
    expect(handles.has('false'), 'no case exercises the false branch').toBe(true);
  });

  it('covers both outcomes, so an always-follow implementation cannot pass', () => {
    const outcomes = new Set(cases.map((c) => c.expect));

    expect(outcomes.has('follow')).toBe(true);
    expect(outcomes.has('skip')).toBe(true);
    expect(outcomes.has('error')).toBe(true);
  });
});

/**
 * The end-to-end shape of the bug, rather than the unit underneath it: what a
 * user sees after marking a cleanup branch as the error path and pressing
 * "Test Workflow".
 */
describe('simulated routing honours edge conditions', () => {
  const node = (id: string, type: string, config: Record<string, unknown> = {}): Node => ({
    id,
    type,
    position: { x: 0, y: 0 },
    data: { label: id, config },
  });

  const run = (edges: Edge[]) =>
    simulateWorkflow(
      [node('trigger-1', 'trigger'), node('work', 'action', { action: 'a' }), node('cleanup', 'action', { action: 'b' })],
      edges,
      { injectErrors: false, mockDelay: false }
    ).stepsExecuted.map((s) => s.nodeId);

  it('does not run an error branch after a successful node', () => {
    // Before this, the simulation executed all three steps.
    const ran = run([
      { id: 'e1', source: 'trigger-1', target: 'work' },
      { id: 'e2', source: 'work', target: 'cleanup', data: { condition: 'error' } },
    ]);

    expect(ran).toContain('work');
    expect(ran).not.toContain('cleanup');
  });

  it('runs an always branch after a successful node', () => {
    // The companion: dropping every conditional edge would also satisfy the
    // test above, so this pins that 'always' is still followed.
    const ran = run([
      { id: 'e1', source: 'trigger-1', target: 'work' },
      { id: 'e2', source: 'work', target: 'cleanup', data: { condition: 'always' } },
    ]);

    expect(ran).toContain('cleanup');
  });

  it('runs a success branch after a successful node', () => {
    const ran = run([
      { id: 'e1', source: 'trigger-1', target: 'work' },
      { id: 'e2', source: 'work', target: 'cleanup', data: { condition: 'success' } },
    ]);

    expect(ran).toContain('cleanup');
  });
});
