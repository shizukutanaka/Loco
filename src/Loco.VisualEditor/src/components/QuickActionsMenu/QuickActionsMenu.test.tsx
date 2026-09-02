import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QuickActionsMenu } from './QuickActionsMenu';

/**
 * What the right-click menu offers must be something the product does.
 *
 * Three entries were not. "Run from Here" showed "Running workflow from this
 * node..." and ran nothing - the API executes a whole workflow and exposes no
 * partial run, so there was no way for it to happen. "Group Nodes" and
 * "Connect To..." each showed "feature coming soon". A menu entry is a promise
 * that something will happen; these advertised capabilities the product does
 * not have, and the first one reported success for work it never did.
 *
 * "Disconnect" was in the same state, but is kept and now works: the store has
 * had deleteEdge all along and the note claiming otherwise was stale.
 */

const menu = (nodeId: string | null) =>
  render(
    <QuickActionsMenu
      isOpen
      position={{ x: 0, y: 0 }}
      nodeId={nodeId}
      onClose={vi.fn()}
      onAction={vi.fn()}
    />
  );

describe('QuickActionsMenu', () => {
  it.each(['Run from Here', 'Group Nodes', 'Connect To...'])(
    'does not offer %s, which nothing backs',
    (label) => {
      menu('n1');
      expect(screen.queryByText(label)).toBeNull();
    }
  );

  it.each(['Duplicate', 'Rename', 'Delete', 'Disconnect', 'Properties', 'Node Info'])(
    'offers %s, which is implemented',
    (label) => {
      menu('n1');
      expect(screen.getByText(label)).toBeTruthy();
    }
  );

  it('offers a node of every type the canvas can render', () => {
    // The canvas menu offered trigger/action/condition/transform/loop but not
    // delay, though delay is a full node type with a renderer, a property
    // editor and an engine handler - the same gap the palette had.
    menu(null);

    for (const label of [
      'Add Trigger',
      'Add Action',
      'Add Condition',
      'Add Transform',
      'Add Loop',
      'Add Delay',
    ]) {
      expect(screen.getByText(label), `no "${label}" entry`).toBeTruthy();
    }
  });

  it('shows the node menu and the canvas menu as different things', () => {
    // Guards the two tests above from both passing against one merged menu
    // that happens to contain everything.
    const { unmount } = menu('n1');
    expect(screen.queryByText('Add Trigger')).toBeNull();
    unmount();

    menu(null);
    expect(screen.queryByText('Duplicate')).toBeNull();
  });
});
