import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PropertyPanel } from './PropertyPanel';
import { useWorkflowStore } from '@/store/workflowStore';

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
        data: { type, label: 'Node', config: {} },
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
