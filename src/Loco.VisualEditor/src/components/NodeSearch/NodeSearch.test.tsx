import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { NodeSearch } from './NodeSearch';
import { useWorkflowStore } from '@/store/workflowStore';
import { integrations } from '@/data/integrations';
import { NodeType } from '@/types/workflow';

/**
 * NodeSearch is the second way to put a node on the canvas, and it had the
 * same gap the palette did: its built-in list held condition, transform and
 * loop, so searching "trigger" or "delay" returned nothing. Both are real node
 * types - renderer, property editor, engine handler - and trigger is the one
 * every workflow needs before it will validate at all.
 *
 * What these pin is the boundary rather than the list: a built-in result's id
 * becomes `node.type` verbatim, so an id that is not a NodeType produces a
 * node nothing can render.
 */

const NODE_TYPE_UNION: NodeType[] = [
  'trigger',
  'action',
  'condition',
  'transform',
  'loop',
  'delay',
];

/** What search can offer as a built-in. `action` only ever comes from an integration. */
const BUILT_IN_TYPES = NODE_TYPE_UNION.filter((t) => t !== 'action');

const nodes = () => useWorkflowStore.getState().nodes;

/** Opens the dialog and types `query`, returning the result buttons. */
const search = (query: string) => {
  const view = render(<NodeSearch isOpen onClose={vi.fn()} />);

  fireEvent.change(screen.getByLabelText(/search for nodes/i), {
    target: { value: query },
  });

  // Every result is a button; the only other button is the close control,
  // which has an aria-label rather than result text.
  const results = within(view.container)
    .getAllByRole('button')
    .filter((b) => b.getAttribute('aria-label') === null);

  return { ...view, results };
};

/** Clicks the result whose title line is exactly `name`. */
const select = (results: HTMLElement[], name: string) => {
  const match = results.find((b) =>
    within(b).queryByText(new RegExp(`^${name}$`, 'i'))
  );

  expect(match, `"${name}" is not offered as a search result`).toBeTruthy();
  fireEvent.click(match!);
};

describe('NodeSearch', () => {
  beforeEach(() => {
    useWorkflowStore.setState({ nodes: [], edges: [], selectedNodeId: null });
  });

  it.each(BUILT_IN_TYPES)('finds the %s built-in and adds it with that type', (type) => {
    const { results } = search(type);
    select(results, type);

    expect(nodes(), `selecting "${type}" added no node`).toHaveLength(1);
    expect(nodes()[0].type).toBe(type);
  });

  it('gives built-in nodes no integration', () => {
    // `integration: result.type === 'integration' ? result.id : result.id` -
    // both branches the same - stamped `integration: 'condition'` onto a
    // condition node, naming a connector that does not exist. The engine
    // dispatches condition/transform/delay/loop by node type and never looks,
    // so nothing broke; the field was simply untrue.
    const { results } = search('condition');
    select(results, 'condition');

    expect(nodes()[0].data.integration).toBeUndefined();
  });

  it('adds an integration as an action node carrying its id', () => {
    const { results } = search('Slack');
    select(results, 'Slack');

    expect(nodes()[0].type).toBe('action');
    expect(nodes()[0].data.integration).toBe('slack');
    expect(nodes()[0].data.label).toBe('Slack');
  });

  it('offers every integration in the catalogue', () => {
    // The palette hides most of the catalogue behind collapsed categories, so
    // search is the only route to some integrations by name. One that cannot
    // be found here is one a user has to go looking for.
    for (const integration of integrations) {
      const { results, unmount } = search(integration.name);

      expect(
        results.some((b) => within(b).queryByText(integration.name)),
        `"${integration.name}" returns no search result`
      ).toBe(true);

      unmount();
    }
  });
});
