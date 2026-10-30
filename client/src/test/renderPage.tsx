import { render, type RenderResult } from '@testing-library/react';
import { vi } from 'vitest';
import { AppProviders, Pages } from '../App';
import type { AuthApi, Me } from '../api/authApi';
import type { Api } from '../api/context';
import type { ContentApi } from '../api/contentApi';
import type { GameApi, Race } from '../api/gameApi';
import { mockContentApi } from '../api/mockContentApi';
import type { Planet, PlanetApi } from '../api/planetApi';
import type { ProcessBuilding, ProcessBuildingSummary, ProcessesApi } from '../api/processesApi';
import type { ProductionApi, ProductionFactory, ProductionFactorySummary, ProductionStock } from '../api/productionApi';
import type { ResearchApi, ResearchTree } from '../api/researchApi';
import type { TextsApi } from '../api/textsApi';

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
 * A race of the tests with neutral keys, as the server sends it.
 */
export function sampleRace(id: number): Race {
  const key = (part: string) => `world.race.race_${id}.${part}`;

  return {
    id,
    nameKey: key('name'),
    descriptionKey: key('description'),
    moodKey: key('mood'),
    traitKeys: [key('trait_1'), key('trait_2')],
    imageKey: id <= 4 ? `art-${String(id + 6).padStart(2, '0')}` : null,
    colorKey: id <= 4 ? `race-${id}` : null,
  };
}

/**
 * The texts of the game data the tests show, as the server keeps them in the database.
 */
export const sampleTexts: Record<string, Record<string, string>> = {
  uk: {
    'world.race.race_1.name': 'Терани',
    'world.race.race_1.description': 'Надійні й дисципліновані.',
    'world.race.race_1.mood': 'військовий порядок',
    'world.race.race_1.trait_1': 'Оборона',
    'world.race.race_1.trait_2': 'Логістика',
    'world.race.race_2.name': 'Кварри',
    'world.race.race_2.description': 'Кристалічні корпуси й товста броня.',
    'world.race.race_3.name': 'Сколари',
    'world.race.race_4.name': 'Нексари',
    'world.resource.resource_1.name': 'Залізо',
    'world.resource.resource_3.name': 'Мідь',
    'world.resource_group.resource_group_1.name': 'Видобувні',
    'planets.building.building_1.name': 'Адміністративний центр',
    'planets.building.building_1.description': 'Головна будівля планети.',
    'planets.building.building_2.name': 'Науковий центр',
    'planets.building.building_5.name': 'Шахта заліза',
    'planets.building.building_20.name': 'Фабрика утилізації',
    'research.branch.economy.name': 'Економіка',
    'research.technology.tech_1.name': 'Глибинний видобуток',
    'research.technology.tech_1.description': 'Шахти видобувають більше металів.',
    'production.recipe.recipe_1.name': 'Будматеріали',
    'production.recipe.recipe_1.description': 'Плити й балки з сировини.',
    'production.recipe.recipe_2.name': 'Корпусний модуль',
    'production.recipe.recipe_2.description': 'Модуль корпусу корабля.',
    'production.part_type.part_3.name': 'Корпус «Оса» Мк. II',
    'processes.process_type.terraforming.name': 'Тераформінг',
    'processes.process_type.recycling.name': 'Розбирання',
    'processes.process_recipe.recipe_1.name': 'Тераформування планети',
    'processes.process_recipe.recipe_1.description': 'Пристрій на орбіті робить родовища багатими.',
    'processes.process_recipe.recipe_2.name': 'Розбирання деталі',
    'processes.process_recipe.recipe_2.description': 'Деталь розбирається на ресурси.',
  },
  en: {
    'world.race.race_1.name': 'Terrans',
    'world.race.race_2.name': 'Kvarr',
    'world.resource.resource_1.name': 'Iron',
    'planets.building.building_1.name': 'Administrative center',
    'research.branch.economy.name': 'Economy',
    'research.technology.tech_1.name': 'Deep extraction',
    'research.technology.tech_1.description': 'The mines extract more metals.',
    'production.recipe.recipe_1.name': 'Building materials',
    'production.recipe.recipe_1.description': 'The plates and beams from the raw materials.',
    'production.recipe.recipe_2.name': 'Hull module',
    'production.recipe.recipe_2.description': 'A module of the hull of a ship.',
    'planets.building.building_20.name': 'Recycling factory',
    'processes.process_type.terraforming.name': 'Terraforming',
    'processes.process_type.recycling.name': 'Recycling',
    'processes.process_recipe.recipe_1.name': 'Terraforming of the planet',
    'processes.process_recipe.recipe_1.description': 'The device on the orbit makes the deposits rich.',
    'processes.process_recipe.recipe_2.name': 'Recycling of a part',
    'processes.process_recipe.recipe_2.description': 'The part is taken apart into resources.',
  },
};

