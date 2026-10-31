import { useCallback, useEffect, useState } from 'react';
import type { MarketOverview } from '../../../api/marketApi';
import { clockTickMs } from '../clock';
import type { ReadState } from './useRead';

/**
 * The game time now in milliseconds, counted from the moment of game time of the last answer of the
 * server and the real time that has passed since, at the speed of the game.
 *
 * @param overview The read of the overview of the market.
 * @returns A function that gives the game time now, or `null` before the answer.
 */
export function useGameClock(overview: ReadState<MarketOverview>): () => number | null {
  const { data, receivedAt } = overview;

  return useCallback(
    () => (data && receivedAt !== null ? Date.parse(data.gameNow) + (Date.now() - receivedAt) * data.timeMultiplier : null),
    [data, receivedAt],
  );
}

/**
 * Draws the component again once a second while it is on, and never asks the server for anything.
 *
 * @param on Whether the clock runs.
 * @returns The number of ticks, which changes every second.
 */
export function useTick(on: boolean): number {
  const [tick, setTick] = useState(0);

  useEffect(() => {
    if (!on) {
      return undefined;
    }

    const timer = window.setInterval(() => setTick((value) => value + 1), clockTickMs);

    return () => window.clearInterval(timer);
  }, [on]);

  return tick;
}
