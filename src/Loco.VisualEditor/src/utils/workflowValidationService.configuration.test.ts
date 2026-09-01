import { describe, it, expect } from 'vitest';
import type { Node } from 'reactflow';
import { validateWorkflow } from './workflowValidationService';
import { integrations } from '@/data/integrations';
import { CONDITION_OPERATIONS } from './constants';

/**
 * Regression tests for the field paths configuration validation reads.
 *
 * The editor writes an action node as:
 *   node.data.integration        <- set by the canvas drop handler
 *   node.data.config.action      <- set by PropertyPanel
 *   node.data.config.parameters  <- set by PropertyPanel
 *
 * and the server's WorkflowMapper reads the same shape. Validation previously
 * read `config.integration` and `config.actionType`, neither of which is ever
 * written. The effects were: "Missing Integration" fired on every correctly
 * configured action node, and the action + required-parameter checks could
 * never fire at all. These tests pin the real paths.
 */

const actionNode = (over: Record<string, unknown> = {}): Node => ({
  id: 'n1',
  type: 'action',
  position: { x: 0, y: 0 },
  data: {
    label: 'Call API',
    integration: 'http',
    // Exactly the fields the PropertyPanel offers for http:get - `url` and
    // an optional `headers`. This fixture used to carry a `method` too, which
    // no http action declares; see the "no field can supply it" test below.
    config: { action: 'get', parameters: { url: 'https://x.test' } },
    ...over,
  },
});

const issuesOf = (nodes: Node[]) => validateWorkflow(nodes, []).issues;
const titles = (nodes: Node[]) => issuesOf(nodes).map((i) => i.title);

describe('validateConfiguration field paths', () => {
  it('does NOT report Missing Integration for a properly configured action node', () => {
    // The core regression: data.integration is set, so this must be clean.
    expect(titles([actionNode()])).not.toContain('Missing Integration');
  });

  it('reports Missing Integration only when data.integration is absent', () => {
    const node = actionNode();
    delete (node.data as Record<string, unknown>).integration;
    expect(titles([node])).toContain('Missing Integration');
  });

  it('is not fooled by an integration placed under config (the old, wrong path)', () => {
    const node = actionNode({ integration: undefined, config: { integration: 'http' } });
    // config.integration is not where the editor writes it, so this node really
    // is missing an integration and must still be flagged.
    expect(titles([node])).toContain('Missing Integration');
  });

  it('reports Missing Action Type when config.action is absent but integration is set', () => {
    const node = actionNode({ config: { parameters: {} } });
    expect(titles([node])).toContain('Missing Action Type');
  });

  it('does not report Missing Action Type once config.action is set', () => {
    expect(titles([actionNode()])).not.toContain('Missing Action Type');
  });

  it('checks required parameters against the integration key', () => {
    // http:get declares a required `url`; omit it and the check must fire,
    // where previously it silently never ran.
    const node = actionNode({ config: { action: 'get', parameters: {} } });
    const found = issuesOf([node]).filter((i) => i.category === 'configuration');
    expect(found.some((i) => /url/i.test(i.description) || /url/i.test(i.title))).toBe(true);
  });

  it('accepts a node whose required parameters are all supplied', () => {
    const clean = issuesOf([actionNode()]).filter(
      (i) => i.category === 'configuration' && i.severity === 'error'
    );
    expect(clean).toEqual([]);
  });

  it('never demands a parameter no field can supply', () => {
    // The check used to read a hand-written table with three entries, whose
    // http row required `method`. No http action declares one - HttpConnector
    // models a separate action per verb - so the panel rendered no field for
    // it and a correctly configured HTTP node reported "Missing Required
    // Parameter: method" forever. A permanent, unclearable error teaches the
    // user to ignore the panel.
    //
    // Generalised: every parameter validation demands must be one the
    // connector declares, since the panel builds its fields from exactly that
    // declaration.
    const bad: string[] = [];

    for (const integration of integrations) {
      for (const action of integration.actions ?? []) {
        const declared = new Set(action.parameters.map((p) => p.name));
        const node = actionNode({
          integration: integration.id,
          config: { action: action.id, parameters: {} },
        });

        for (const issue of issuesOf([node])) {
          const match = /^Missing Required Parameter: (.+)$/.exec(issue.title);
          if (match && !declared.has(match[1])) {
            bad.push(`${integration.id}:${action.id} demands undeclared "${match[1]}"`);
          }
        }
      }
    }

    expect(bad).toEqual([]);
  });

  it('checks required parameters for every connector, not a chosen few', () => {
    // The old table covered http, database and email. Every other connector -
    // slack, discord, stripe, s3, redis, postgresql and the rest - went
    // entirely unchecked, so a Slack node with no channel and no text passed
    // validation cleanly.
    const unchecked: string[] = [];

    for (const integration of integrations) {
      for (const action of integration.actions ?? []) {
        if (!action.parameters.some((p) => p.required)) continue;

        const node = actionNode({
          integration: integration.id,
          config: { action: action.id, parameters: {} },
        });

        const reported = issuesOf([node]).some((i) =>
          i.title.startsWith('Missing Required Parameter:')
        );

        if (!reported) unchecked.push(`${integration.id}:${action.id}`);
      }
    }

    expect(unchecked).toEqual([]);
  });
});

describe('built-in node configuration', () => {
  const node = (type: string, config: Record<string, unknown>): Node => ({
    id: 'n1',
    type,
    position: { x: 0, y: 0 },
    data: { label: `A ${type}`, config },
  });

  // Scoped to issues attached to this node. A single node on its own is also
  // a workflow with no trigger, and "Missing Trigger Node" is a true finding
  // about the workflow rather than anything about the node's configuration.
  const errorTitles = (n: Node) =>
    issuesOf([n])
      .filter((i) => i.severity === 'error' && i.nodeId === n.id)
      .map((i) => i.title);

  it('accepts a condition configured the way the panel writes it', () => {
    // The engine reads left/operation/right and the panel writes those three.
    // Validation used to require `config.expression`, which nothing in the
    // product writes and nothing reads - so a fully configured condition node
    // reported "Missing Condition Expression" with no field able to clear it.
    expect(
      errorTitles(node('condition', { left: '{{amount}}', operation: 'greater_than', right: '100' }))
    ).toEqual([]);
  });

  it('reports a condition missing an operand', () => {
    // Both operands absent means `equals` compares null to null, which is
    // true, so the branch fires silently on every run.
    expect(errorTitles(node('condition', {}))).toContain('Missing Condition Operand');
  });

  it('rejects an operation the engine does not implement', () => {
    // The engine's switch falls through to `_ => false`, making the condition
    // permanently false rather than failing loudly.
    expect(
      errorTitles(node('condition', { left: 'a', right: 'b', operation: 'matches_regex' }))
    ).toContain('Unknown Condition Operation');
  });

  it.each(CONDITION_OPERATIONS)('accepts the %s operation', (operation) => {
    expect(errorTitles(node('condition', { left: 'a', right: 'b', operation }))).toEqual([]);
  });

  it('accepts a loop configured the way the panel writes it', () => {
    // Validation used to require `config.variable` and `config.arrayExpression`.
    // The engine iterates `items` and exposes `currentItem`; there is no
    // configurable iteration variable and no separate array expression, so
    // both errors were unclearable.
    expect(errorTitles(node('loop', { items: '["a","b"]' }))).toEqual([]);
  });

  it('reports a loop with nothing to iterate', () => {
    expect(errorTitles(node('loop', {}))).toContain('Missing Loop Items');
  });
});
