import { memo, useCallback, useMemo, useState } from 'react';
import { X, Trash2, Plug, CheckCircle2, AlertCircle } from 'lucide-react';
import { FormInput, FormSelect } from '@/components/Form';
import { integrations } from '@/data/integrations';
import {
  createConnection,
  deleteConnection,
  testConnection,
  type Connection,
} from '@/api/connections';
import { getMissingRequiredFields, type CredentialFieldDescriptor } from '@/api/connectors';
import { useConnections } from '@/hooks/useConnections';
import { useConnectors } from '@/hooks/useConnectors';

interface ConnectionsManagerProps {
  isOpen: boolean;
  onClose: () => void;
}

/**
 * Create, inspect, test and delete stored connector credentials.
 *
 * The property panel's connection selector is useless without this: there was
 * no way to create a connection, so the selector was permanently empty.
 *
 * Secrets are write-only throughout. Nothing here ever displays a stored value -
 * the server does not return them - so editing credentials means re-entering
 * them, and the list shows only which fields are set.
 */
function ConnectionsManagerComponent({ isOpen, onClose }: ConnectionsManagerProps) {
  // Hooks run unconditionally; the early return is below. Placing them after it
  // changes the hook count between renders and crashes React - the same bug
  // this codebase hit in three other panels.
  const [connectorId, setConnectorId] = useState('');
  const [name, setName] = useState('');
  const [secretValues, setSecretValues] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [testResults, setTestResults] = useState<Record<string, { ok: boolean; message: string }>>({});

  const { connections, loading, error, reload } = useConnections(connectorId || undefined);
  const { byId: connectorsById, error: catalogueError } = useConnectors(isOpen);

  /**
   * Which credential fields to ask for.
   *
   * The connector declares them itself - name, label, whether to mask, whether
   * it can work without one - and reads them back by exactly those names at
   * execution time. Rendering the declaration is what removes the whole class of
   * "saved fine, failed at execution" connections that a hand-typed field name
   * produced.
   *
   * `fieldNames` remains as the fallback for a connector the catalogue could not
   * describe (the server is unreachable, or it is a connector this build does
   * not know). Then, and only then, the user types names again.
   */
  const [fieldNames, setFieldNames] = useState<string[]>(['']);

  const selectedConnector = connectorId ? connectorsById[connectorId] : undefined;
  const declaredFields: CredentialFieldDescriptor[] | null =
    selectedConnector?.credentialFields ?? null;

  // The catalogue is authoritative about which connectors exist; the local
  // integrations list only supplies the icon, and connectors without a palette
  // entry still have to be selectable.
  const connectorOptions = useMemo(() => {
    const iconOf = (id: string) => integrations.find((i) => i.id === id)?.icon;
    const catalogue = Object.values(connectorsById);

    if (catalogue.length > 0) {
      return catalogue
        .map((c) => {
          const icon = iconOf(c.id);
          return { value: c.id, label: icon ? `${icon} ${c.name}` : c.name };
        })
        .sort((a, b) => a.label.localeCompare(b.label));
    }

    // Catalogue unavailable: fall back to the palette so the dialog still works.
    return integrations
      .filter((i) => i.id !== 'variable')
      .map((i) => ({ value: i.id, label: `${i.icon} ${i.name}` }));
  }, [connectorsById]);

  const resetForm = useCallback(() => {
    setName('');
    setSecretValues({});
    setFieldNames(['']);
    setFormError(null);
  }, []);

  const handleCreate = useCallback(async () => {
    if (!connectorId) return setFormError('Choose a connector');
    if (!name.trim()) return setFormError('Give the connection a name');

    const secrets: Record<string, string> = {};

    if (selectedConnector) {
      const missing = getMissingRequiredFields(selectedConnector, secretValues);
      if (missing.length > 0) {
        return setFormError(
          `Enter a value for ${missing.map((f) => f.label).join(', ')}`
        );
      }

      // Only declared fields are submitted, and blank optional ones are left
      // out entirely so configuredFields reports what is genuinely set.
      for (const field of selectedConnector.credentialFields) {
        const value = secretValues[field.name];
        if (value?.trim()) secrets[field.name] = value;
      }

    } else {
      for (const field of fieldNames) {
        const key = field.trim();
        if (key === '') continue;
        if (!secretValues[key]?.trim()) {
          return setFormError(`Enter a value for '${key}'`);
        }
        secrets[key] = secretValues[key];
      }
    }

    // A connector that declares no credentials - an unauthenticated webhook, say -
    // still needs a connection record for a node to reference, so an empty set is
    // only an error when fields were expected.
    if (Object.keys(secrets).length === 0 && declaredFields?.length !== 0) {
      return setFormError('Add at least one credential field');
    }

    setBusy(true);
    setFormError(null);
    try {
      const response = await createConnection({ connectorId, name: name.trim(), secrets });
      if (response.success) {
        resetForm();
        reload();
      } else {
        setFormError(response.error.message);
      }
    } catch (e) {
      setFormError(e instanceof Error ? e.message : 'Failed to create connection');
    } finally {
      setBusy(false);
    }
  }, [
    connectorId,
    name,
    fieldNames,
    secretValues,
    selectedConnector,
    declaredFields,
    resetForm,
    reload,
  ]);

  const handleDelete = useCallback(
    async (connection: Connection) => {
      setBusy(true);
      try {
        await deleteConnection(connection.id);
        reload();
      } finally {
        setBusy(false);
      }
    },
    [reload]
  );

  const handleTest = useCallback(async (connection: Connection) => {
    setBusy(true);
    try {
      const response = await testConnection(connection.id);
      setTestResults((prev) => ({
        ...prev,
        [connection.id]: response.success
          ? { ok: response.data.success, message: response.data.message }
          : { ok: false, message: response.error.message },
      }));
    } catch (e) {
      setTestResults((prev) => ({
        ...prev,
        [connection.id]: {
          ok: false,
          message: e instanceof Error ? e.message : 'Test failed',
        },
      }));
    } finally {
      setBusy(false);
    }
  }, []);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-3xl max-h-[85vh] flex flex-col">
        <div className="p-4 border-b border-gray-200 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Plug className="w-5 h-5 text-gray-700" />
            <h2 className="text-lg font-semibold text-gray-900">Connections</h2>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-gray-500 hover:bg-gray-100 rounded-lg transition-colors"
            title="Close"
            aria-label="Close"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-4 space-y-6">
          <p className="text-xs text-gray-600">
            Credentials are stored encrypted on the server and referenced by nodes
            using an ID, so an exported workflow never contains a secret. Values
            are never sent back to the browser — to change one, enter it again.
          </p>

          {/* Create */}
          <section className="space-y-3">
            <h3 className="text-sm font-semibold text-gray-700">New connection</h3>

            <FormSelect
              id="connection-connector"
              label="Connector"
              value={connectorId}
              onChange={(e) => {
                // Values typed for the previous connector must not carry over:
                // two connectors can both declare "apiKey", and silently reusing
                // one account's key for another is exactly the mistake stored
                // credentials exist to prevent.
                setConnectorId(e.target.value);
                setSecretValues({});
                setFieldNames(['']);
                setFormError(null);
              }}
              options={connectorOptions}
              placeholder="Select a connector"
            />

            <FormInput
              id="connection-name"
              label="Name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Acme workspace"
              helpText="Shown when picking a connection on a node."
            />

            {/*
              The connector's own declaration when we have it, so the field
              names are right by construction. Only when the catalogue is
              unavailable does the user type them again.
            */}
            {declaredFields ? (
              <>
                {declaredFields.length === 0 && (
                  <div className="text-xs text-gray-600">
                    {selectedConnector?.name} needs no credentials. Create the
                    connection so nodes have something to reference.
                  </div>
                )}

                {declaredFields.map((field) => (
                  <FormInput
                    key={field.name}
                    id={`connection-field-${field.name}`}
                    label={field.required ? `${field.label} *` : `${field.label} (optional)`}
                    type={field.type === 'password' ? 'password' : 'text'}
                    value={secretValues[field.name] ?? ''}
                    onChange={(e) =>
                      setSecretValues((prev) => ({ ...prev, [field.name]: e.target.value }))
                    }
                    placeholder={field.type === 'password' ? '••••••••' : field.name}
                    helpText={field.description ?? `Stored as ${field.name}.`}
                  />
                ))}
              </>
            ) : (
              <>
                {connectorId && catalogueError && (
                  <div className="text-xs text-amber-700" role="status">
                    Could not load this connector&apos;s credential fields (
                    {catalogueError}). Enter the field names manually — they must
                    match what the connector reads.
                  </div>
                )}

                {fieldNames.map((field, index) => (
                  <div key={index} className="grid grid-cols-2 gap-2">
                    <FormInput
                      id={`connection-field-${index}`}
                      label={index === 0 ? 'Credential field' : ''}
                      value={field}
                      onChange={(e) => {
                        const next = [...fieldNames];
                        next[index] = e.target.value;
                        setFieldNames(next);
                      }}
                      placeholder="botToken"
                      helpText={index === 0 ? "Must match the connector's field name." : undefined}
                    />
                    <FormInput
                      id={`connection-value-${index}`}
                      label={index === 0 ? 'Value' : ''}
                      type="password"
                      value={secretValues[field.trim()] ?? ''}
                      onChange={(e) =>
                        setSecretValues((prev) => ({ ...prev, [field.trim()]: e.target.value }))
                      }
                      placeholder="••••••••"
                      helpText={index === 0 ? 'Sent once; never returned.' : undefined}
                    />
                  </div>
                ))}

                <button
                  type="button"
                  onClick={() => setFieldNames((prev) => [...prev, ''])}
                  className="text-xs text-blue-600 hover:underline"
                >
                  + Add another field
                </button>
              </>
            )}

            {formError && (
              <div className="text-xs text-red-600" role="alert">
                {formError}
              </div>
            )}

            <button
              type="button"
              onClick={handleCreate}
              disabled={busy}
              className="px-4 py-2 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              {busy ? 'Saving…' : 'Create connection'}
            </button>
          </section>

          {/* Existing */}
          <section className="space-y-2">
            <h3 className="text-sm font-semibold text-gray-700">
              {connectorId ? 'Existing connections' : 'Existing connections (choose a connector above)'}
            </h3>

            {error && (
              <div className="text-xs text-red-600" role="alert">
                Could not load connections: {error}
              </div>
            )}

            {loading && <div className="text-xs text-gray-500">Loading…</div>}

            {!loading && connectorId && connections.length === 0 && !error && (
              <div className="text-xs text-gray-500">No connections for this connector yet.</div>
            )}

            {connections.map((connection) => {
              const result = testResults[connection.id];
              return (
                <div
                  key={connection.id}
                  className="border border-gray-200 rounded-lg p-3 flex items-start justify-between gap-3"
                >
                  <div className="min-w-0">
                    <div className="font-medium text-sm text-gray-900">{connection.name}</div>
                    <div className="text-xs text-gray-600">
                      Fields set: {connection.configuredFields.join(', ') || '(none)'}
                    </div>
                    {result && (
                      <div
                        className={`text-xs mt-1 flex items-center gap-1 ${
                          result.ok ? 'text-green-700' : 'text-red-600'
                        }`}
                        role="status"
                      >
                        {result.ok ? (
                          <CheckCircle2 className="w-3 h-3" />
                        ) : (
                          <AlertCircle className="w-3 h-3" />
                        )}
                        {result.message}
                      </div>
                    )}
                  </div>
                  <div className="flex gap-2 shrink-0">
                    <button
                      type="button"
                      onClick={() => handleTest(connection)}
                      disabled={busy}
                      className="px-2 py-1 text-xs border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50"
                    >
                      Test
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDelete(connection)}
                      disabled={busy}
                      className="p-1 text-red-600 hover:bg-red-50 rounded disabled:opacity-50"
                      title={`Delete ${connection.name}`}
                      aria-label={`Delete ${connection.name}`}
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              );
            })}
          </section>
        </div>
      </div>
    </div>
  );
}

export const ConnectionsManager = memo(ConnectionsManagerComponent);
export default ConnectionsManager;
