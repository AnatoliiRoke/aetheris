import { useCallback, useContext, useRef, useSyncExternalStore } from 'react';
import { useSession } from '../../session/useSession';
import { ShellContext } from './shellContext';
import type { ShellActions, ShellState, ShellStore } from './shellStore';

/**
 * The shared state of the frame and its actions. The value never changes.
 */
export function useShell(): ShellStore {
  const store = useContext(ShellContext);

  if (!store) {
    throw new Error('useShell must be used inside ShellProvider.');
  }

  return store;
}

/**
 * The actions of the shared state, for a page that sends a command.
 */
export function useShellActions(): ShellActions {
  return useShell().actions;
}

/**
 * Two values that say the same when written as JSON; for a small selection of the state.
 */
export function sameJson<T>(first: T, second: T): boolean {
  return Object.is(first, second) || JSON.stringify(first) === JSON.stringify(second);
}

/**
 * Reads a part of the shared state. The component is drawn again only when the part it selected
 * changed, so the header does not follow the page and the page does not follow the header.
 *
 * @param selector Takes the part of the state; it may be written inline.
 * @param isEqual Tells whether two selections are the same; the identity by default.
 */
export function useShellSelector<T>(selector: (state: ShellState) => T, isEqual: (first: T, second: T) => boolean = Object.is): T {
  const store = useShell();
  const last = useRef<{ value: T } | null>(null);

  const getSnapshot = useCallback(() => {
    const next = selector(store.getState());

    if (last.current && isEqual(last.current.value, next)) {
      return last.current.value;
    }

    last.current = { value: next };

    return next;
  }, [store, selector, isEqual]);

  return useSyncExternalStore(store.subscribe, getSnapshot, getSnapshot);
}

/**
 * The body the player works with: the one chosen in the bottom bar, or the home body of the
 * civilization while none was chosen. Every screen of a body reads this one.
 */
export function useCurrentPlanetId(): number | null {
  const { session } = useSession();
  const selected = useShellSelector((state) => state.selectedPlanetId);
  const home = session.status === 'signed-in' ? session.me.civilization?.planetId ?? null : null;

  return selected ?? home;
}
