import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ProcessBuilding, ProcessBuildingSummary } from '../../api/processesApi';
import { useApi } from '../../api/useApi';

/**
 * How often the buildings are read again while the page is open, in milliseconds: a unit of
 * a process finishes on the server and the client only shows it.
 */
const refreshIntervalMs = 15000;

/**
 * How long after the moment of the next unit the building is read again, in milliseconds, so
 * the server has made the unit by then.
 */
const unitReadyDelayMs = 1000;

/**
 * The longest wait for the next unit, in milliseconds: the timers of the browser hold no
 * longer delay, and the periodic read covers the rest.
 */
const longestWaitMs = 2147483647;

/**
 * The state of the processes on a page: the buildings of the body, the building that is open
 * and the moment the answers came.
 */
export interface ProcessesState {
  buildings: ProcessBuildingSummary[];
  building: ProcessBuilding | null;
  receivedAt: number;
  loaded: boolean;
  failed: boolean;
  reload: () => Promise<void>;
  applyBuilding: (building: ProcessBuilding) => void;
  selectBuilding: (planetBuildingId: number | null) => void;
}

/**
 * Reads the buildings of a body that run a process, keeps them fresh and takes the answers
 * of the commands.
 */
export function useProcesses(planetId: number | null): ProcessesState {
  const { processes } = useApi();
  const [buildings, setBuildings] = useState<ProcessBuildingSummary[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [building, setBuilding] = useState<ProcessBuilding | null>(null);
  const [receivedAt, setReceivedAt] = useState(() => Date.now());
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);
  const [refreshCount, setRefreshCount] = useState(0);
  const active = useRef(true);

  const applyBuilding = useCallback((next: ProcessBuilding) => {
    if (active.current) {
      setBuilding(next);
      setReceivedAt(Date.now());
      setFailed(false);
    }
  }, []);

  const applySummary = useCallback((next: ProcessBuildingSummary[]) => {
    if (active.current) {
      setBuildings(next);
      setLoaded(true);
      setFailed(false);
      // The building that is open is read again with the list, so its queue moves on too.
      setRefreshCount((count) => count + 1);
    }
  }, []);

  const reload = useCallback(async () => {
    if (planetId === null) {
      return;
    }

    try {
      applySummary(await processes.buildings(planetId));
    } catch {
      if (active.current) {
        setFailed(true);
      }
    }
  }, [planetId, processes, applySummary]);

  // The building that is open is the first one, until the player picks another.
  const effectiveId = useMemo(() => {
    if (selectedId !== null && buildings.some((summary) => summary.planetBuildingId === selectedId)) {
      return selectedId;
    }

    return buildings[0]?.planetBuildingId ?? null;
  }, [selectedId, buildings]);

  useEffect(() => {
    active.current = true;
    void reload();
    const timer = window.setInterval(() => void reload(), refreshIntervalMs);

    return () => {
      active.current = false;
      window.clearInterval(timer);
    };
  }, [reload]);

  // The building that is open is read on its own, because it holds the recipes and the
  // queue. The answer arrives after the page is drawn, so it never blocks the first one.
  useEffect(() => {
    if (planetId === null || effectiveId === null) {
      return;
    }

    let cancelled = false;

    void processes.building(planetId, effectiveId).then((next) => {
      if (!cancelled && active.current) {
        applyBuilding(next);
      }
    }).catch(() => {
      if (!cancelled && active.current) {
        setFailed(true);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [planetId, effectiveId, processes, applyBuilding, refreshCount]);

  // The next unit of the queue is read when it is due, so the queue moves at its own moment and
  // not only with the periodic read.
  useEffect(() => {
    const nextUnitAt = building?.queue.find((order) => order.nextUnitAt !== null && !order.isBlocked)?.nextUnitAt;

    if (!building || !nextUnitAt) {
      return;
    }

    const waitMs = Date.parse(nextUnitAt) - Date.parse(building.gameNow) - (Date.now() - receivedAt) + unitReadyDelayMs;
    // A unit that is already late waits for the periodic read, so a slow server is not asked every second.
    const timer = window.setTimeout(() => void reload(), waitMs > 0 ? Math.min(waitMs, longestWaitMs) : refreshIntervalMs);

    return () => window.clearTimeout(timer);
  }, [building, receivedAt, reload]);

  const selectBuilding = useCallback((planetBuildingId: number | null) => {
    setSelectedId(planetBuildingId);
  }, []);

  return { buildings, building, receivedAt, loaded, failed, reload, applyBuilding, selectBuilding };
}
