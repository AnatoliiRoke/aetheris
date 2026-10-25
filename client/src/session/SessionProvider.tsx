import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useApi } from '../api/useApi';
import { SessionContext, type SessionState, type SessionValue } from './context';

/**
 * Asks the server who is signed in. The session itself lives in `HttpOnly` cookies that
 * the page cannot read, so the server is the only source of truth.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const { auth } = useApi();
  const [session, setSession] = useState<SessionState>({ status: 'loading' });

  const refresh = useCallback(async () => {
    try {
      const me = await auth.me();
      setSession(me ? { status: 'signed-in', userId: me.userId } : { status: 'anonymous' });
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

  useEffect(() => {
    let current = true;

    auth.me().then(
      (me) => current && setSession(me ? { status: 'signed-in', userId: me.userId } : { status: 'anonymous' }),
      () => current && setSession({ status: 'anonymous' }),
    );

    return () => {
      current = false;
    };
  }, [auth]);

  const value = useMemo<SessionValue>(() => ({ session, refresh, signOut }), [session, refresh, signOut]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}
