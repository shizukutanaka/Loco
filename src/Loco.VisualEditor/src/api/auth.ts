/**
 * Signing in.
 *
 * Every controller on the API carries [Authorize] and the server registers
 * exactly one authentication scheme, JWT bearer. The editor had no way to
 * obtain a token: no sign-in, no call to apiClient.setToken, nothing. What it
 * offered instead was an "API Key" field whose value went out as an X-API-Key
 * header the server does not read - and, because the client sent it INSTEAD of
 * the Authorization header, filling that field in was worse than leaving it
 * empty.
 *
 * So the editor could not talk to its own API at all. This is the path that
 * makes it possible: exchange a username and password for a bearer token, the
 * same exchange LocoApiFactory performs in the API tests.
 *
 * The token is held in localStorage. That is readable by any script running on
 * the page, which is the accepted trade for a single-page app that must
 * survive a reload; the alternative - an httpOnly cookie - needs the server to
 * set it, and this API issues bearer tokens.
 */

import { apiClient } from './client';
import { ApiResponse } from './types';

const TOKEN_KEY = 'loco_token';

/** What POST /api/v1/authentication/token answers with. */
export interface TokenResponse {
  accessToken: string;
  tokenType: string;
  /** Seconds until the token expires. */
  expiresIn: number;
  /** Space-separated scopes, e.g. "workflows:read workflows:manage". */
  scope: string;
}

interface StoredToken {
  accessToken: string;
  /** Epoch milliseconds. */
  expiresAt: number;
  scope: string;
}

/**
 * Exchange credentials for a bearer token and apply it to the shared client.
 *
 * Returns the API's own envelope so a caller can show the server's message -
 * "Invalid credentials" and "No users are configured" are different problems
 * and the second one is not the user's fault.
 */
export async function signIn(
  username: string,
  password: string
): Promise<ApiResponse<TokenResponse>> {
  const response = await apiClient.post<TokenResponse>('/authentication/token', {
    username,
    password,
  });

  if (response.success) {
    store({
      accessToken: response.data.accessToken,
      expiresAt: Date.now() + response.data.expiresIn * 1000,
      scope: response.data.scope,
    });
    apiClient.setToken(response.data.accessToken);
  }

  return response;
}

/** Forget the token and stop sending it. */
export function signOut(): void {
  try {
    localStorage.removeItem(TOKEN_KEY);
  } catch {
    // A browser with storage disabled still signs out for this session.
  }
  apiClient.clearAuth();
}

/**
 * Re-apply a stored token at startup.
 *
 * An expired token is discarded rather than sent: the request would 401 and
 * the user would be told their credentials are wrong, when the truth is that
 * their session ended.
 */
export function restoreSession(): boolean {
  const token = read();
  if (!token) return false;

  if (token.expiresAt <= Date.now()) {
    signOut();
    return false;
  }

  apiClient.setToken(token.accessToken);
  return true;
}

/** Whether a usable token is held right now. */
export function isSignedIn(): boolean {
  const token = read();
  return token !== null && token.expiresAt > Date.now();
}

/** The scopes the current token carries, for hiding actions it cannot perform. */
export function currentScopes(): string[] {
  const token = read();
  if (!token || token.expiresAt <= Date.now()) return [];
  return token.scope.split(' ').filter(Boolean);
}

function store(token: StoredToken): void {
  try {
    localStorage.setItem(TOKEN_KEY, JSON.stringify(token));
  } catch {
    // Storage can be unavailable (private mode, quota). The token still works
    // for this session; it just will not survive a reload.
  }
}

function read(): StoredToken | null {
  try {
    const raw = localStorage.getItem(TOKEN_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as Partial<StoredToken>;
    if (typeof parsed.accessToken !== 'string' || typeof parsed.expiresAt !== 'number') {
      return null;
    }

    return {
      accessToken: parsed.accessToken,
      expiresAt: parsed.expiresAt,
      scope: typeof parsed.scope === 'string' ? parsed.scope : '',
    };
  } catch {
    return null;
  }
}
