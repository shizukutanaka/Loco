import { describe, it, expect } from 'vitest';
import { Node, Edge } from 'reactflow';
import { templates } from './templates';
import { validateWorkflow } from '@/utils/workflowValidationService';

/**
 * A template is the product's own answer to "what does a good workflow look
 * like?", so it should survive the product's own validator. A template that
 * loads and immediately lights up the validation panel teaches the user that
 * the panel is noise.
 *
 * Only `error` severity is asserted. Warnings are advice - "no error handling
 * on this branch" is reasonable to leave out of a four-node example - but an
 * error is the validator saying this workflow will not run.
 */

const asNodes = (t: (typeof templates)[number]) => (t.workflow.nodes ?? []) as unknown as Node[];
const asEdges = (t: (typeof templates)[number]) => (t.workflow.edges ?? []) as unknown as Edge[];

describe('shipped templates', () => {
  it('ships at least one', () => {
    // Guards the assertions below from passing over an empty list, which is
    // how a check like this quietly stops checking anything.
    expect(templates.length).toBeGreaterThan(0);
  });

  it.each(templates.map((t) => [t.id, t] as const))(
    '%s validates without errors',
    (id, template) => {
      const report = validateWorkflow(asNodes(template), asEdges(template));
      const errors = report.issues.filter((i) => i.severity === 'error');

      expect(
        errors.map((e) => `${e.title}: ${e.description}`),
        `template "${id}" loads with validation errors`
      ).toEqual([]);
    }
  );

  it.each(templates.map((t) => [t.id, t] as const))(
    '%s starts from a trigger node',
    (id, template) => {
      const triggers = asNodes(template).filter((n) => n.type === 'trigger');

      expect(triggers.length, `template "${id}" has no trigger node`).toBeGreaterThan(0);
    }
  );
});

/**
 * Every {{reference}} a template writes must point at something that exists
 * in that template.
 *
 * The engine resolves {{first.rest}} by looking up `first` as a workflow
 * variable, then as a node id, then as the keyword `previous`. A reference to
 * a name none of those match resolves to null - silently, since an unknown
 * reference is not an error - and a condition comparing null to anything
 * takes the wrong branch without saying so. Two templates shipped exactly
 * that: `{{item.amount}}` and `{{payment.status}}`, where nothing named
 * `item` or `payment` was ever produced.
 *
 * `input` is allowed because the engine seeds Variables["input"] with the
 * trigger payload before any node runs.
 */
describe('template references', () => {
  const REFERENCE = /\{\{\s*([^.}\s]+)/g;

  const collect = (value: unknown, out: string[]) => {
    if (typeof value === 'string') {
      for (const m of value.matchAll(REFERENCE)) out.push(m[1]);
    } else if (Array.isArray(value)) {
      value.forEach((v) => collect(v, out));
    } else if (value && typeof value === 'object') {
      Object.values(value).forEach((v) => collect(v, out));
    }
  };

  it.each(templates.map((t) => [t.id, t] as const))(
    '%s only references nodes it contains',
    (id, template) => {
      const nodes = asNodes(template);
      const known = new Set([...nodes.map((n) => n.id), 'input', 'previous']);
      const refs: string[] = [];
      nodes.forEach((n) => collect(n.data?.config, refs));

      const dangling = refs.filter((r) => !known.has(r));

      expect(dangling, `template "${id}" references names nothing produces`).toEqual([]);
    }
  );

  it('actually finds references (the check is not vacuous)', () => {
    // A regex that matched nothing would let every template pass above.
    const refs: string[] = [];
    templates.forEach((t) => asNodes(t).forEach((n) => collect(n.data?.config, refs)));
    expect(refs.length).toBeGreaterThan(0);
  });
});
