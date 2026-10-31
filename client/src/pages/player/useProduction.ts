import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ProductionFactory, ProductionFactorySummary, ProductionStock } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { useReloadOnVisible } from './tabVisibility';

/**
 * How long after the moment of the next unit the factory is read again, in milliseconds, so
 * the server has made the unit by then.
 */
const unitReadyDelayMs = 1000;

/**
 * The longest wait for the next unit, in milliseconds: the timers of the browser hold no
 * longer delay, so a longer wait is cut and the read that follows sets the next one.
 */
const longestWaitMs = 2147483647;

/**
 * The state of the production on a page: the factories of the body, the factory that is
 * open, the stock of the parts and the moment the answers came.
 */
export interface ProductionState {
  factories: ProductionFactorySummary[];
  factory: ProductionFactory | null;
  stock: ProductionStock | null;
  receivedAt: number;
  loaded: boolean;
  failed: boolean;
  reload: () => Promise<void>;
  applyFactory: (factory: ProductionFactory) => void;
  selectFactory: (planetBuildingId: number | null) => void;
}

/**
 * Reads the factories of a body and the stock of its parts, reads them again on the events of the page and takes the
 * answers of the commands.
 */
export function useProduction(planetId: number | null): ProductionState {
  const { production } = useApi();
  const [factories, setFactories] = useState<ProductionFactorySummary[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [factory, setFactory] = useState<ProductionFactory | null>(null);
  const [stock, setStock] = useState<ProductionStock | null>(null);
  const [receivedAt, setReceivedAt] = useState(() => Date.now());
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);
  const [refreshCount, setRefreshCount] = useState(0);
  const active = useRef(true);

  const applyFactory = useCallback((next: ProductionFactory) => {
    if (active.current) {
      setFactory(next);
      setReceivedAt(Date.now());
      setFailed(false);
    }
  }, []);

  const applySummary = useCallback((next: ProductionFactorySummary[], nextStock: ProductionStock) => {
    if (active.current) {
      setFactories(next);
      setStock(nextStock);
      setLoaded(true);
      setFailed(false);
      // The factory that is open is read again with the list, so its queue moves on too.
      setRefreshCount((count) => count + 1);
    }
  }, []);

  const reload = useCallback(async () => {
    if (planetId === null) {
      return;
    }

    try {
      const summaries = await production.factories(planetId);
      const nextStock = await production.stock(planetId);

      applySummary(summaries, nextStock);
    } catch {
      if (active.current) {
        setFailed(true);
      }
    }
  }, [planetId, production, applySummary]);

  // The factory that is open is the first one, until the player picks another.
  const effectiveId = useMemo(() => {
    if (selectedId !== null && factories.some((summary) => summary.planetBuildingId === selectedId)) {
      return selectedId;
    }

    return factories[0]?.planetBuildingId ?? null;
  }, [selectedId, factories]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  useReloadOnVisible(reload);

  // The factory that is open is read on its own, because it holds the recipes and the
  // queue. The answer arrives after the page is drawn, so it never blocks the first one.
  useEffect(() => {
    if (planetId === null || effectiveId === null) {
      return;
    }

    let cancelled = false;

    void production.factory(planetId, effectiveId).then((next) => {
      if (!cancelled && active.current) {
        applyFactory(next);
      }
    }).catch(() => {
      if (!cancelled && active.current) {
        setFailed(true);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [planetId, effectiveId, production, applyFactory, refreshCount]);

  // The next unit of the queue is read when it is due, so the queue moves at its own moment, not on
  // a periodic read.
  useEffect(() => {
    const nextUnitAt = factory?.queue.find((order) => order.nextUnitAt !== null && !order.isBlocked)?.nextUnitAt;

    if (!factory || !nextUnitAt) {
      return;
    }

    const waitMs = Date.parse(nextUnitAt) - Date.parse(factory.gameNow) - (Date.now() - receivedAt) + unitReadyDelayMs;
    // A unit that is already late waits for the next event (a command, a finished queue record or
    // the tab becoming visible), so a slow server is not asked every second.
    if (waitMs <= 0) {
      return;
    }

    const timer = window.setTimeout(() => void reload(), Math.min(waitMs, longestWaitMs));

    return () => window.clearTimeout(timer);
  }, [factory, receivedAt, reload]);

  const selectFactory = useCallback((planetBuildingId: number | null) => {
    setSelectedId(planetBuildingId);
  }, []);

  return { factories, factory, stock, receivedAt, loaded, failed, reload, applyFactory, selectFactory };
}
