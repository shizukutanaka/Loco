import { describe, it, expect, beforeEach, vi } from 'vitest';

const post = vi.fn();
const setToken = vi.fn();
const clearAuth = vi.fn();

vi.mock('./client', () => ({
  apiClient: {
    post: (...args: unknown[]) => post(...args),
    setToken: (...args: unknown[]) => setToken(...args),
    clearAuth: () => clearAuth(),
  },
}));

import { signIn, signOut, restoreSession, isSignedIn, currentScopes } from './auth';

/**
 * The editor had no way to obtain a token at all: no sign-in, no call to
 * setToken. Every controller on the API carries [Authorize] and the server
 * issues JWT bearer tokens, so the editor could not make one successful
 * request. What it offered instead was an "API Key" field whose value went out
 * as a header the server does not read.
 *
 * These pin the exchange that replaced it, and the two edges that decide
 * whether a returning user sees the canvas or the sign-in dialog.
 */
describe('signIn', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  const ok = (overrides: Record<string, unknown> = {}) => ({
    success: true as const,
    data: {
      accessToken: 'a-token',
      tokenType: 'Bearer',
      expiresIn: 3600,
      scope: 'workflows:read workflows:manage',
      ...overrides,
    },
  });

  it('exchanges credentials at the endpoint the API actually exposes', async () => {
    post.mockResolvedValue(ok());

    await signIn('admin', 'hunter2');

    expect(post).toHaveBeenCalledWith('/authentication/token', {
      username: 'admin',
      password: 'hunter2',
    });
  });

  it('applies the token to the client so later requests carry it', async () => {
    post.mockResolvedValue(ok());

    await signIn('admin', 'hunter2');

    expect(setToken).toHaveBeenCalledWith('a-token');
    expect(isSignedIn()).toBe(true);
  });

  it('turns expiresIn into an absolute expiry', async () => {
    // Stored as a deadline rather than a duration: a duration is meaningless
    // after a reload, which is exactly when it gets read.
    post.mockResolvedValue(ok({ expiresIn: 60 }));

    const before = Date.now();
    await signIn('admin', 'hunter2');

    const stored = JSON.parse(localStorage.getItem('loco_token')!);
    expect(stored.expiresAt).toBeGreaterThanOrEqual(before + 60_000);
  });

  it('does not store anything when the credentials are refused', async () => {
    post.mockResolvedValue({
      success: false,
      error: { code: 'UNAUTHORIZED', message: 'Invalid credentials' },
    });

    const result = await signIn('admin', 'wrong');

    expect(result.success).toBe(false);
    expect(setToken).not.toHaveBeenCalled();
    expect(localStorage.getItem('loco_token')).toBeNull();
  });

  it('returns the server error so the caller can show it', async () => {
    // "Invalid credentials" and "No users are configured" are different
    // problems, and only one of them is the user's fault.
    post.mockResolvedValue({
      success: false,
      error: { code: 'AUTH_NOT_CONFIGURED', message: 'No users are configured.' },
    });

    const result = await signIn('admin', 'hunter2');

    expect(result.success).toBe(false);
    if (!result.success) expect(result.error.message).toBe('No users are configured.');
  });

  it('exposes the granted scopes', async () => {
    post.mockResolvedValue(ok());
    await signIn('admin', 'hunter2');

    expect(currentScopes()).toEqual(['workflows:read', 'workflows:manage']);
  });
});

describe('restoreSession', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  const store = (token: Record<string, unknown>) =>
    localStorage.setItem('loco_token', JSON.stringify(token));

  it('re-applies a token that is still valid', () => {
    store({ accessToken: 'kept', expiresAt: Date.now() + 60_000, scope: '' });

    expect(restoreSession()).toBe(true);
    expect(setToken).toHaveBeenCalledWith('kept');
  });

  it('discards an expired token rather than sending it', () => {
    store({ accessToken: 'stale', expiresAt: Date.now() - 1, scope: '' });

    expect(restoreSession()).toBe(false);
    expect(setToken).not.toHaveBeenCalled();
    expect(localStorage.getItem('loco_token')).toBeNull();
  });

  it('reports no session when nothing is stored', () => {
    expect(restoreSession()).toBe(false);
  });

  it('treats a malformed entry as no session', () => {
    localStorage.setItem('loco_token', '{ not json');

    expect(restoreSession()).toBe(false);
    expect(isSignedIn()).toBe(false);
  });

  it('treats an entry missing its token as no session', () => {
    store({ expiresAt: Date.now() + 60_000, scope: '' });

    expect(restoreSession()).toBe(false);
  });
});

describe('signOut', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('forgets the token and stops sending it', () => {
    localStorage.setItem(
      'loco_token',
      JSON.stringify({ accessToken: 't', expiresAt: Date.now() + 60_000, scope: '' })
    );

    signOut();

    expect(localStorage.getItem('loco_token')).toBeNull();
    expect(clearAuth).toHaveBeenCalled();
    expect(isSignedIn()).toBe(false);
  });
});
