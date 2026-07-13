/**
 * Centralized access to persisted app settings and the wiring that applies
 * them to the API client.
 *
 * Two bugs this fixes:
 *  - SettingsPanel saved the API key to localStorage but never called
 *    apiClient.setApiKey(), so every request went out unauthenticated and the
 *    (now [Authorize]'d) backend returned 401 for everything.
 *  - There was no restore-on-startup, so even a correctly-applied key was lost
 *    on reload.
 */

import { apiClient } from '@/api/client';

const STORAGE_KEY = 'loco_settings';

export interface ApiSettings {
  apiKey: string;
  apiBaseUrl: string;
}

export interface PersistedSettings {
  general?: Record<string, unknown>;
  api?: ApiSettings;
  appearance?: Record<string, unknown>;
  notifications?: Record<string, unknown>;
  environment?: unknown;
}

export function loadSettings(): PersistedSettings | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as PersistedSettings) : null;
  } catch {
    return null;
  }
}

export function saveSettings(settings: PersistedSettings): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
  applyApiAuth(settings.api);
}

/**
 * Apply the persisted API credential to the shared client. An empty/absent key
 * clears any previously-applied credential so switching to "no auth" actually
 * takes effect.
 */
export function applyApiAuth(api?: ApiSettings): void {
  const key = api?.apiKey?.trim();
  if (key) {
    apiClient.setApiKey(key);
  } else {
    apiClient.clearAuth();
  }
}

/**
 * Called once at startup (main.tsx) to restore the API credential from the
 * previous session before the first request is made.
 */
export function bootstrapSettings(): void {
  const settings = loadSettings();
  applyApiAuth(settings?.api);
}
