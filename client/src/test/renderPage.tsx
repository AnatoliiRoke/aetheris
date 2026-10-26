import { render, type RenderResult } from '@testing-library/react';
import { vi } from 'vitest';
import { AppProviders, Pages } from '../App';
import type { AuthApi, Me } from '../api/authApi';
import type { Api } from '../api/context';
import type { ContentApi } from '../api/contentApi';
import type { GameApi } from '../api/gameApi';
import { mockContentApi } from '../api/mockContentApi';
import type { Planet, PlanetApi } from '../api/planetApi';

/**
 * An account API whose every method is a spy; a test overrides what it needs.
 */
export function fakeAuthApi(overrides: Partial<AuthApi> = {}): AuthApi {
  return {
    register: vi.fn().mockResolvedValue(undefined),
    resendConfirmation: vi.fn().mockResolvedValue(undefined),
    confirmEmail: vi.fn().mockResolvedValue(undefined),
    login: vi.fn().mockResolvedValue({ deletionCancelled: false }),
    logout: vi.fn().mockResolvedValue(undefined),
    forgotPassword: vi.fn().mockResolvedValue(undefined),
    resetPassword: vi.fn().mockResolvedValue(undefined),
    me: vi.fn().mockResolvedValue(null),
    providers: vi.fn().mockResolvedValue({ google: false }),
    googleSignInUrl: vi.fn((returnUrl: string) => `/api/v1/auth/google?returnUrl=${returnUrl}`),
    resendMyConfirmation: vi.fn().mockResolvedValue(undefined),
    changePassword: vi.fn().mockResolvedValue(undefined),
    changeNickname: vi.fn().mockResolvedValue({ nickname: 'Pilot', nextChangeAt: '2026-11-25T00:00:00Z' }),
    signOutEverywhere: vi.fn().mockResolvedValue(undefined),
    deleteAccount: vi.fn().mockResolvedValue({ deletionDueAt: '2026-11-02T00:00:00Z' }),
    ...overrides,
  };
}

/**
 * A world API whose every method is a spy; a test overrides what it needs.
 */
export function fakeGameApi(overrides: Partial<GameApi> = {}): GameApi {
  return {
    races: vi.fn().mockResolvedValue([
      { id: 1, code: 'terran', nameKey: 'races.terran.name' },
      { id: 2, code: 'kvarr', nameKey: 'races.kvarr.name' },
    ]),
    foundCivilization: vi.fn().mockResolvedValue({
      raceId: 1,
      raceCode: 'terran',
      planetId: 10,
      planetName: 'Nova',
      systemId: 3,
      sectorId: 1,
    }),
    ...overrides,
  };
}

/**
 * A home planet for the tests: the main building, a power plant to queue, a science centre
 * behind a requirement and a construction under way.
 */
