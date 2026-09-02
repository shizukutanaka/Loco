import { describe, it, expect } from 'vitest';
import { readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { compare, ConditionError, SUPPORTED_OPERATIONS } from './conditionSemantics';

/**
 * Runs tests/shared/condition-truth-table.json against this implementation.
 *
 * Loco.Core's ConditionEvaluatorTests runs the same file against the engine's.
 * The two are separate implementations in separate languages, so the guard
 * against drift is the shared table rather than shared code: changing one side
 * without the other fails one of the two suites.
 *
 * It exists because they did drift. `"abc" greater_than "100"` failed the node
 * in the engine and returned false here, so "Test Workflow" reported a green
 * run for a workflow that would die in production.
 */

interface TruthCase {
  left: unknown;
  operation: string;
  right: unknown;
  expect: boolean | 'error';
  why: string;
}

/** Walks up for Loco.sln rather than assuming vitest's working directory. */
function repositoryRoot(): string {
  let dir = process.cwd();
  while (!existsSync(join(dir, 'Loco.sln'))) {
    const parent = dirname(dir);
    if (parent === dir) throw new Error('could not find the repository root');
    dir = parent;
  }
  return dir;
}

const cases: TruthCase[] = JSON.parse(
  readFileSync(join(repositoryRoot(), 'tests', 'shared', 'condition-truth-table.json'), 'utf8')
).cases;

const render = (v: unknown) => (typeof v === 'string' ? `"${v}"` : String(v));

describe('condition semantics against the shared truth table', () => {
  it('actually loaded the table', () => {
    // A table that failed to load would make every case below vacuous.
    expect(cases.length).toBeGreaterThan(25);
  });

  it.each(cases.map((c) => [`${render(c.left)} ${c.operation} ${render(c.right)}`, c] as const))(
    '%s',
    (_label, testCase) => {
      if (testCase.expect === 'error') {
        let thrown: unknown;
        try {
          compare(testCase.left, testCase.operation, testCase.right, 'Check');
        } catch (e) {
          thrown = e;
        }

        expect(thrown, testCase.why).toBeInstanceOf(ConditionError);
        // Naming the node and the operation is the point: the engine's old
        // failure said "The input string 'abc' was not in a correct format"
        // and named neither.
        expect((thrown as Error).message).toContain('Check');
        expect((thrown as Error).message).toContain(testCase.operation);
        return;
      }

      expect(
        compare(testCase.left, testCase.operation, testCase.right, 'Check'),
        testCase.why
      ).toBe(testCase.expect);
    }
  );

  it('covers every supported operation', () => {
    const covered = new Set(cases.map((c) => c.operation));

    for (const operation of SUPPORTED_OPERATIONS) {
      expect(covered.has(operation), `no case exercises '${operation}'`).toBe(true);
    }
  });

  it('covers both outcomes of every supported operation', () => {
    // Guards against a table that only ever expects one answer, which would
    // pass against an implementation that always returned it.
    for (const operation of SUPPORTED_OPERATIONS) {
      const expectations = cases.filter((c) => c.operation === operation).map((c) => c.expect);

      expect(expectations, `'${operation}' needs a case that holds`).toContain(true);
      expect(expectations, `'${operation}' needs a case that does not`).toContain(false);
    }
  });
});
