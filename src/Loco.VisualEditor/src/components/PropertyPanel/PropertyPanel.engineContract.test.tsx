import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PropertyPanel } from './PropertyPanel';
import { useWorkflowStore } from '@/store/workflowStore';

// The credential selector loads connections from the API. Stub it so these are
// unit tests of the panel rather than of the network, and so the <select> has
// real options - firing a change to a value with no matching option is a no-op.
vi.mock('@/api/connections', () => ({
  listConnections: vi.fn(async () => ({
    success: true as const,
    data: {
      connections: [
        {
          id: 'conn-1',
          connectorId: 'slack',
          name: 'Acme workspace',
          configuredFields: ['botToken'],
          createdAt: '2026-01-01T00:00:00.000Z',
        },
      ],
      total: 1,
      page: 1,
      pageSize: 50,
    },
  })),
}));

/**
 * Pins the PropertyPanel's built-in node configuration to the parameter names
 * the engine actually reads (VisualWorkflowEngine.RegisterDefaultHandlers):
 *
 *   condition -> left, operation, right
 *   transform -> type, json  (there is no C# execution)
 *
 * The panel previously wrote a single free-text `condition` string and a
 * `code` string. The engine read neither, so every condition node evaluated
 * Equals(null, null) - true, always, silently - and the "Transform Code (C#)"
 * editor did nothing at all. These tests fail if the panel drifts back to
 * writing keys the engine ignores.
 */

const selectNode = (type: string) => {
  useWorkflowStore.setState({
    nodes: [
      {
        id: 'n1',
        type,
        position: { x: 0, y: 0 },
        data: { label: 'Node', config: {} },
      },
    ],
    edges: [],
    selectedNodeId: 'n1',
    selectedEdgeId: null,
  });
};

const configOf = () => useWorkflowStore.getState().nodes[0].data.config;

describe('PropertyPanel <-> engine parameter contract', () => {
  beforeEach(() => {
    useWorkflowStore.setState({ nodes: [], edges: [], selectedNodeId: null, selectedEdgeId: null });
  });

  describe('condition node', () => {
    beforeEach(() => selectNode('condition'));

    it('offers left/operation/right, not a free-text expression', () => {
      render(<PropertyPanel />);
      expect(screen.getByLabelText(/left value/i)).toBeTruthy();
      expect(screen.getByLabelText(/operation/i)).toBeTruthy();
      expect(screen.getByLabelText(/right value/i)).toBeTruthy();
      expect(screen.queryByLabelText(/condition expression/i)).toBeNull();
    });

    it('writes config.left / config.operation / config.right', () => {
      render(<PropertyPanel />);

      fireEvent.change(screen.getByLabelText(/left value/i), { target: { value: '{{price}}' } });
      fireEvent.change(screen.getByLabelText(/operation/i), { target: { value: 'greater_than' } });
      fireEvent.change(screen.getByLabelText(/right value/i), { target: { value: '100' } });

      const config = configOf();
      expect(config.left).toBe('{{price}}');
      expect(config.operation).toBe('greater_than');
      expect(config.right).toBe('100');
      // The key the engine never reads must not be produced
      expect(config.condition).toBeUndefined();
    });

    it('only offers operations the engine implements', () => {
      render(<PropertyPanel />);
      const select = screen.getByLabelText(/operation/i) as HTMLSelectElement;
      const values = Array.from(select.options).map((o) => o.value).filter(Boolean);
      // Engine's switch: equals, not_equals, greater_than, less_than, contains
      expect(new Set(values)).toEqual(
        new Set(['equals', 'not_equals', 'greater_than', 'less_than', 'contains'])
      );
    });
  });

  describe('delay node', () => {
    beforeEach(() => selectNode('delay'));

    it('is configurable at all (the engine handler had no editor node type)', () => {
      render(<PropertyPanel />);
      expect(screen.getByLabelText(/delay \(seconds\)/i)).toBeTruthy();
    });

    it('writes config.seconds, the key the engine reads', () => {
      render(<PropertyPanel />);
      fireEvent.change(screen.getByLabelText(/delay \(seconds\)/i), { target: { value: '30' } });
      expect(configOf().seconds).toBe('30');
    });
  });

  describe('loop node', () => {
    beforeEach(() => selectNode('loop'));

    it('exposes the items collection the handler iterates', () => {
      render(<PropertyPanel />);
      expect(screen.getByLabelText(/items/i)).toBeTruthy();
    });

    it('writes config.items and rejects malformed JSON before execution', () => {
      render(<PropertyPanel />);
      const input = screen.getByLabelText(/items/i);

      fireEvent.change(input, { target: { value: '["a","b"]' } });
      expect(configOf().items).toBe('["a","b"]');

      fireEvent.change(input, { target: { value: '[nope' } });
      expect(screen.getByText(/not valid json/i)).toBeTruthy();
      // The invalid value must not reach the store
      expect(configOf().items).toBe('["a","b"]');
    });
  });

  describe('transform node', () => {
    beforeEach(() => selectNode('transform'));

    it('does not advertise C# execution the engine cannot perform', () => {
      render(<PropertyPanel />);
      expect(screen.queryByLabelText(/transform code/i)).toBeNull();
      expect(screen.getByLabelText(/transform type/i)).toBeTruthy();
    });

    it('writes config.type and config.json', () => {
      render(<PropertyPanel />);

      fireEvent.change(screen.getByLabelText(/^json$/i), { target: { value: '{"a":1}' } });

      const config = configOf();
      expect(config.type ?? 'json').toBe('json');
      expect(config.json).toBe('{"a":1}');
      expect(config.code).toBeUndefined();
    });

    it('rejects malformed JSON at edit time instead of at execution', () => {
      render(<PropertyPanel />);

      fireEvent.change(screen.getByLabelText(/^json$/i), { target: { value: '{nope' } });

      expect(screen.getByText(/not valid json/i)).toBeTruthy();
      // Invalid values must not reach the store
      expect(configOf().json).toBeUndefined();
    });
  });
});