export function samplePlanet(overrides: Partial<Planet> = {}): Planet {
  return {
    planetId: 10,
    gameNow: '2026-10-26T12:00:00Z',
    timeMultiplier: 1,
    resources: [
      { resourceTypeId: 1, resourceGroupId: 1, amount: 900, ratePerHour: 60 },
      { resourceTypeId: 3, resourceGroupId: 1, amount: 500, ratePerHour: 0 },
    ],
    storageGroups: [{ resourceGroupId: 1, capacity: 10000, stored: 1400, isFull: false, fullAt: null }],
    energy: { output: 10, consumption: 5, balance: 5, queuedBalance: 5 },
    queue: {
      slotCount: 2,
      items: [
        {
          id: 7,
          buildingTypeId: 5,
          targetLevel: 1,
          position: 1,
          durationSeconds: 60,
          startedAt: '2026-10-26T11:59:30Z',
          finishesAt: '2026-10-26T12:00:30Z',
          costs: [{ resourceTypeId: 1, amount: 80 }],
        },
      ],
    },
    buildings: [
      {
        buildingTypeId: 1,
        code: 'admin_center',
        nameKey: 'planets.building.admin_center.name',
        descriptionKey: 'planets.building.admin_center.description',
        level: 1,
        queuedLevel: 1,
        maxLevel: 20,
        effects: [{ effectType: 'level_cap', resourceTypeId: null, resourceGroupId: null, value: 1 }],
        next: {
          level: 2,
          durationSeconds: 192,
          costs: [{ resourceTypeId: 1, amount: 510 }],
          effects: [],
          energyBalanceAfter: 8,
          canEnqueue: true,
          reasonCodes: [],
          unmetRequirements: [],
          missingResources: [],
        },
      },
      {
        buildingTypeId: 2,
        code: 'science_center',
        nameKey: 'planets.building.science_center.name',
        descriptionKey: 'planets.building.science_center.description',
        level: 0,
        queuedLevel: 0,
        maxLevel: 20,
        effects: [],
        next: {
          level: 1,
          durationSeconds: 180,
          costs: [{ resourceTypeId: 1, amount: 400 }],
          effects: [],
          energyBalanceAfter: -3,
          canEnqueue: false,
          reasonCodes: ['planets_requirement_unmet', 'planets_energy_short'],
          unmetRequirements: [
            {
              requirementId: 1,
              requirementType: 'building_level',
              targetId: 1,
              targetLevel: 2,
              reasonCode: 'planets_requirement_building_level',
              targetNameKey: 'planets.building.admin_center.name',
            },
          ],
          missingResources: [],
        },
      },
      {
        buildingTypeId: 5,
        code: 'iron_mine',
        nameKey: 'planets.building.iron_mine.name',
        descriptionKey: 'planets.building.iron_mine.description',
        level: 0,
        queuedLevel: 1,
        maxLevel: 20,
        effects: [],
        next: {
          level: 2,
          durationSeconds: 90,
          costs: [{ resourceTypeId: 1, amount: 128 }],
          effects: [],
          energyBalanceAfter: 4,
          canEnqueue: false,
          reasonCodes: ['planets_level_capped'],
          unmetRequirements: [],
          missingResources: [],
        },
      },
    ],
    populationMax: 0,
    resourceTypes: [
      { id: 1, code: 'iron', nameKey: 'world.resource.iron', resourceGroupId: 1 },
      { id: 3, code: 'copper', nameKey: 'world.resource.copper', resourceGroupId: 1 },
    ],
    resourceGroups: [{ id: 1, code: 'extractable', nameKey: 'world.resource_group.extractable', resourceGroupId: null }],
    ...overrides,
  };
}

/**
 * A planet API whose every method is a spy; a test overrides what it needs.
 */
export function fakePlanetApi(overrides: Partial<PlanetApi> = {}): PlanetApi {
  return {
    planet: vi.fn().mockResolvedValue(samplePlanet()),
    enqueue: vi.fn().mockResolvedValue(samplePlanet()),
    cancel: vi.fn().mockResolvedValue(samplePlanet({ queue: { slotCount: 2, items: [] } })),
    ...overrides,
  };
}

/**
 * A signed-in account for the tests.
 */
export function signedInMe(overrides: Partial<Me> = {}): Me {
  return {
    userId: 'user-1',
    nickname: 'Pilot',
    email: 'pilot@example.test',
    emailConfirmed: true,
    nicknameChangedAt: null,
    civilization: { raceId: 1, raceCode: 'terran', planetId: 10, planetName: 'Nova', systemId: 3, sectorId: 1 },
    ...overrides,
  };
}

/**
 * Renders the site at an address with fake server interfaces.
 */
export function renderPage(
  path: string,
  options: { auth?: AuthApi; content?: ContentApi; game?: GameApi; planets?: PlanetApi } = {},
): RenderResult & { api: Api } {
  window.history.replaceState(null, '', path);

  const api: Api = {
    auth: options.auth ?? fakeAuthApi(),
    content: options.content ?? mockContentApi,
    game: options.game ?? fakeGameApi(),
    planets: options.planets ?? fakePlanetApi(),
  };
  const result = render(
    <AppProviders api={api}>
      <Pages />
    </AppProviders>,
  );

  return { ...result, api };
}
