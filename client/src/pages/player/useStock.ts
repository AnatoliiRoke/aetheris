import { useEffect, useState } from 'react';
import type { ProductionStock } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { onTabVisible } from './tabVisibility';

/**
 * The stock of the parts of a body: the stacks and the storage of the parts.
 */
export interface StockState {
  stock: ProductionStock | null;
  loaded: boolean;
  failed: boolean;
}

/**
 * Reads the stock of the parts of a body and reads it again when the tab becomes visible.
 *
 * @param planetId The body, or `null` while it is not known.
 */
export function useStock(planetId: number | null): StockState {
  const { production } = useApi();
  const [state, setState] = useState<StockState>({ stock: null, loaded: false, failed: false });

  useEffect(() => {
    if (planetId === null) {
      return undefined;
    }

    let cancelled = false;

    const read = () =>
      production
        .stock(planetId)
        .then((stock) => {
          if (!cancelled) {
            setState({ stock, loaded: true, failed: false });
          }
        })
        .catch(() => {
          if (!cancelled) {
            setState((previous) => ({ ...previous, loaded: true, failed: true }));
          }
        });

    void read();
    const stopWatching = onTabVisible(() => void read());

    return () => {
      cancelled = true;
      stopWatching();
    };
  }, [planetId, production]);

  return state;
}
