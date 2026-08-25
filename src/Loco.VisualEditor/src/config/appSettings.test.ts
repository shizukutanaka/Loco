import { describe, it, expect, beforeEach, vi } from 'vitest';

// Mock the shared api client so the wiring can be asserted without real HTTP.
vi.mock('@/api/client', () => ({
  apiClient: {
    setToken: vi.fn(),
    clearAuth: vi.fn(),
    post: vi.fn(),
  },
}));

import { apiClient } from '@/api/client';
import { saveSettings, loadSettings, bootstrapSettings } from './appSettings';

/**
 * These used to assert that an API key was saved and applied to the client.
 * That was the wrong contract: the API registers exactly one authentication
 * scheme, JWT bearer, and reads no X-API-Key header - so the key
 * authenticated nothing, and the client sent it INSTEAD of the Authorization
 * header, meaning that filling the field in broke a session that worked.
 *
 * Settings now hold settings. Sessions belong to api/auth.ts, and what
 * bootstrapSettings does is re-apply a stored bearer token.
 */
describe('appSettings', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('persists settings to localStorage', () => {
    saveSettings({ api: { apiBaseUrl: '/api/v1' } });

    expect(loadSettings()?.api?.apiBaseUrl).toBe('/api/v1');
  });

  it('does not touch authentication when settings are saved', () => {
    // Saving a preference must not disturb a signed-in session, which is
    // exactly what the old apiKey path did.
    saveSettings({ api: { apiBaseUrl: '/api/v1' } });

    expect(apiClient.clearAuth).not.toHaveBeenCalled();
    expect(apiClient.setToken).not.toHaveBeenCalled();
  });

  it('restores a stored bearer token at startup', () => {
    localStorage.setItem(
      'loco_token',
      JSON.stringify({
        accessToken: 'stored-token',
        expiresAt: Date.now() + 60_000,
        scope: 'workflows:read',
      })
    );

    expect(bootstrapSettings()).toBe(true);
    expect(apiClient.setToken).toHaveBeenCalledWith('stored-token');
  });

  it('reports no session when nothing is stored', () => {
    expect(bootstrapSettings()).toBe(false);
    expect(apiClient.setToken).not.toHaveBeenCalled();
  });

  it('discards an expired token instead of sending it', () => {
    // A request with an expired token 401s, and the user would be told their
    // credentials are wrong when in fact their session simply ended.
    localStorage.setItem(
      'loco_token',
      JSON.stringify({
        accessToken: 'stale-token',
        expiresAt: Date.now() - 1,
        scope: '',
      })
    );

    expect(bootstrapSettings()).toBe(false);
    expect(apiClient.setToken).not.toHaveBeenCalled();
    expect(localStorage.getItem('loco_token')).toBeNull();
  });

  it('tolerates corrupt JSON', () => {
    localStorage.setItem('loco_settings', '{ not json');
    expect(loadSettings()).toBeNull();
  });
});
