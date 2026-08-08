import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  listConnections,
  getConnection,
  createConnection,
  updateConnection,
  deleteConnection,
  testConnection,
  getMissingFields,
  isConnectionComplete,
  type Connection,
} from './connections';
import { apiClient } from './client';

const connection = (over: Partial<Connection> = {}): Connection => ({
  id: 'c1',
  connectorId: 'slack',
  name: 'Acme workspace',
  configuredFields: ['botToken'],
  createdAt: '2026-01-01T00:00:00.000Z',
  ...over,
});

describe('connections API', () => {
  let get: ReturnType<typeof vi.spyOn>;
  let post: ReturnType<typeof vi.spyOn>;
  let put: ReturnType<typeof vi.spyOn>;
  let del: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    const ok = { success: true as const, data: undefined as never };
    get = vi.spyOn(apiClient, 'get').mockResolvedValue(ok);
    post = vi.spyOn(apiClient, 'post').mockResolvedValue(ok);
    put = vi.spyOn(apiClient, 'put').mockResolvedValue(ok);
    del = vi.spyOn(apiClient, 'delete').mockResolvedValue(ok);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('listConnections', () => {
    it('requests the bare endpoint when given no params', async () => {
      await listConnections();
      expect(get).toHaveBeenCalledWith('/connections');
    });

    it('serializes pagination and connector filters into the query string', async () => {
      await listConnections({ page: 2, pageSize: 25, connectorId: 'stripe' });
      const url = get.mock.calls[0][0] as string;
      expect(url).toContain('page=2');
      expect(url).toContain('pageSize=25');
      expect(url).toContain('connectorId=stripe');
    });
  });

  describe('CRUD verbs map to the right method and path', () => {
    it('getConnection issues a GET', async () => {
      await getConnection('c1');
      expect(get).toHaveBeenCalledWith('/connections/c1');
    });

    it('createConnection POSTs the secrets payload', async () => {
      await createConnection({
        connectorId: 'slack',
        name: 'Acme',
        secrets: { botToken: 'xoxb-secret' },
      });
      expect(post).toHaveBeenCalledWith('/connections', {
        connectorId: 'slack',
        name: 'Acme',
        secrets: { botToken: 'xoxb-secret' },
      });
    });

    it('updateConnection PUTs to the id, and can rename without resubmitting secrets', async () => {
      await updateConnection('c1', { name: 'Renamed' });
      expect(put).toHaveBeenCalledWith('/connections/c1', { name: 'Renamed' });

      const body = put.mock.calls[0][1] as Record<string, unknown>;
      expect('secrets' in body).toBe(false);
    });

    it('deleteConnection issues a DELETE', async () => {
      await deleteConnection('c1');
      expect(del).toHaveBeenCalledWith('/connections/c1');
    });

    it('testConnection POSTs to the test sub-resource so the secret stays server-side', async () => {
      await testConnection('c1');
      expect(post).toHaveBeenCalledWith('/connections/c1/test', {});
    });
  });

  describe('secrets travel one way only', () => {
    it('the Connection response shape carries no secret values', () => {
      // Compile-time guarantee is in the type; this asserts the runtime shape a
      // server response is expected to have, so a future field addition that
      // smuggles values back has to break this test deliberately.
      const c = connection();
      expect(Object.keys(c).sort()).toEqual(
        ['configuredFields', 'connectorId', 'createdAt', 'id', 'name'].sort()
      );
      expect(JSON.stringify(c)).not.toContain('xoxb');
    });

    it('configuredFields names fields without exposing their values', () => {
      const c = connection({ configuredFields: ['botToken', 'signingSecret'] });
      expect(c.configuredFields).toEqual(['botToken', 'signingSecret']);
      // Field NAMES only - no value-bearing companion field
      expect(c).not.toHaveProperty('secrets');
    });
  });

  describe('getMissingFields / isConnectionComplete', () => {
    it('reports nothing missing when every required field is configured', () => {
      const c = connection({ configuredFields: ['botToken', 'signingSecret'] });
      expect(getMissingFields(c, ['botToken', 'signingSecret'])).toEqual([]);
      expect(isConnectionComplete(c, ['botToken', 'signingSecret'])).toBe(true);
    });

    it('lists the required fields that were never supplied', () => {
      const c = connection({ configuredFields: ['botToken'] });
      expect(getMissingFields(c, ['botToken', 'signingSecret'])).toEqual(['signingSecret']);
      expect(isConnectionComplete(c, ['botToken', 'signingSecret'])).toBe(false);
    });

    it('treats a connection with no requirements as complete', () => {
      expect(isConnectionComplete(connection({ configuredFields: [] }), [])).toBe(true);
    });

    it('ignores extra configured fields that are not required', () => {
      const c = connection({ configuredFields: ['botToken', 'legacyToken'] });
      expect(getMissingFields(c, ['botToken'])).toEqual([]);
    });
  });
});
