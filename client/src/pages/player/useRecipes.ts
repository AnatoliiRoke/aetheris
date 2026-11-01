import { useCallback, useEffect, useRef, useState } from 'react';
import type { ProductionRecipeList } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { useReloadOnVisible } from './tabVisibility';

/**
 * The recipes of every factory of a body with the factories it has.
 */
export interface RecipesState {
  list: ProductionRecipeList | null;
  failed: boolean;
  reload: () => Promise<void>;
}

/**
 * Reads every recipe of a body and reads it again on the events of the page (a command, a
 * finished batch, the tab becoming visible), never on a timer.
 *
 * @param planetId The body, or `null` while it is not known.
 */
export function useRecipes(planetId: number | null): RecipesState {
  const { production } = useApi();
  const [list, setList] = useState<ProductionRecipeList | null>(null);
  const [failed, setFailed] = useState(false);
  const active = useRef(true);

  const apply = useCallback((next: ProductionRecipeList) => {
    if (active.current) {
      setList(next);
      setFailed(false);
    }
  }, []);

  const reload = useCallback(async () => {
    if (planetId === null) {
      return;
    }

    try {
      apply(await production.recipes(planetId));
    } catch {
      if (active.current) {
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

  return { list, failed, reload };
}
