import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { Me } from '../api/authApi';
import { useApi } from '../api/useApi';
import { SessionContext, type SessionState, type SessionValue } from './context';

const toState = (me: Me | null): SessionState => (me ? { status: 'signed-in', me } : { status: 'anonymous' });

/**
 * Asks the server who is signed in. The session itself lives in `HttpOnly` cookies that
 * the page cannot read, so the server is the only source of truth.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const { auth } = useApi();
  const [session, setSession] = useState<SessionState>({ status: 'loading' });

  const refresh = useCallback(async () => {
    try {
      setSession(toState(await auth.me()));
    } catch {
      setSession({ status: 'anonymous' });
    }
  }, [auth]);

  const signOut = useCallback(async () => {
    try {
      await auth.logout();
    } finally {
      setSession({ status: 'anonymous' });
    }
  }, [auth]);

  const forget = useCallback(() => setSession({ status: 'anonymous' }), []);

  useEffect(() => {
    let current = true;

    auth.me().then(
      (me) => current && setSession(toState(me)),
      () => current && setSession({ status: 'anonymous' }),
    );

    return () => {
      current = false;
    };
  }, [auth]);

  const value = useMemo<SessionValue>(() => ({ session, refresh, signOut, forget }), [session, refresh, signOut, forget]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}
