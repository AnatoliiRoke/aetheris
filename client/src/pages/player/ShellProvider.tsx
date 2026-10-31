import { useEffect, useMemo, type ReactNode } from 'react';
import { useApi } from '../../api/useApi';
import { useSession } from '../../session/useSession';
import { ShellContext } from './shellContext';
import { createShellStore } from './shellStore';
import { onTabVisible } from './tabVisibility';

/**
 * Holds the shared state of the frame of the screens of the game for the signed-in player. The
 * state is made again only for another player, and the server is read again on the events of the
 * page, never on a timer: the one watch of the tab reads again what was read before.
 */
export function ShellProvider({ children }: { children: ReactNode }) {
  const api = useApi();
  const { session } = useSession();
  const userId = session.status === 'signed-in' ? session.me.userId : null;
  // The state belongs to the player: another player starts with nothing.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const store = useMemo(() => createShellStore(api), [api, userId]);

  useEffect(() => onTabVisible(() => void store.actions.refreshAll()), [store]);

  return <ShellContext.Provider value={store}>{children}</ShellContext.Provider>;
}
