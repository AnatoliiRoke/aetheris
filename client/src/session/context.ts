import { createContext } from 'react';

/**
 * Whether a player is signed in.
 */
export type SessionState = { status: 'loading' } | { status: 'anonymous' } | { status: 'signed-in'; userId: string };

/**
 * What the components get from the session provider.
 */
export interface SessionValue {
  session: SessionState;
  refresh: () => Promise<void>;
  signOut: () => Promise<void>;
}

export const SessionContext = createContext<SessionValue | null>(null);