/**
 * The user-facing halves of the credential (O-6) and trigger (O-7) work.
 * Both engine sides exist; until these inputs did, neither was reachable from
 * the editor.
 */
describe('PropertyPanel credential and schedule configuration', () => {
  beforeEach(() => {
    useWorkflowStore.setState({ nodes: [], edges: [], selectedNodeId: null, selectedEdgeId: null });
  });

  describe('trigger node scheduling', () => {
    beforeEach(() => selectNode('trigger'));

    it('exposes a cron field, which did not exist before', () => {
      render(<PropertyPanel />);
      expect(screen.getByLabelText(/schedule \(cron\)/i)).toBeTruthy();
    });

    it('writes config.cron, the key WorkflowSchedulerService reads', () => {
      render(<PropertyPanel />);
      fireEvent.change(screen.getByLabelText(/schedule \(cron\)/i), {
        target: { value: '0 9 * * 1-5' },
      });
      expect(configOf().cron).toBe('0 9 * * 1-5');
    });

    it('rejects a malformed expression instead of letting the scheduler skip it silently', () => {
      render(<PropertyPanel />);
      const input = screen.getByLabelText(/schedule \(cron\)/i);

      fireEvent.change(input, { target: { value: '0 9 * *' } }); // 4 fields
      expect(screen.getByText(/expected 5 fields/i)).toBeTruthy();
      expect(configOf().cron).toBeUndefined();
    });

    it('only asks for a timezone once a schedule is set', () => {
      render(<PropertyPanel />);
      expect(screen.queryByLabelText(/timezone/i)).toBeNull();

      fireEvent.change(screen.getByLabelText(/schedule \(cron\)/i), {
        target: { value: '0 0 * * *' },
      });
      expect(screen.getByLabelText(/timezone/i)).toBeTruthy();
    });
  });

  describe('connector node credentials', () => {
    const selectConnectorNode = (integration: string) => {
      useWorkflowStore.setState({
        nodes: [
          {
            id: 'n1',
            type: 'action',
            position: { x: 0, y: 0 },
            data: { label: 'Node', integration, config: {} },
          },
        ],
        edges: [],
        selectedNodeId: 'n1',
        selectedEdgeId: null,
      });
    };

    it('offers a connection selector for a connector-backed node', () => {
      selectConnectorNode('slack');
      render(<PropertyPanel />);
      expect(screen.getByLabelText(/connection/i)).toBeTruthy();
    });

    it('does not ask an engine built-in for credentials', () => {
      // 'variable' runs in-process and calls nothing external.
      selectConnectorNode('variable');
      render(<PropertyPanel />);
      expect(screen.queryByLabelText(/connection/i)).toBeNull();
    });

    it('stores the connection id on the node, never a secret', async () => {
      selectConnectorNode('slack');
      render(<PropertyPanel />);

      const select = await waitFor(() => {
        const el = screen.getByLabelText(/connection/i) as HTMLSelectElement;
        // Wait for the loaded option to exist; a change to an absent value is a no-op.
        expect(Array.from(el.options).some((o) => o.value === 'conn-1')).toBe(true);
        return el;
      });

      fireEvent.change(select, { target: { value: 'conn-1' } });

      const node = useWorkflowStore.getState().nodes[0];
      expect(node.data.credentialId).toBe('conn-1');
      // The node must carry a reference only - nothing secret-shaped.
      expect(JSON.stringify(node.data)).not.toMatch(/secret|token|apiKey/i);
    });

    it('clears the reference rather than storing an empty string', async () => {
      selectConnectorNode('slack');
      render(<PropertyPanel />);

      const select = await waitFor(() => {
        const el = screen.getByLabelText(/connection/i) as HTMLSelectElement;
        expect(Array.from(el.options).some((o) => o.value === 'conn-1')).toBe(true);
        return el;
      });

      fireEvent.change(select, { target: { value: 'conn-1' } });
      expect(useWorkflowStore.getState().nodes[0].data.credentialId).toBe('conn-1');

      fireEvent.change(select, { target: { value: '' } });
      expect(useWorkflowStore.getState().nodes[0].data.credentialId).toBeUndefined();
    });

    it('shows the connection names the API returned', async () => {
      selectConnectorNode('slack');
      render(<PropertyPanel />);

      await waitFor(() => expect(screen.getByText('Acme workspace')).toBeTruthy());
    });
  });
});
