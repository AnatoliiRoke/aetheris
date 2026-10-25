import { useContext } from 'react';
import { SessionContext, type SessionValue } from './context';

/**
 * The current session.
 */
export function useSession(): SessionValue {
  const value = useContext(SessionContext);

  if (!value) {
    throw new Error('useSession must be used inside SessionProvider.');
  }

  return value;
}
