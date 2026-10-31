import { useCallback, useEffect } from 'react';
import type { CurrencyBalanceFacts, PopulationFacts } from '../../api/economyApi';
import { useShell, useShellSelector } from './useShell';

/**
 * The state of the economy on a page: the balances of the currencies of the civilization and,
 * when a body is named, the population of that body.
 */
export interface EconomyState {
  /** The balances of the currencies of the civilization. */
  balances: readonly CurrencyBalanceFacts[];
  /** The population of the body, or `null` when it has none yet. */
  population: PopulationFacts | null;
  /** Whether the answers of the server have arrived. */
  loaded: boolean;
  /** Whether a read failed. */
  failed: boolean;
  /** Asks the server for the economy again. */
  reload: () => Promise<void>;
}

/**
 * Reads the economy of the player from the state the frame shares: the balances and, when a body
 * is named, the population of that body. The values grow on the server, so they are read again on
 * the events of the page (the shared watch of the tab, a command), never on a timer.
 */
export function useEconomy(planetId: number | null = null): EconomyState {
  const { actions } = useShell();
  const balances = useShellSelector((state) => state.balances);
  const population = useShellSelector((state) => (planetId === null ? null : state.populations.get(planetId) ?? null));
  const economyLoaded = useShellSelector((state) => state.economyLoaded);
  const populationLoaded = useShellSelector((state) => planetId === null || state.populations.has(planetId));
  const failed = useShellSelector((state) => state.economyFailed);

  useEffect(() => {
    void actions.loadEconomy();
  }, [actions]);

  useEffect(() => {
    if (planetId !== null) {
      void actions.loadPopulation(planetId);
    }
  }, [actions, planetId]);

  const reload = useCallback(async () => {
    await Promise.all([actions.loadEconomy(true), planetId === null ? undefined : actions.loadPopulation(planetId, true)]);
  }, [actions, planetId]);

  return { balances, population, loaded: economyLoaded && populationLoaded, failed, reload };
}
