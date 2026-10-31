import type { Api } from '../../api/context';
import type { CurrencyBalanceFacts, PopulationFacts } from '../../api/economyApi';
import type { Race } from '../../api/gameApi';
import { ApiError } from '../../api/http';
import type { Planet } from '../../api/planetApi';
import type { OwnedPlanet, WorldReference } from '../../api/worldApi';

/**
 * A body as the server last told it and the moment the answer came.
 */
export interface PlanetEntry {
  planet: Planet;
  receivedAt: number;
}

/**
 * Everything the frame of the screens shares: the bodies, the economy and the body the player
 * works with. One store holds it, so the header, the menu, the bottom bar and the pages read the
 * same answers and the server is asked once (task 00020, section 7.8).
 */
export interface ShellState {
  /** The body the player chose in the bottom bar, or `null` while the home body is the current one. */
  selectedPlanetId: number | null;
  planets: ReadonlyMap<number, PlanetEntry>;
  failedPlanetIds: readonly number[];
  balances: readonly CurrencyBalanceFacts[];
  economyLoaded: boolean;
  economyFailed: boolean;
  populations: ReadonlyMap<number, PopulationFacts | null>;
  bodies: readonly OwnedPlanet[];
  reference: WorldReference | null;
  bodiesLoaded: boolean;
  bodiesFailed: boolean;
  races: readonly Race[] | null;
}

/**
 * The reads and the writes of the shared state. A read that is already under way is not sent
 * again: every caller gets the answer of the one request.
 */
export interface ShellActions {
  /** Reads a body, unless it is known already; `force` reads it again. */
  loadPlanet: (planetId: number, force?: boolean) => Promise<void>;
  /** Takes the answer of a command on a body; the balances are read again, the command may have spent them. */
  applyPlanet: (planetId: number, planet: Planet) => void;
  loadEconomy: (force?: boolean) => Promise<void>;
  loadPopulation: (planetId: number, force?: boolean) => Promise<void>;
  loadBodies: (force?: boolean) => Promise<void>;
  loadRaces: () => Promise<void>;
  selectPlanet: (planetId: number) => void;
  /** Reads again what was read before: the one reload of the page when it comes back into view. */
  refreshAll: () => Promise<void>;
  /** Reads a body and the balances again after a command that spent or paid resources. */
  refreshAfterCommand: (planetId: number | null) => void;
}

/**
 * The shared state of the frame with its readers and its actions.
 */
export interface ShellStore {
  getState: () => ShellState;
  subscribe: (listener: () => void) => () => void;
  actions: ShellActions;
}

const initialState: ShellState = {
  selectedPlanetId: null,
  planets: new Map(),
  failedPlanetIds: [],
  balances: [],
  economyLoaded: false,
  economyFailed: false,
  populations: new Map(),
  bodies: [],
  reference: null,
  bodiesLoaded: false,
  bodiesFailed: false,
  races: null,
};

/**
 * Keeps the old value when the new one says the same, so a read that changed nothing wakes no
 * reader.
 */
function keepIfSame<T>(previous: T, next: T): T {
  return JSON.stringify(previous) === JSON.stringify(next) ? previous : next;
}

/**
 * Makes the shared state of the frame over the server interfaces.
 *
 * @param api The server interfaces.
 */
