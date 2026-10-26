import { createContext } from 'react';
import type { Me } from '../api/authApi';

/**
 * Whether a player is signed in, and who.
 */
export type SessionState = { status: 'loading' } | { status: 'anonymous' } | { status: 'signed-in'; me: Me };

/**
 * What the components get from the session provider.
 */
export interface SessionValue {
  session: SessionState;
  refresh: () => Promise<void>;
  signOut: () => Promise<void>;
  /** Forgets the session without asking the server, after the server has ended it. */
  forget: () => void;
}

export const SessionContext = createContext<SessionValue | null>(null);
