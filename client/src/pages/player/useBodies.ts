import { useCallback, useEffect } from 'react';
import type { OwnedPlanet, WorldReference } from '../../api/worldApi';
import { useShell, useShellSelector } from './useShell';

/**
 * The state of the bodies of the player on a screen: the bodies themselves and the reference
 * tables of the world, which name them and give their pictures (task 00010, section 7.3).
 */
export interface BodiesState {
  /** The bodies of the player. */
  bodies: readonly OwnedPlanet[];
  /** The reference tables of the world, or `null` when they have not arrived. */
  reference: WorldReference | null;
  /** Whether the answers of the server have arrived. */
  loaded: boolean;
  /** Whether a read failed. */
  failed: boolean;
  /** Asks the server for the bodies and the reference tables again. */
  reload: () => Promise<void>;
}

/**
 * Reads the bodies of the player and the reference tables of the world from the state the frame
 * shares, so the bottom bar and the screens ask the server once. The tables never change while
 * the screen is open, so they are read once.
 */
export function useBodies(): BodiesState {
  const { actions } = useShell();
  const bodies = useShellSelector((state) => state.bodies);
  const reference = useShellSelector((state) => state.reference);
  const loaded = useShellSelector((state) => state.bodiesLoaded);
  const failed = useShellSelector((state) => state.bodiesFailed);

  useEffect(() => {
    void actions.loadBodies();
  }, [actions]);

  const reload = useCallback(() => actions.loadBodies(true), [actions]);

  return { bodies, reference, loaded, failed, reload };
}
