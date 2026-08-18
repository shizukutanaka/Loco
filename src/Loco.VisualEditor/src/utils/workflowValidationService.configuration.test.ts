import { describe, it, expect } from 'vitest';
import type { Node } from 'reactflow';
import { validateWorkflow } from './workflowValidationService';

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
    config: { action: 'get', parameters: { url: 'https://x.test', method: 'GET' } },
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
    // http requires url and method; omit them and the check must now fire,
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
});
