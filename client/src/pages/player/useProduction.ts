import { useCallback, useEffect, useRef, useState } from 'react';
import type { ProductionStock } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { useReloadOnVisible } from './tabVisibility';

/**
 * The state of the stock of the parts on the planet screen: the stacks and the storage.
 */
export interface ProductionState {
  stock: ProductionStock | null;
  loaded: boolean;
  failed: boolean;
  reload: () => Promise<void>;
}

/**
 * Reads the stock of the parts of a body and reads it again on the events of the page (a
 * command, the tab becoming visible), never on a timer. The recipes and the factories are on the
 * production screen (task 00032).
 *
 * @param planetId The body, or `null` while it is not known.
 */
export function useProduction(planetId: number | null): ProductionState {
  const { production } = useApi();
  const [stock, setStock] = useState<ProductionStock | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);
  const active = useRef(true);

  const apply = useCallback((next: ProductionStock) => {
    if (active.current) {
      setStock(next);
      setLoaded(true);
      setFailed(false);
    }
  }, []);

  const reload = useCallback(async () => {
    if (planetId === null) {
      return;
    }

    try {
      apply(await production.stock(planetId));
    } catch {
      if (active.current) {
        setLoaded(true);
        setFailed(true);
      }
    }
  }, [planetId, production, apply]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  useReloadOnVisible(reload);

  return { stock, loaded, failed, reload };
}