/**
 * A texts API that answers with the sample texts; a test overrides what it needs.
 */
export function fakeTextsApi(texts: Record<string, Record<string, string>> = sampleTexts): TextsApi {
  return {
    pack: vi.fn((language: string) => Promise.resolve({ language, version: 1, texts: { ...(texts[language] ?? {}) } })),
  };
}

/**
 * A world API whose every method is a spy; a test overrides what it needs.
 */
export function fakeGameApi(overrides: Partial<GameApi> = {}): GameApi {
  return {
    races: vi.fn().mockResolvedValue([sampleRace(1), sampleRace(2)]),
    publicRaces: vi.fn().mockResolvedValue([sampleRace(1), sampleRace(2), sampleRace(3), sampleRace(4)]),
    foundCivilization: vi.fn().mockResolvedValue({
      raceId: 1,
      raceNameKey: 'world.race.race_1.name',
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
        code: 'building_1',
        nameKey: 'planets.building.building_1.name',
        descriptionKey: 'planets.building.building_1.description',
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
        code: 'building_2',
        nameKey: 'planets.building.building_2.name',
        descriptionKey: 'planets.building.building_2.description',
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
              targetNameKey: 'planets.building.building_1.name',
            },
          ],
          missingResources: [],
        },
      },
      {
        buildingTypeId: 5,
        code: 'building_5',
        nameKey: 'planets.building.building_5.name',
        descriptionKey: 'planets.building.building_5.description',
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
      { id: 1, code: 'resource_1', nameKey: 'world.resource.resource_1.name', resourceGroupId: 1 },
      { id: 3, code: 'resource_3', nameKey: 'world.resource.resource_3.name', resourceGroupId: 1 },
    ],
    resourceGroups: [{ id: 1, code: 'resource_group_1', nameKey: 'world.resource_group.resource_group_1.name', resourceGroupId: null }],
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
 * A sample tree of the technologies for the tests (task 00012).
 */
export function sampleResearchTree(overrides: Partial<ResearchTree> = {}): ResearchTree {
  return {
    branches: [{ branch: 'economy', nameKey: 'research.branch.economy.name' }],
    technologies: [
      {
        id: 1,
        code: 'tech_1',
        nameKey: 'research.technology.tech_1.name',
        descriptionKey: 'research.technology.tech_1.description',
        branch: 'economy',
        sortOrder: 1,
        maxLevel: 10,
        learnedLevel: 0,
        status: 'available',
        researchCenterLevel: 1,
        availableCenterLevel: 2,
        reasonCode: null,
        levels: [
          {
            level: 1,
            durationSeconds: 300,
            costs: [{ resourceTypeId: 1, amount: 200 }],
            effects: [{ effectType: 'metal_production', unlockKind: null, unlockTargetId: null, unlockNameKey: null, value: 1.05 }],
            unmetRequirementIds: [],
          },
        ],
      },
    ],
    queue: [],
    queueSlots: 2,
    queueUsedSlots: 0,
    selectedPlanetId: 10,
    availableCenterLevel: 2,
    planets: [
      { planetId: 10, name: 'Nova', researchCenterLevel: 2, isAvailable: true, reasonCode: null },
      { planetId: 11, name: 'Vega', researchCenterLevel: 0, isAvailable: false, reasonCode: 'research_center_level_unmet' },
    ],
    gameNow: '2026-10-26T12:00:00Z',
    timeMultiplier: 1,
    ...overrides,
  };
}

