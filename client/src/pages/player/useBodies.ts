import { useCallback, useEffect, useRef, useState } from 'react';
import type { OwnedPlanet, WorldReference } from '../../api/worldApi';
import { useApi } from '../../api/useApi';
import { useReloadOnVisible } from './tabVisibility';

/**
 * The state of the bodies of the player on a screen: the bodies themselves and the reference
 * tables of the world, which name them and give their pictures (task 00010, section 7.3).
 */
export interface BodiesState {
  /** The bodies of the player. */
  bodies: OwnedPlanet[];
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
 * Reads the bodies of the player and the reference tables of the world, and reads them again on the events of the page.
 * The tables never change while the screen is open, so they are read once.
 */
export function useBodies(): BodiesState {
  const { world } = useApi();
  const [bodies, setBodies] = useState<OwnedPlanet[]>([]);
  const [reference, setReference] = useState<WorldReference | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);
  const active = useRef(true);

  const apply = useCallback((answer: OwnedPlanet[], tables: WorldReference) => {
    if (active.current) {
      setBodies(answer);
      setReference(tables);
      setLoaded(true);
      setFailed(false);
    }
  }, []);

  const applyFailure = useCallback(() => {
    if (active.current) {
      setFailed(true);
      setLoaded(true);
    }
  }, []);

  const reload = useCallback(async () => {
    try {
      const [answer, tables] = await Promise.all([world.planets(), world.reference()]);

      apply(answer, tables);
    } catch {
      applyFailure();
    }
  }, [world, apply, applyFailure]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  useReloadOnVisible(reload);

  return { bodies, reference, loaded, failed, reload };
}
