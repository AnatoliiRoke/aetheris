import { useCallback, useEffect, useState } from 'react';
import type { Planet } from '../../api/planetApi';
import { useShell, useShellSelector } from './useShell';

/**
 * The state of a body on a page: the last answer of the server and the moment it came.
 */
export interface PlanetState {
  planet: Planet | null;
  receivedAt: number;
  failed: boolean;
  reload: () => Promise<void>;
  apply: (planet: Planet) => void;
}

/**
 * Reads a body from the state the frame shares: the body is asked for once for the header and
 * for every page, and it is read again only on the events of the page or after a command. The
 * answers of the commands are taken through `apply`.
 */
export function usePlanet(planetId: number | null): PlanetState {
  const { actions } = useShell();
  const entry = useShellSelector((state) => (planetId === null ? null : state.planets.get(planetId) ?? null));
  const failed = useShellSelector((state) => planetId !== null && state.failedPlanetIds.includes(planetId));
  const [fallbackTime] = useState(() => Date.now());

  const reload = useCallback(async () => {
    if (planetId !== null) {
      await actions.loadPlanet(planetId, true);
    }
  }, [actions, planetId]);

  const apply = useCallback(
    (next: Planet) => {
      if (planetId !== null) {
        actions.applyPlanet(planetId, next);
      }
    },
    [actions, planetId],
  );

  useEffect(() => {
    if (planetId !== null) {
      void actions.loadPlanet(planetId);
    }
  }, [actions, planetId]);

  return { planet: entry?.planet ?? null, receivedAt: entry?.receivedAt ?? fallbackTime, failed, reload, apply };
}