/**
 * A research API whose every method is a spy; a test overrides what it needs.
 */
export function fakeResearchApi(overrides: Partial<ResearchApi> = {}): ResearchApi {
  return {
    tree: vi.fn().mockResolvedValue(sampleResearchTree()),
    enqueue: vi.fn().mockResolvedValue(sampleResearchTree()),
    cancel: vi.fn().mockResolvedValue(sampleResearchTree()),
    ...overrides,
  };
}

/**
 * A production API whose every method is a spy; a test overrides what it needs.
 */
export function fakeProductionApi(overrides: Partial<ProductionApi> = {}): ProductionApi {
  return {
    factories: vi.fn().mockResolvedValue(sampleProductionFactories()),
    factory: vi.fn().mockResolvedValue(sampleProductionFactory()),
    stock: vi.fn().mockResolvedValue(sampleProductionStock()),
    enqueue: vi.fn().mockResolvedValue(sampleProductionFactory()),
    cancel: vi.fn().mockResolvedValue(sampleProductionFactory()),
    ...overrides,
  };
}

/**
 * A factory of a body for the tests (task 00013).
 */
export function sampleProductionFactory(overrides: Partial<ProductionFactory> = {}): ProductionFactory {
  return {
    planetId: 10,
    planetBuildingId: 500,
    factoryLevel: 1,
    recipes: [
      {
        recipeId: 1,
        code: 'recipe_1',
        nameKey: 'production.recipe.recipe_1.name',
        descriptionKey: 'production.recipe.recipe_1.description',
        buildingTypeId: 13,
        partTypeId: null,
        resourceTypeId: 13,
        outputQuantity: 1,
        durationSeconds: 60,
        isAvailable: true,
        reasons: [],
        imageKey: null,
        unitSeconds: 60,
        ingredients: [{ resourceTypeId: 1, partTypeId: null, amount: 4, nameKey: 'world.resource.resource_1.name' }],
      },
      {
        recipeId: 2,
        code: 'recipe_2',
        nameKey: 'production.recipe.recipe_2.name',
        descriptionKey: 'production.recipe.recipe_2.description',
        buildingTypeId: 13,
        partTypeId: 3,
        resourceTypeId: null,
        outputQuantity: 1,
        durationSeconds: 120,
        isAvailable: false,
        reasons: [{ code: 'production_requirement_technology', targetId: 5, targetLevel: 1, targetNameKey: 'research.technology.tech_1.name' }],
        imageKey: null,
        unitSeconds: 120,
        ingredients: [{ resourceTypeId: 3, partTypeId: null, amount: 2, nameKey: 'world.resource.resource_3.name' }],
      },
    ],
    queue: [
      {
        orderId: 700,
        recipeId: 1,
        quantity: 3,
        producedQuantity: 1,
        position: 1,
        startsAt: '2026-10-26T11:59:00Z',
        nextUnitAt: '2026-10-26T12:00:00Z',
        finishesAt: '2026-10-26T12:02:00Z',
        isBlocked: false,
      },
    ],
    storage: { capacity: 100, stored: 5, fullAt: null },
    gameNow: '2026-10-26T12:00:00Z',
    ...overrides,
  };
}

/**
 * The list of the factories of a body for the tests.
 */
export function sampleProductionFactories(): ProductionFactorySummary[] {
  return [{ planetId: 10, planetBuildingId: 500, buildingTypeId: 13, level: 1, nameKey: 'planets.building.building_13.name' }];
}

/**
 * The stock of the parts of a body for the tests.
 */
export function sampleProductionStock(overrides: Partial<ProductionStock> = {}): ProductionStock {
  return {
    planetId: 10,
    stacks: [{ partTypeId: 3, markLevel: 2, hasRaceBonus: true, conditionPercent: 100, quantity: 4, nameKey: 'production.part_type.part_3.name', imageKey: null }],
    storage: { capacity: 100, stored: 4, fullAt: null },
    gameNow: '2026-10-26T12:00:00Z',
    ...overrides,
  };
}

/**
 * A processes API whose every method is a spy; a test overrides what it needs.
 */
