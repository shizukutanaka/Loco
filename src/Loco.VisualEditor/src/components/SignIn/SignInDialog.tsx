import { useCallback, useState } from 'react';
import { LogIn, AlertCircle } from 'lucide-react';
import { FormInput } from '@/components/Form';
import { signIn } from '@/api/auth';

interface SignInDialogProps {
  isOpen: boolean;
  onSignedIn: () => void;
}

/**
 * Sign in to the API.
 *
 * Every controller carries [Authorize] and the server issues JWT bearer
 * tokens, so without this the editor could not make a single successful
 * request - it had no sign-in at all, only an "API Key" field the server
 * ignored. The exchange here is the same one the API's own tests perform:
 * POST /api/v1/authentication/token with a username and password.
 *
 * Deliberately not dismissible. There is nothing the editor can usefully do
 * unauthenticated - every list, save and run would 401 - so offering a way to
 * close it would only produce a canvas that fails on contact.
 *
 * Users live in the server's configuration (Auth:Users), with PBKDF2-hashed
 * passwords. There is no registration endpoint, and this does not pretend
 * otherwise.
 */
export function SignInDialog({ isOpen, onSignedIn }: SignInDialogProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const handleSubmit = useCallback(
    async (event: React.FormEvent) => {
      event.preventDefault();

      if (!username.trim() || !password) {
        setError('Enter a username and password');
        return;
      }

      setBusy(true);
      setError(null);
      try {
        const response = await signIn(username.trim(), password);
        if (response.success) {
          setPassword('');
          onSignedIn();
        } else {
          // The server's own wording: "Invalid credentials" and "No users are
          // configured" are different problems, and only one is the user's.
          setError(response.error.message);
        }
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not reach the API');
      } finally {
        setBusy(false);
      }
    },
    [username, password, onSignedIn]
  );

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-sm">
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="flex items-center gap-2">
            <LogIn className="w-5 h-5 text-gray-700" />
            <h2 className="text-lg font-semibold text-gray-900">Sign in to Loco</h2>
          </div>

          <p className="text-xs text-gray-600">
            The API requires a signed-in user for every request. Accounts are
            defined in the server&apos;s configuration.
          </p>

          <FormInput
            id="signin-username"
            label="Username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoFocus
          />

          <FormInput
            id="signin-password"
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />

          {error && (
            <div className="text-xs text-red-600 flex items-start gap-1" role="alert">
              <AlertCircle className="w-3 h-3 mt-0.5 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <button
            type="submit"
            disabled={busy}
            className="w-full px-4 py-2 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  );
}

export default SignInDialog;
