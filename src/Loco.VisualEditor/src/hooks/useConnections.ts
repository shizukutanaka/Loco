import { useState, useEffect, useCallback } from 'react';
import { listConnections, type Connection } from '@/api/connections';

interface UseConnectionsResult {
  connections: Connection[];
  loading: boolean;
  /** Set when the list could not be loaded; the caller decides how to surface it. */
  error: string | null;
  reload: () => void;
}

/**
 * Loads the connections available for a connector.
 *
 * Kept deliberately tolerant: the credential API is new, and an editor that
 * cannot reach it must still let the user build workflows. A failure leaves the
 * list empty and reports the reason rather than throwing, so the property panel
 * degrades to "no connections available" instead of blanking out.
 */
export function useConnections(connectorId?: string): UseConnectionsResult {
  const [connections, setConnections] = useState<Connection[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const reload = useCallback(() => setReloadToken((n) => n + 1), []);

  useEffect(() => {
    if (!connectorId) {
      setConnections([]);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);

    listConnections({ connectorId })
      .then((response) => {
        if (cancelled) return;

        if (response.success) {
          setConnections(response.data.connections ?? []);
          setError(null);
        } else {
          setConnections([]);
          setError(response.error.message);
        }
      })
      .catch((e: unknown) => {
        if (cancelled) return;
        setConnections([]);
        setError(e instanceof Error ? e.message : 'Failed to load connections');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    // A connector change mid-flight must not let a stale response overwrite the
    // new list.
    return () => {
      cancelled = true;
    };
  }, [connectorId, reloadToken]);

  return { connections, loading, error, reload };
}