export function fakeProcessesApi(overrides: Partial<ProcessesApi> = {}): ProcessesApi {
  return {
    buildings: vi.fn().mockResolvedValue(sampleProcessesBuildings()),
    building: vi.fn().mockResolvedValue(sampleProcessesBuilding()),
    enqueue: vi.fn().mockResolvedValue(sampleProcessesBuilding()),
    cancel: vi.fn().mockResolvedValue(sampleProcessesBuilding()),
    ...overrides,
  };
}

/**
 * A building of a body that runs a process, with its recipes and its queue (task 00014).
 */
export function sampleProcessesBuilding(overrides: Partial<ProcessBuilding> = {}): ProcessBuilding {
  return {
    planetId: 10,
    planetBuildingId: 600,
    buildingTypeId: 20,
    buildingLevel: 1,
    recipes: [
      {
        recipeId: 1,
        code: 'recipe_1',
        nameKey: 'processes.process_recipe.recipe_1.name',
        descriptionKey: 'processes.process_recipe.recipe_1.description',
        processTypeNameKey: 'processes.process_type.terraforming.name',
        buildingTypeId: 20,
        sourceGroupId: null,
        targetGroupId: null,
        durationSeconds: 3600,
        needsShip: true,
        partSubject: 'none',
        isAvailable: true,
        reasons: [],
        imageKey: null,
        ingredients: [{ resourceTypeId: 1, partTypeId: null, amount: 4 }],
      },
      {
        recipeId: 2,
        code: 'recipe_2',
        nameKey: 'processes.process_recipe.recipe_2.name',
        descriptionKey: 'processes.process_recipe.recipe_2.description',
        processTypeNameKey: 'processes.process_type.recycling.name',
        buildingTypeId: 20,
        sourceGroupId: null,
        targetGroupId: null,
        durationSeconds: 900,
        needsShip: false,
        partSubject: 'any',
        isAvailable: false,
        reasons: [{ code: 'processes_requirement_building_level', targetId: 1, targetLevel: 2, targetNameKey: 'planets.building.building_1.name' }],
        imageKey: null,
        ingredients: [{ resourceTypeId: null, partTypeId: 3, amount: 1 }],
      },
    ],
    queue: [
      {
        orderId: 800,
        recipeId: 1,
        quantity: 2,
        producedQuantity: 1,
        position: 1,
        startsAt: '2026-10-26T11:59:00Z',
        nextUnitAt: '2026-10-26T12:00:00Z',
        finishesAt: '2026-10-26T12:01:00Z',
        isBlocked: false,
      },
    ],
    gameNow: '2026-10-26T12:00:00Z',
    fullConditionPercent: 100,
    ...overrides,
  };
}

/**
 * The list of the buildings of a body that run a process for the tests.
 */
export function sampleProcessesBuildings(): ProcessBuildingSummary[] {
  return [{ planetId: 10, planetBuildingId: 600, buildingTypeId: 20, level: 1, nameKey: 'planets.building.building_20.name' }];
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
    civilization: { raceId: 1, raceNameKey: 'world.race.race_1.name', planetId: 10, planetName: 'Nova', systemId: 3, sectorId: 1 },
    ...overrides,
  };
}

/**
 * Renders the site at an address with fake server interfaces.
 */
export function renderPage(
  path: string,
  options: { auth?: AuthApi; content?: ContentApi; game?: GameApi; planets?: PlanetApi; processes?: ProcessesApi; production?: ProductionApi; research?: ResearchApi; texts?: TextsApi } = {},
): RenderResult & { api: Api } {
  window.history.replaceState(null, '', path);

  const api: Api = {
    auth: options.auth ?? fakeAuthApi(),
    content: options.content ?? mockContentApi,
    game: options.game ?? fakeGameApi(),
    planets: options.planets ?? fakePlanetApi(),
    processes: options.processes ?? fakeProcessesApi(),
    production: options.production ?? fakeProductionApi(),
    research: options.research ?? fakeResearchApi(),
    texts: options.texts ?? fakeTextsApi(),
  };
  const result = render(
    <AppProviders api={api}>
      <Pages />
    </AppProviders>,
  );

  return { ...result, api };
}