export function createShellStore(api: Api): ShellStore {
  let state: ShellState = initialState;
  const listeners = new Set<() => void>();
  const inFlight = new Map<string, Promise<void>>();

  const set = (patch: Partial<ShellState>) => {
    state = { ...state, ...patch };
    listeners.forEach((listener) => listener());
  };

  // The same read asked twice at once is one request.
  const once = (key: string, run: () => Promise<void>): Promise<void> => {
    const running = inFlight.get(key);

    if (running) {
      return running;
    }

    const started = run().finally(() => inFlight.delete(key));

    inFlight.set(key, started);

    return started;
  };

  const setPlanet = (planetId: number, planet: Planet) => {
    const planets = new Map(state.planets);

    planets.set(planetId, { planet, receivedAt: Date.now() });
    set({ planets, failedPlanetIds: state.failedPlanetIds.filter((id) => id !== planetId) });
  };

  const loadPlanet = (planetId: number, force = false): Promise<void> => {
    if (!force && state.planets.has(planetId)) {
      return Promise.resolve();
    }

    return once(`planet:${planetId}`, async () => {
      try {
        setPlanet(planetId, await api.planets.planet(planetId));
      } catch {
        if (!state.failedPlanetIds.includes(planetId)) {
          set({ failedPlanetIds: [...state.failedPlanetIds, planetId] });
        }
      }
    });
  };

  const loadEconomy = (force = false): Promise<void> => {
    if (!force && state.economyLoaded) {
      return Promise.resolve();
    }

    return once('economy', async () => {
      try {
        const answer = await api.economy.overview();

        set({ balances: keepIfSame(state.balances, answer.balances), economyLoaded: true, economyFailed: false });
      } catch {
        set({ economyFailed: true });
      }
    });
  };

  const loadPopulation = (planetId: number, force = false): Promise<void> => {
    if (!force && state.populations.has(planetId)) {
      return Promise.resolve();
    }

    return once(`population:${planetId}`, async () => {
      const store = (facts: PopulationFacts | null, failed: boolean) => {
        const populations = new Map(state.populations);

        populations.set(planetId, facts);
        set({ populations, economyFailed: failed });
      };

      try {
        store(keepIfSame(state.populations.get(planetId) ?? null, await api.economy.population(planetId)), state.economyFailed);
      } catch (error) {
        // The body has no population yet: the server answers 404 and the panel shows a
        // placeholder, not a made-up number (task 00010).
        store(null, error instanceof ApiError && error.status === 404 ? state.economyFailed : true);
      }
    });
  };

  const loadReference = (): Promise<void> => {
    if (state.reference) {
      return Promise.resolve();
    }

    return once('reference', async () => {
      try {
        set({ reference: await api.world.reference() });
      } catch {
        set({ bodiesFailed: true, bodiesLoaded: true });
      }
    });
  };

  const loadBodies = (force = false): Promise<void> => {
    if (!force && state.bodiesLoaded) {
      return loadReference();
    }

    const bodies = once('bodies', async () => {
      try {
        set({ bodies: keepIfSame(state.bodies, await api.world.planets()), bodiesLoaded: true, bodiesFailed: false });
      } catch {
        set({ bodiesFailed: true, bodiesLoaded: true });
      }
    });

    return Promise.all([bodies, loadReference()]).then(() => undefined);
  };

  const loadRaces = (): Promise<void> => {
    if (state.races) {
      return Promise.resolve();
    }

    return once('races', async () => {
      try {
        set({ races: await api.game.races() });
      } catch {
        // Without the list the glyph of the race is the neutral one.
      }
    });
  };

  const actions: ShellActions = {
    loadPlanet,
    applyPlanet: (planetId, planet) => {
      setPlanet(planetId, planet);

      if (state.economyLoaded) {
        void loadEconomy(true);
      }
    },
    loadEconomy,
    loadPopulation,
    loadBodies,
    loadRaces,
    selectPlanet: (planetId) => {
      if (state.selectedPlanetId !== planetId) {
        set({ selectedPlanetId: planetId });
      }
    },
    refreshAll: async () => {
      const reads: Promise<void>[] = [...state.planets.keys()].map((planetId) => loadPlanet(planetId, true));

      reads.push(...[...state.populations.keys()].map((planetId) => loadPopulation(planetId, true)));

      if (state.economyLoaded) {
        reads.push(loadEconomy(true));
      }

      if (state.bodiesLoaded) {
        reads.push(loadBodies(true));
      }

      await Promise.all(reads);
    },
    refreshAfterCommand: (planetId) => {
      if (planetId !== null && state.planets.has(planetId)) {
        void loadPlanet(planetId, true);
      }

      if (state.economyLoaded) {
        void loadEconomy(true);
      }
    },
  };

  return {
    getState: () => state,
    subscribe: (listener) => {
      listeners.add(listener);

      return () => listeners.delete(listener);
    },
    actions,
  };
}
