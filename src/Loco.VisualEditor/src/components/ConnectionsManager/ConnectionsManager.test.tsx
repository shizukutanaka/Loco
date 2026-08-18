import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ConnectionsManager } from './ConnectionsManager';

const listConnections = vi.fn();
const createConnection = vi.fn();
const deleteConnection = vi.fn();
const testConnection = vi.fn();

vi.mock('@/api/connections', () => ({
  listConnections: (...args: unknown[]) => listConnections(...args),
  createConnection: (...args: unknown[]) => createConnection(...args),
  deleteConnection: (...args: unknown[]) => deleteConnection(...args),
  testConnection: (...args: unknown[]) => testConnection(...args),
}));

const okList = (connections: unknown[] = []) => ({
  success: true as const,
  data: { connections, total: connections.length, page: 1, pageSize: 50 },
});

const slackConnection = {
  id: 'conn-1',
  connectorId: 'slack',
  name: 'Acme workspace',
  configuredFields: ['botToken'],
  createdAt: '2026-01-01T00:00:00.000Z',
};

/**
 * The property panel can reference a connection but had no way to create one,
 * so its selector was permanently empty. These cover that path, and the
 * property that makes the whole design safe: a secret goes in and never comes
 * back out.
 */
describe('ConnectionsManager', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listConnections.mockResolvedValue(okList());
    createConnection.mockResolvedValue({ success: true, data: slackConnection });
    deleteConnection.mockResolvedValue({ success: true, data: undefined });
    testConnection.mockResolvedValue({
      success: true,
      data: { success: true, message: 'Connected', responseTimeMs: 42 },
    });
  });

  it('renders nothing while closed', () => {
    const { container } = render(<ConnectionsManager isOpen={false} onClose={() => {}} />);
    expect(container.childElementCount).toBe(0);
  });

  it('creates a connection with the connector, name and secret fields', async () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);

    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'slack' } });
    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: 'Acme workspace' } });
    fireEvent.change(screen.getByLabelText(/credential field/i), { target: { value: 'botToken' } });
    fireEvent.change(screen.getByLabelText(/^value$/i), { target: { value: 'xoxb-secret' } });

    fireEvent.click(screen.getByRole('button', { name: /create connection/i }));

    await waitFor(() =>
      expect(createConnection).toHaveBeenCalledWith({
        connectorId: 'slack',
        name: 'Acme workspace',
        secrets: { botToken: 'xoxb-secret' },
      })
    );
  });

  it('masks the secret input so it is not shoulder-readable', () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    expect(screen.getByLabelText(/^value$/i)).toHaveProperty('type', 'password');
  });

  it('refuses to submit without a connector, a name, or a credential', async () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    const submit = screen.getByRole('button', { name: /create connection/i });

    fireEvent.click(submit);
    // Scoped to the alert: the section heading also mentions choosing a connector.
    expect((await screen.findByRole('alert')).textContent).toMatch(/choose a connector/i);

    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'slack' } });
    fireEvent.click(submit);
    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toMatch(/give the connection a name/i)
    );

    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: 'Acme' } });
    fireEvent.click(submit);
    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toMatch(/add at least one credential field/i)
    );

    expect(createConnection).not.toHaveBeenCalled();
  });

  it('lists existing connections by which fields are set, never by value', async () => {
    listConnections.mockResolvedValue(okList([slackConnection]));
    render(<ConnectionsManager isOpen onClose={() => {}} />);

    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'slack' } });

    await waitFor(() => expect(screen.getByText('Acme workspace')).toBeTruthy());
    expect(screen.getByText(/fields set: botToken/i)).toBeTruthy();
    // A field NAME is shown; no value is available to show.
    expect(document.body.textContent).not.toMatch(/xoxb/);
  });

  it('tests a connection server-side and reports the result', async () => {
    listConnections.mockResolvedValue(okList([slackConnection]));
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'slack' } });

    const testButton = await screen.findByRole('button', { name: /^test$/i });
    fireEvent.click(testButton);

    await waitFor(() => expect(testConnection).toHaveBeenCalledWith('conn-1'));
    expect(await screen.findByText('Connected')).toBeTruthy();
  });

  it('surfaces a failed test rather than showing nothing', async () => {
    listConnections.mockResolvedValue(okList([slackConnection]));
    testConnection.mockResolvedValue({
      success: true,
      data: { success: false, message: 'invalid_auth', responseTimeMs: 12 },
    });

    render(<ConnectionsManager isOpen onClose={() => {}} />);
    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'slack' } });

    fireEvent.click(await screen.findByRole('button', { name: /^test$/i }));
    expect(await screen.findByText('invalid_auth')).toBeTruthy();
  });

  it('deletes a connection', async () => {
    listConnections.mockResolvedValue(okList([slackConnection]));
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'slack' } });

    fireEvent.click(await screen.findByRole('button', { name: /delete acme workspace/i }));
    await waitFor(() => expect(deleteConnection).toHaveBeenCalledWith('conn-1'));
  });

  it('reports a load failure instead of appearing empty', async () => {
    listConnections.mockResolvedValue({
      success: false,
      error: { code: 'UNAUTHORIZED', message: 'Not signed in' },
    });

    render(<ConnectionsManager isOpen onClose={() => {}} />);
    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'slack' } });

    expect(await screen.findByText(/could not load connections: not signed in/i)).toBeTruthy();
  });
});
