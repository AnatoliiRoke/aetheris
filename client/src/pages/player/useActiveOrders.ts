import { useCallback, useEffect, useRef, useState } from 'react';
import type { ActiveOrder, ActiveOrders } from '../../api/activeOrders';
import type { ProcessActiveOrder } from '../../api/processesApi';
import { useApi } from '../../api/useApi';
import { useI18n } from '../../i18n/useI18n';
import { noticeFromError, type Notice } from './screenNotice';
import { useReloadOnVisible } from './tabVisibility';

/**
 * The prefixes of the error codes of the production that have their own text for the player.
 */
const productionReasonPrefixes = ['production_', 'planets_', 'requirements_'];

/**
 * The prefixes of the error codes of the processes that have their own text for the player.
 */
const processesReasonPrefixes = ['processes_', 'planets_'];

/**
 * The state of a queue of the orders of a body on a page: the last answer of the server and the
 * moment it came.
 */
export interface ActiveOrdersState<TOrder extends ActiveOrder = ActiveOrder> {
  orders: ActiveOrders<TOrder> | null;
  receivedAt: number;
  failed: boolean;
  reload: () => Promise<void>;
}

/**
 * Reads every order of every building of a body, reads them again on the events of the page
 * (the start, a command of the player, the end of a record of the queue, the tab becoming
 * visible again) and never on a timer.
 *
 * @param planetId The body, or `null` while it is not known.
 * @param read The read of the server for the kind of the orders; it must be the same function on every draw.
 */
function useOrdersState<TOrder extends ActiveOrder>(planetId: number | null, read: (planetId: number) => Promise<ActiveOrders<TOrder>>): ActiveOrdersState<TOrder> {
  const [orders, setOrders] = useState<ActiveOrders<TOrder> | null>(null);
  const [receivedAt, setReceivedAt] = useState(() => Date.now());
  const [failed, setFailed] = useState(false);
  const active = useRef(true);

  const apply = useCallback((next: ActiveOrders<TOrder>) => {
    if (active.current) {
      setOrders(next);
      setReceivedAt(Date.now());
      setFailed(false);
    }
  }, []);

  const reload = useCallback(async () => {
    if (planetId === null) {
      return;
    }

    try {
      apply(await read(planetId));
    } catch {
      if (active.current) {
        setFailed(true);
      }
    }
  }, [planetId, read, apply]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  useReloadOnVisible(reload);

  return { orders, receivedAt, failed, reload };
}

/**
 * The batches of all the factories of a body with the cancel of a batch.
 *
 * @param planetId The body, or `null` while it is not known.
 * @param onChanged What to tell the page after a command, so the other blocks of the production read again.
 */
export function useProductionOrders(planetId: number | null, onChanged: () => void) {
  const { t, language } = useI18n();
  const { production } = useApi();
  const read = useCallback((id: number) => production.orders(id), [production]);
  const state = useOrdersState(planetId, read);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  const cancel = async (order: ActiveOrder) => {
    if (planetId === null) {
      return;
    }

    setBusy(true);
    setNotice(null);

    try {
      await production.cancel(planetId, order.planetBuildingId, order.orderId);
      setNotice({ kind: 'success', text: t('production.cancelled') });
    } catch (error) {
      setNotice(noticeFromError(error, language, t('error.generic.text'), productionReasonPrefixes));
    } finally {
      await state.reload();
      onChanged();
      setBusy(false);
    }
  };

  return { ...state, busy, notice, cancel };
}

/**
 * The orders of all the buildings of a body that run a process with the cancel of an order.
 *
 * @param planetId The body, or `null` while it is not known.
 * @param onChanged What to tell the page after a command, so the other blocks of the processes read again.
 */
export function useProcessOrders(planetId: number | null, onChanged: () => void) {
  const { t, language } = useI18n();
  const { processes } = useApi();
  const read = useCallback((id: number) => processes.orders(id), [processes]);
  const state = useOrdersState<ProcessActiveOrder>(planetId, read);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  const cancel = async (order: ActiveOrder) => {
    if (planetId === null) {
      return;
    }

    setBusy(true);
    setNotice(null);

    try {
      await processes.cancel(planetId, order.planetBuildingId, order.orderId);
      setNotice({ kind: 'success', text: t('processes.cancelled') });
    } catch (error) {
      setNotice(noticeFromError(error, language, t('error.generic.text'), processesReasonPrefixes));
    } finally {
      await state.reload();
      onChanged();
      setBusy(false);
    }
  };

  return { ...state, busy, notice, cancel };
}
