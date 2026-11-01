import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react';
import type { MapTarget } from './mapLocation';
import { MapTargetContext, type MapTargetValue } from './useMapTarget';

/**
 * Holds the target of a flight above the screens of the game, so the screen of the fleets can
 * read what the map chose without the map knowing it.
 *
 * @param children The screens.
 */
export function MapTargetProvider({ children }: { children: ReactNode }) {
  const [target, setTargetState] = useState<MapTarget | null>(null);
  const [picking, setPicking] = useState(false);
  const subscribers = useRef(new Set<(target: MapTarget) => void>());
  const targetRef = useRef<MapTarget | null>(null);

  const setTarget = useCallback((next: MapTarget | null) => {
    targetRef.current = next;
    setTargetState(next);
  }, []);
  const clearTarget = useCallback(() => setTarget(null), [setTarget]);
  const startPicking = useCallback(() => setPicking(true), []);
  const stopPicking = useCallback(() => setPicking(false), []);
  const confirmTarget = useCallback(() => {
    const current = targetRef.current;

    if (current) {
      subscribers.current.forEach((callback) => callback(current));
    }
  }, []);
  const onTargetConfirmed = useCallback((callback: (target: MapTarget) => void) => {
    subscribers.current.add(callback);

    return () => {
      subscribers.current.delete(callback);
    };
  }, []);

  const value = useMemo<MapTargetValue>(
    () => ({ target, picking, setTarget, clearTarget, startPicking, stopPicking, confirmTarget, onTargetConfirmed }),
    [target, picking, setTarget, clearTarget, startPicking, stopPicking, confirmTarget, onTargetConfirmed],
  );

  return <MapTargetContext.Provider value={value}>{children}</MapTargetContext.Provider>;
}
