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

const listConnectors = vi.fn();

vi.mock('@/api/connectors', async (importActual) => ({
  // getMissingRequiredFields is pure logic under test, not a boundary.
  ...(await importActual<typeof import('@/api/connectors')>()),
  listConnectors: (...args: unknown[]) => listConnectors(...args),
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
    // Catalogue unreachable by default, which is the fallback path: the form
    // asks for field names by hand. The declared-field path has its own block.
    listConnectors.mockResolvedValue({
      success: false,
      error: { code: 'UNAVAILABLE', message: 'Catalogue unavailable' },
    });
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

/**
 * The form used to ask the user to type credential field names from memory,
 * under the warning "must match the connector's field name". A typo was
 * undetectable: the connection saved, listed, and reported its fields as set,
 * then failed at execution with a credential the connector never found.
 *
 * Each connector declares its fields exactly - name, label, whether to mask,
 * whether it is required - and reads them back by those names. These pin that
 * the form renders the declaration and submits the declared names verbatim.
 */
describe('ConnectionsManager with the connector catalogue', () => {
  const slackDescriptor = {
    id: 'slack',
    name: 'Slack',
    description: 'Slack messaging',
    category: 'Communication',
    authType: 'ApiKey',
    credentialFields: [
      {
        name: 'botToken',
        label: 'Bot User OAuth Token',
        type: 'password',
        required: true,
        description: 'Starts with xoxb-',
      },
      {
        name: 'signingSecret',
        label: 'Signing Secret',
        type: 'password',
        required: false,
      },
    ],
  };

  const selectSlack = () =>
    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'slack' } });

  beforeEach(() => {
    vi.clearAllMocks();
    listConnectors.mockResolvedValue({
      success: true,
      data: { connectors: [slackDescriptor], total: 1 },
    });
    listConnections.mockResolvedValue(okList());
    createConnection.mockResolvedValue({ success: true, data: slackConnection });
  });

  it('renders the fields the connector declares, by their labels', async () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    await screen.findByRole('option', { name: /slack/i });
    selectSlack();

    expect(await screen.findByLabelText(/bot user oauth token/i)).toBeTruthy();
    expect(screen.getByLabelText(/signing secret/i)).toBeTruthy();
    // The whole point: no free-text name box to get wrong.
    expect(screen.queryByLabelText(/^credential field$/i)).toBeNull();
  });

  it('submits the declared field name, not anything the user typed', async () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    await screen.findByRole('option', { name: /slack/i });
    selectSlack();

    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: 'Acme' } });
    fireEvent.change(await screen.findByLabelText(/bot user oauth token/i), {
      target: { value: 'xoxb-secret' },
    });
    fireEvent.click(screen.getByRole('button', { name: /create connection/i }));

    await waitFor(() =>
      expect(createConnection).toHaveBeenCalledWith({
        connectorId: 'slack',
        name: 'Acme',
        secrets: { botToken: 'xoxb-secret' },
      })
    );
  });

  it('omits an optional field left blank rather than storing an empty secret', async () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    await screen.findByRole('option', { name: /slack/i });
    selectSlack();

    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: 'Acme' } });
    fireEvent.change(await screen.findByLabelText(/bot user oauth token/i), {
      target: { value: 'xoxb-secret' },
    });
    fireEvent.click(screen.getByRole('button', { name: /create connection/i }));

    await waitFor(() => expect(createConnection).toHaveBeenCalled());
    // configuredFields drives the "is this connection complete" display, so an
    // empty signingSecret must not count as set.
    expect(createConnection.mock.calls[0][0].secrets).not.toHaveProperty('signingSecret');
  });

  it('blocks submission naming the required field the connector is missing', async () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    await screen.findByRole('option', { name: /slack/i });
    selectSlack();

    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: 'Acme' } });
    fireEvent.click(screen.getByRole('button', { name: /create connection/i }));

    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toMatch(/bot user oauth token/i)
    );
    expect(createConnection).not.toHaveBeenCalled();
  });

  it('does not demand an optional field', async () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    await screen.findByRole('option', { name: /slack/i });
    selectSlack();

    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: 'Acme' } });
    fireEvent.change(await screen.findByLabelText(/bot user oauth token/i), {
      target: { value: 'xoxb-secret' },
    });
    fireEvent.click(screen.getByRole('button', { name: /create connection/i }));

    await waitFor(() => expect(createConnection).toHaveBeenCalled());
  });

  it('masks a password-typed field', async () => {
    render(<ConnectionsManager isOpen onClose={() => {}} />);
    await screen.findByRole('option', { name: /slack/i });
    selectSlack();

    expect(await screen.findByLabelText(/bot user oauth token/i))
      .toHaveProperty('type', 'password');
  });

  it('clears entered values when the connector changes', async () => {
    listConnectors.mockResolvedValue({
      success: true,
      data: {
        connectors: [
          slackDescriptor,
          {
            ...slackDescriptor,
            id: 'notion',
            name: 'Notion',
            credentialFields: [
              { name: 'apiKey', label: 'Internal Integration Token', type: 'password', required: true },
            ],
          },
        ],
        total: 2,
      },
    });

    render(<ConnectionsManager isOpen onClose={() => {}} />);
    await screen.findByRole('option', { name: /slack/i });
    selectSlack();

    fireEvent.change(await screen.findByLabelText(/bot user oauth token/i), {
      target: { value: 'xoxb-secret' },
    });

    fireEvent.change(screen.getByLabelText(/connector/i), { target: { value: 'notion' } });

    // Two connectors can both declare "apiKey"; carrying a value across would
    // quietly file one account's key under another.
    const notionField = await screen.findByLabelText(/internal integration token/i);
    expect(notionField).toHaveProperty('value', '');
    expect(document.body.textContent).not.toMatch(/xoxb/);
  });

  it('falls back to manual entry, and says why, when the catalogue is unreachable', async () => {
    listConnectors.mockResolvedValue({
      success: false,
      error: { code: 'UNAVAILABLE', message: 'Catalogue unavailable' },
    });

    render(<ConnectionsManager isOpen onClose={() => {}} />);
    selectSlack();

    expect(await screen.findByLabelText(/^credential field$/i)).toBeTruthy();
    expect(screen.getByText(/could not load this connector's credential fields/i)).toBeTruthy();
  });
});
