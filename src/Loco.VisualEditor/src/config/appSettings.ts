/**
 * Centralized access to persisted app settings.
 *
 * Authentication used to live here, as an "API key" applied to the client on
 * startup. That was built on a wrong premise: the API registers exactly one
 * authentication scheme, JWT bearer, and reads no X-API-Key header - so the
 * key authenticated nothing, and because the client sent it INSTEAD of the
 * Authorization header, setting one could only make things worse.
 *
 * Sessions now belong to api/auth.ts, which exchanges a username and password
 * for a real bearer token. What is left here is what it says: settings.
 */

import { restoreSession } from '@/api/auth';

const STORAGE_KEY = 'loco_settings';

export interface ApiSettings {
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
}

/**
 * Called once at startup (main.tsx), before the first request is made.
 *
 * Returns whether a usable session was restored, so the app can decide
 * between showing the canvas and asking the user to sign in.
 */
export function bootstrapSettings(): boolean {
  return restoreSession();
}
