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
