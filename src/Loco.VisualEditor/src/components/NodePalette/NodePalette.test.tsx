import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { NodePalette } from './NodePalette';
import { integrations } from '@/data/integrations';

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
  const payloadFor = (label: string) => {
    render(<NodePalette />);

    const card = screen.getByText(label).closest('[draggable]');
    expect(card, `no draggable card labelled "${label}"`).not.toBeNull();

    const setData = vi.fn();
    fireEvent.dragStart(card!, {
      dataTransfer: { setData, effectAllowed: '' },
    });

    expect(setData).toHaveBeenCalled();
    return JSON.parse(setData.mock.calls[0][1]);
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

  it('no integration declares a trigger the product cannot fire', () => {
    // The palette's node type used to be derived from this. More to the
    // point: the API exposes no webhook endpoint, so a declared trigger here
    // is an affordance nothing can deliver. Cron on a trigger node is the
    // only trigger this product fires.
    const withTriggers = integrations.filter((i) => (i.triggers?.length ?? 0) > 0);

    expect(withTriggers.map((i) => i.id)).toEqual([]);
  });
});
