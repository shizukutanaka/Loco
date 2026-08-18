import { useState, useEffect, useMemo } from 'react';
import { listConnectors, type ConnectorDescriptor } from '@/api/connectors';

interface UseConnectorsResult {
  connectors: ConnectorDescriptor[];
  /** Lookup by connector id, for rendering one connector's credential fields. */
  byId: Record<string, ConnectorDescriptor>;
  loading: boolean;
  /** Set when the catalogue could not be loaded; the caller decides what to show. */
  error: string | null;
}

/**
 * Loads the connector catalogue - what each connector is, and which credential
 * fields it declares.
 *
 * Tolerant in the same way as useConnections: a server the editor cannot reach
 * must not take the connections dialog down with it. On failure the list is
 * empty and the reason is reported, so the form can fall back to asking for
 * field names by hand rather than offering nothing at all.
 *
 * The catalogue is fixed for a running server - connectors are discovered once
 * at startup - so this loads once per mount and does not expose a reload.
 *
 * `enabled` exists because the component that needs this is a dialog: fetching
 * the catalogue for an editor session where nobody opens it is work nobody
 * asked for.
 */
export function useConnectors(enabled = true): UseConnectorsResult {
  const [connectors, setConnectors] = useState<ConnectorDescriptor[]>([]);
  const [loading, setLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled) return;

    let cancelled = false;
    setLoading(true);

    listConnectors()
      .then((response) => {
        if (cancelled) return;

        if (response.success) {
          setConnectors(response.data.connectors ?? []);
          setError(null);
        } else {
          setConnectors([]);
          setError(response.error.message);
        }
      })
      .catch((e: unknown) => {
        if (cancelled) return;
        setConnectors([]);
        setError(e instanceof Error ? e.message : 'Failed to load connectors');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [enabled]);

  const byId = useMemo(
    () => Object.fromEntries(connectors.map((c) => [c.id, c])),
    [connectors]
  );

  return { connectors, byId, loading, error };
}
