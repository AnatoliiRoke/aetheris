import { useCallback, useEffect, useRef, useState } from 'react';
import type { Planet } from '../../api/planetApi';
import { useApi } from '../../api/useApi';

/**
 * How often the body is read again while the page is open, in milliseconds: the balance grows
 * on the server and the client only shows it.
 */
const refreshIntervalMs = 15000;

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
 * Reads a body, keeps it fresh and takes the answers of the commands.
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
    const timer = window.setInterval(() => void reload(), refreshIntervalMs);

    return () => {
      active.current = false;
      window.clearInterval(timer);
    };
  }, [reload]);

  return { planet, receivedAt, failed, reload, apply };
}

/**
 * The current time, renewed every second, for the timers of the queue.
 */
export function useNow(): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);

    return () => window.clearInterval(timer);
  }, []);

  return now;
}
