import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { Me } from '../api/authApi';
import { useApi } from '../api/useApi';
import { serverConfig } from '../config/server';
import { SessionContext, type SessionState, type SessionValue } from './context';

const toState = (me: Me | null): SessionState => (me ? { status: 'signed-in', me } : { status: 'anonymous' });

/**
 * Asks the server who is signed in. The session itself lives in `HttpOnly` cookies that
 * the page cannot read, so the server is the only source of truth.
 *
 * A server that does not answer (it is starting, the network is down) is not the same as nobody
 * signed in: the page stays in the loading state and asks again after a pause (the last pause
 * repeats), so a tab that was open while the server restarted finds its player by itself and does
 * not send them to the sign-in page. Only the answer "not signed in" makes the session anonymous.
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
    let attempt = 0;
    let timer: ReturnType<typeof setTimeout> | null = null;

    const load = () =>
      auth.me().then(
        (me) => {
          if (current) {
            setSession(toState(me));
          }
        },
        () => {
          if (!current) {
            return;
          }

          const pauses = serverConfig.retryDelaysMs;

          timer = setTimeout(() => void load(), pauses[Math.min(attempt, pauses.length - 1)]);
          attempt += 1;
        },
      );

    void load();

    return () => {
      current = false;

      if (timer) {
        clearTimeout(timer);
      }
    };
  }, [auth]);

  const value = useMemo<SessionValue>(() => ({ session, refresh, signOut, forget }), [session, refresh, signOut, forget]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}
