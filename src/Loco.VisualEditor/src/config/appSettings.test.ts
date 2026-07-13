import { describe, it, expect, beforeEach, vi } from 'vitest';

// Mock the shared api client so we can assert the wiring without real HTTP.
vi.mock('@/api/client', () => ({
  apiClient: {
    setApiKey: vi.fn(),
    setToken: vi.fn(),
    clearAuth: vi.fn(),
  },
}));

import { apiClient } from '@/api/client';
import { saveSettings, loadSettings, bootstrapSettings, applyApiAuth } from './appSettings';

describe('appSettings API-auth wiring', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('saveSettings persists to localStorage AND applies the API key to the client', () => {
    // This is the core Phase-4 fix: previously the key was saved but never
    // applied, so every request went out anonymous and got a 401.
    saveSettings({ api: { apiKey: 'secret-key', apiBaseUrl: '/api/v1' } });

    expect(loadSettings()?.api?.apiKey).toBe('secret-key');
    expect(apiClient.setApiKey).toHaveBeenCalledWith('secret-key');
  });

  it('applyApiAuth clears auth when the key is empty', () => {
    applyApiAuth({ apiKey: '', apiBaseUrl: '/api/v1' });
    expect(apiClient.clearAuth).toHaveBeenCalled();
    expect(apiClient.setApiKey).not.toHaveBeenCalled();
  });

  it('applyApiAuth trims whitespace-only keys to "no auth"', () => {
    applyApiAuth({ apiKey: '   ', apiBaseUrl: '/api/v1' });
    expect(apiClient.clearAuth).toHaveBeenCalled();
  });

  it('bootstrapSettings restores a previously-saved key at startup', () => {
    localStorage.setItem(
      'loco_settings',
      JSON.stringify({ api: { apiKey: 'restored-key', apiBaseUrl: '/api/v1' } })
    );

    bootstrapSettings();

    expect(apiClient.setApiKey).toHaveBeenCalledWith('restored-key');
  });

  it('bootstrapSettings with no saved settings clears auth (does not throw)', () => {
    expect(() => bootstrapSettings()).not.toThrow();
    expect(apiClient.clearAuth).toHaveBeenCalled();
  });

  it('loadSettings tolerates corrupt JSON', () => {
    localStorage.setItem('loco_settings', '{ not json');
    expect(loadSettings()).toBeNull();
  });
});
