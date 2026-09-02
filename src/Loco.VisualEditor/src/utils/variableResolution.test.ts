import { describe, it, expect } from 'vitest';
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { resolveReference } from './workflowSimulator';

/**
 * Runs tests/shared/variable-resolution-table.json against the simulator's
 * resolver. Loco.Core's WorkflowVariableResolverTests runs the same file
 * against the engine's.
 *
 * The companion of conditionSemantics.test.ts and it exists for the same
 * reason: two implementations in two languages had drifted. Probing both on
 * identical input found this side missing the `.data.` form the engine
 * supports and resolving names in the opposite order, plus a defect both
 * shared - a string that both begins and ends with a reference resolved to
 * null.
 */

interface ResolutionCase {
  expression: string;
  expect: unknown;
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

const table = JSON.parse(
  readFileSync(join(repositoryRoot(), 'tests', 'shared', 'variable-resolution-table.json'), 'utf8')
);

const variables: Record<string, unknown> = table.context.variables;
const nodeResults: Record<string, Record<string, unknown>> = table.context.nodeResults;
const cases: ResolutionCase[] = table.cases;

describe('variable resolution against the shared table', () => {
  it('actually loaded the table', () => {
    expect(cases.length).toBeGreaterThan(15);
  });

  it.each(cases.map((c) => [c.expression || '(empty string)', c] as const))(
    '%s',
    (_label, testCase) => {
      const actual = resolveReference(testCase.expression, variables, nodeResults);

      if (testCase.expect === null) {
        expect(actual, testCase.why).toBeNull();
      } else if (typeof testCase.expect === 'number') {
        // Compared numerically rather than by identity so neither side is
        // pinned to a particular numeric representation.
        expect(typeof actual, testCase.why).toBe('number');
        expect(Number(actual), testCase.why).toBe(testCase.expect);
      } else {
        expect(actual, testCase.why).toBe(testCase.expect);
      }
    }
  );

  it('covers the shapes that made the multi-reference bug survive', () => {
    // The bug needed a template that BOTH begins and ends with a reference.
    // A table holding only one of the two shapes would pass against the
    // broken implementation.
    const expressions = cases.map((c) => c.expression);

    expect(expressions.some((e) => /^\{\{.*\}\}$/.test(e) && (e.match(/\{\{/g) ?? []).length > 1))
      .toBe(true);
    expect(expressions.some((e) => !e.startsWith('{{') && e.includes('{{'))).toBe(true);
  });
});
