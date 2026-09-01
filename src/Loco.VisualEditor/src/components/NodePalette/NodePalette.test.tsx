import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, within } from '@testing-library/react';
import { NodePalette } from './NodePalette';
import { integrations } from '@/data/integrations';
import { Integration, IntegrationCategory } from '@/types/workflow';

/** The headings the palette renders, keyed the way integrations are tagged. */
const CATEGORY_LABELS: Record<IntegrationCategory, string> = {
  web: 'Web & APIs',
  communication: 'Communication',
  database: 'Database',
  cloud: 'Cloud',
  ai: 'AI',
  file: 'File',
  transform: 'Transform',
};

/**
 * Tests for what the palette actually puts on the canvas.
 *
 * The palette is where every node comes from on a blank canvas, and what it
 * writes into the drag payload becomes the node's `type` verbatim
 * (WorkflowCanvas's drop handler copies `nodeData.type`). Two things were
 * wrong and neither had a test:
 *
 *   There was no Trigger in the list. Every workflow needs one - validation
 *   reports "Missing Trigger Node" without it, the engine starts from nodes
 *   with no incoming edge, and the scheduler reads its cron - and templates
 *   were the only thing that ever created one.
 *
 *   Dragging an integration produced `type: 'trigger'` whenever its entry
 *   declared any triggers. Exactly one did (http, a webhook), so dragging
 *   HTTP/REST API onto the canvas made a trigger node instead of the API call
 *   the user was reaching for.
 */
describe('NodePalette drag payloads', () => {
  /**
   * Drags the card whose visible text is `label` and returns what it wrote.
   *
   * `within(container)` rather than `screen` because one test walks every
   * integration: querying the whole document would match cards from a palette
   * rendered by an earlier iteration, and the failure ("found multiple
   * elements") looks nothing like the bug it would be hiding.
   */
  const payloadFrom = (container: HTMLElement, label: string) => {
    const card = within(container).getByText(label).closest('[draggable]');
    expect(card, `no draggable card labelled "${label}"`).not.toBeNull();

    const setData = vi.fn();
    fireEvent.dragStart(card!, {
      dataTransfer: { setData, effectAllowed: '' },
    });

    expect(setData).toHaveBeenCalled();
    return JSON.parse(setData.mock.calls[0][1]);
  };

  const payloadFor = (label: string) => payloadFrom(render(<NodePalette />).container, label);

  /**
   * Makes `integration`'s card present, clicking its category open if needed.
   *
   * The heading is found by the category's own label rather than by position,
   * so an integration in a category the palette never lists fails here with
   * that as the message - which is the interesting failure, since such an
   * integration is unreachable no matter how many are in the catalogue.
   */
  const expandCategoryOf = (container: HTMLElement, integration: Integration) => {
    const q = within(container);
    if (q.queryByText(integration.name)) return;

    const heading = q
      .getAllByRole('button')
      .find((b) => new RegExp(`\\(\\d+\\)$`).test(b.textContent?.trim() ?? '') &&
                   b.textContent!.toLowerCase().includes(CATEGORY_LABELS[integration.category].toLowerCase()));

    expect(
      heading,
      `no palette category heading for "${integration.category}" (${integration.id} is unreachable)`
    ).toBeTruthy();

    fireEvent.click(heading!);
  };

  it('offers a Trigger node', () => {
    // Without this the only route to a trigger on a blank canvas was dragging
    // HTTP and getting one by accident.
    expect(payloadFor('Trigger').type).toBe('trigger');
  });

  it.each(['Condition', 'Transform', 'Loop', 'Delay'])(
    'offers the %s built-in with its own node type',
    (label) => {
      expect(payloadFor(label).type).toBe(label.toLowerCase());
    }
  );

  it('drags an integration as an action, not a trigger', () => {
    // Worth being precise about what this can catch. With no integration
    // declaring triggers any more, the old buggy expression - 'trigger' when
    // the entry declared any - now yields 'action' as well, so this test
    // alone would not notice its return. The test below is what makes the
    // pair sound: it fails the moment a trigger declaration reappears, which
    // is the only condition under which the old expression differs.
    const payload = payloadFor('HTTP Request');

    expect(payload.type).toBe('action');
    expect(payload.integration).toBe('http');
  });

  it('names every integration it drags', () => {
    // The drop handler reads `nodeData.label` and falls back to "New Node".
    // The payload never carried one, so every integration dragged onto the
    // canvas was called "New Node" while the palette card the user had just
    // clicked said "Slack".
    const { container } = render(<NodePalette />);

    for (const integration of integrations) {
      // Only web/communication/database start expanded, so most of the
      // catalogue is not in the DOM until its category heading is clicked.
      // Expanding on demand doubles as a reachability check: an integration
      // whose category has no heading is one a user can never reach, and it
      // fails here rather than going quietly untested.
      expandCategoryOf(container, integration);

      const payload = payloadFrom(container, integration.name);

      expect(payload.label, `${integration.id} drags without a label`).toBe(
        integration.name
      );
      expect(payload.description).toBe(integration.description);
    }
  });

  it('no integration declares a trigger the product cannot fire', () => {
    // The palette's node type used to be derived from this. More to the
    // point: the API exposes no webhook endpoint, so a declared trigger here
    // is an affordance nothing can deliver. Cron on a trigger node is the
    // only trigger this product fires.
    const withTriggers = integrations.filter((i) => (i.triggers?.length ?? 0) > 0);

    expect(withTriggers.map((i) => i.id)).toEqual([]);
  });
});

/**
 * Every node type the palette can produce must have a renderer.
 *
 * `delay` did not. It is a member of the NodeType union, the palette offers
 * it, the PropertyPanel edits its `seconds` and the engine executes it - but
 * WorkflowCanvas's nodeTypes map had no entry, so React Flow silently fell
 * back to its generic default node. The gap was invisible to tsc because the
 * map is typed as React Flow's `NodeTypes`, a plain index signature: it
 * demands nothing of the union.
 *
 * Comparing the two lists is what the compiler cannot do here.
 */
describe('canvas node renderers', () => {
  it('registers a renderer for every NodeType', async () => {
    const { NODE_TYPES } = await import('@/components/Canvas/WorkflowCanvas');

    // Mirrors the NodeType union in types/workflow.ts. Kept literal on
    // purpose: a type cannot be enumerated at runtime, so this is the one
    // place the list must be written out, and a new member that nobody
    // renders should fail here rather than render as a grey box.
    const NODE_TYPE_UNION = [
      'trigger',
      'action',
      'condition',
      'transform',
      'loop',
      'delay',
    ];

    expect(Object.keys(NODE_TYPES).sort()).toEqual([...NODE_TYPE_UNION].sort());
  });
});
