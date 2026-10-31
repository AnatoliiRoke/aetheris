import { useCallback, useEffect, useRef, useState } from 'react';
import type { CurrencyBalanceFacts, EconomyOverview, PopulationFacts } from '../../api/economyApi';
import { ApiError } from '../../api/http';
import { useApi } from '../../api/useApi';
import { useReloadOnVisible } from './tabVisibility';

/**
 * The state of the economy on a page: the balances of the currencies of the civilization and,
 * when a body is named, the population of that body.
 */
export interface EconomyState {
  /** The balances of the currencies of the civilization. */
  balances: CurrencyBalanceFacts[];
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
 * Reads the economy of the player: the balances of the currencies and, when a body is named,
 * the population of that body. The values grow on the server, so they are read again and again.
 */
export function useEconomy(planetId: number | null = null): EconomyState {
  const { economy } = useApi();
  const [balances, setBalances] = useState<CurrencyBalanceFacts[]>([]);
  const [population, setPopulation] = useState<PopulationFacts | null>(null);
  const [overviewLoaded, setOverviewLoaded] = useState(false);
  const [populationLoaded, setPopulationLoaded] = useState(false);
  const [failed, setFailed] = useState(false);
  const active = useRef(true);

  const applyOverview = useCallback((answer: EconomyOverview) => {
    if (active.current) {
      setBalances(answer.balances);
      setOverviewLoaded(true);
      setFailed(false);
    }
  }, []);

  const applyPopulation = useCallback((facts: PopulationFacts | null) => {
    if (active.current) {
      setPopulation(facts);
      setPopulationLoaded(true);
    }
  }, []);

  const applyFailure = useCallback(() => {
    if (active.current) {
      setFailed(true);
    }
  }, []);

  const reload = useCallback(async () => {
    const overviewRequest = economy.overview();
    const populationRequest = planetId === null ? null : economy.population(planetId);
    const [overviewAnswer, populationAnswer] = await Promise.allSettled([overviewRequest, populationRequest]);

    if (!active.current) {
      return;
    }

    if (overviewAnswer.status === 'fulfilled') {
      applyOverview(overviewAnswer.value);
    } else {
      applyFailure();
    }

    if (planetId === null) {
      return;
    }

    if (populationAnswer.status === 'fulfilled' && populationAnswer.value !== null) {
      applyPopulation(populationAnswer.value);
      return;
    }

    applyPopulation(null);

    // The body has no population yet: the server answers 404 and the panel shows a
    // placeholder, not a made-up number (task 00010).
    const error = populationAnswer.status === 'rejected' ? populationAnswer.reason : null;

    if (!(error instanceof ApiError && error.status === 404)) {
      applyFailure();
    }
  }, [economy, planetId, applyOverview, applyPopulation, applyFailure]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  useReloadOnVisible(reload);

  const loaded = overviewLoaded && (planetId === null || populationLoaded);

  return { balances, population, loaded, failed, reload };
}
