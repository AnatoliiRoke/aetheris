import { useCallback, useEffect, useRef, useState } from 'react';
import type { Planet } from '../../api/planetApi';
import { useApi } from '../../api/useApi';
import { useReloadOnVisible } from './tabVisibility';

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
 * Reads a body, reads it again on the events of the page and takes the answers of the commands.
 */
export function usePlanet(planetId: number | null): PlanetState {
  const { planets } = useApi();
  const [planet, setPlanet] = useState<Planet | null>(null);
  const [receivedAt, setReceivedAt] = useState(() => Date.now());
  const [failed, setFailed] = useState(false);
  const active = useRef(true);

  const apply = useCallback((next: Planet) => {
    if (active.current) {
      setPlanet(next);
      setReceivedAt(Date.now());
      setFailed(false);
    }
  }, []);

  const reload = useCallback(async () => {
    if (planetId === null) {
      return;
    }

    try {
      apply(await planets.planet(planetId));
    } catch {
      if (active.current) {
        setFailed(true);
      }
    }
  }, [planetId, planets, apply]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  useReloadOnVisible(reload);

  return { planet, receivedAt, failed, reload, apply };
}
