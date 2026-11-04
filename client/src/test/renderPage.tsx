import { render, type RenderResult } from '@testing-library/react';
import type { ReactNode } from 'react';
import { vi } from 'vitest';
import { AppProviders, Pages } from '../App';
import type { AuthApi, Me } from '../api/authApi';
import type { Api } from '../api/context';
import type { ContentApi } from '../api/contentApi';
import type { CurrencyBalanceFacts, EconomyApi, EconomyOverview, PopulationFacts } from '../api/economyApi';
import type { GameApi, Race } from '../api/gameApi';
import type { MarketApi, MarketLot, MarketOverview, MyLot } from '../api/marketApi';
import { mockContentApi } from '../api/mockContentApi';
import type { Planet, PlanetApi } from '../api/planetApi';
import type { ActiveOrders } from '../api/activeOrders';
import type { ProcessActiveOrder, ProcessBuilding, ProcessBuildingSummary, ProcessesApi, TerraformingState, TerraformingTarget } from '../api/processesApi';
import type { MapApi, MapGalaxy, MapOwner, MapResourceShare, MapSectorView, MapSystemView } from '../api/mapApi';
import type { Characteristic, PartCharacteristics, ProductionApi, ProductionFactory, ProductionFactorySummary, ProductionPartGroup, ProductionRecipe, ProductionRecipeList, ProductionStock } from '../api/productionApi';
import type { ResearchApi, ResearchTree } from '../api/researchApi';
import type { TextsApi } from '../api/textsApi';
import type { OwnedPlanet, Reference, WorldApi, WorldReference } from '../api/worldApi';

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
    'world.resource.resource_18.name': 'Кредити',
    'world.resource_group.resource_group_1.name': 'Видобувні',
    'world.object_type.object_type_5.name': 'Вулканічна',
    'production.part_group.part_group_2.name': 'Реактори',
    'production.part_group.part_group_10.name': 'Інші корабельні',
    'planets.building.building_1.name': 'Адміністративний центр',
    'planets.building.building_1.description': 'Головна будівля планети.',
    'planets.building.building_2.name': 'Науковий центр',
    'planets.building.building_5.name': 'Шахта заліза',
    'planets.building.building_13.name': 'Завод',
    'planets.building.building_20.name': 'Фабрика утилізації',
    'planets.building_category.group_1.name': 'Видобуток',
    'planets.building_category.group_5.name': 'Населення',
    'planets.building_category.group_8.name': 'Дослідження',
    'planets.building_category.group_9.name': 'Торгівля',
    'research.branch.economy.name': 'Економіка',
    'research.technology.tech_1.name': 'Глибинний видобуток',
    'research.technology.tech_1.description': 'Шахти видобувають більше металів.',
    'production.recipe.recipe_1.name': 'Будматеріали',
    'production.recipe.recipe_1.description': 'Плити й балки з сировини.',
    'production.recipe.recipe_2.name': 'Корпусний модуль',
    'production.recipe.recipe_2.description': 'Модуль корпусу корабля.',
    'production.recipe.recipe_3.name': 'Реактор малий',
    'production.recipe.recipe_3.description': 'Малий реактор для корабля.',
    'production.part_group.part_group_3.name': 'Енергетична зброя',
    'production.part_group.part_group_4.name': 'Кінетична зброя',
    'production.part_group.part_group_7.name': 'Боєприпаси',
    'production.recipe.recipe_7.name': 'Снаряди',
    'production.part_group.part_group_12.name': 'Легкі',
    'production.part_group.part_group_19.name': 'Модулі кораблів',
    'production.part_group.part_group_20.name': 'Корпуси кораблів',
    'production.part_group.part_group_22.name': 'Зброя',
    'production.recipe.recipe_4.name': 'Легкий лазер',
    'production.recipe.recipe_5.name': 'РПУ-X1',
    'production.recipe.recipe_6.name': 'Корпус «Шершень»',
    'planets.building.building_14.name': 'Верф',
    'production.part_type.part_3.name': 'Корпус «Оса» Мк. II',
    'production.part_type.part_34.name': 'Пристрій тераформінгу Mk I',
    'production.characteristic.characteristic_1.name': 'Тяга',
    'production.characteristic.characteristic_1.description': 'Сила тяги рушія.',
    'production.characteristic.characteristic_1.unit': 'кН',
    'production.characteristic.characteristic_2.name': 'Витрата палива',
    'production.characteristic.characteristic_2.description': 'Скільки палива рушій витрачає за одиницю часу.',
    'production.characteristic.characteristic_2.unit': 'т/год',
    'processes.process_type.ship_repair.name': 'Ремонт корабля',
    'processes.process_type.recycling.name': 'Розбирання',
    'processes.process_recipe.recipe_1.name': 'Ремонт корабля на орбіті',
    'processes.process_recipe.recipe_1.description': 'Майстерня лагодить корпус і всі модулі корабля.',
    'processes.process_recipe.recipe_2.name': 'Розбирання деталі',
    'processes.process_recipe.recipe_2.description': 'Деталь розбирається на ресурси.',
  },
  en: {
    'world.race.race_1.name': 'Terrans',
    'world.race.race_2.name': 'Kvarr',
    'world.resource.resource_1.name': 'Iron',
    'world.resource.resource_18.name': 'Credits',
    'world.object_type.object_type_5.name': 'Volcanic',
    'production.part_group.part_group_2.name': 'Reactors',
    'production.part_group.part_group_10.name': 'Other ship modules',
    'production.characteristic.characteristic_1.name': 'Thrust',
    'production.characteristic.characteristic_1.unit': 'kN',
    'production.characteristic.characteristic_2.name': 'Fuel use',
    'production.characteristic.characteristic_2.unit': 't/h',
    'planets.building.building_1.name': 'Administrative center',
    'research.branch.economy.name': 'Economy',
    'research.technology.tech_1.name': 'Deep extraction',
    'research.technology.tech_1.description': 'The mines extract more metals.',
    'production.recipe.recipe_1.name': 'Building materials',
    'production.recipe.recipe_1.description': 'The plates and beams from the raw materials.',
    'production.recipe.recipe_2.name': 'Hull module',
    'production.recipe.recipe_2.description': 'A module of the hull of a ship.',
    'planets.building.building_13.name': 'Factory',
    'planets.building.building_20.name': 'Recycling factory',
    'planets.building_category.group_1.name': 'Extraction',
    'planets.building_category.group_5.name': 'Population',
    'planets.building_category.group_8.name': 'Research',
    'planets.building_category.group_9.name': 'Trade',
    'processes.process_type.ship_repair.name': 'Ship repair',
    'processes.process_type.recycling.name': 'Recycling',
    'processes.process_recipe.recipe_1.name': 'Repair of a ship on the orbit',
    'processes.process_recipe.recipe_1.description': 'The workshop repairs the hull and every module of a ship.',
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
      { resourceTypeId: 1, resourceGroupId: 1, amount: 900, ratePerHour: 60, capacity: 10000, isFull: false, fullAt: null },
      { resourceTypeId: 3, resourceGroupId: 1, amount: 500, ratePerHour: 0, capacity: 10000, isFull: false, fullAt: null },
    ],
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
        categoryId: 15,
        next: {
          level: 2,
          durationSeconds: 192,
          costs: [{ resourceTypeId: 1, amount: 510 }],
          effects: [],
          energyBalanceAfter: 8,
          energyOutputAfter: 40,
          energyConsumptionAfter: 32,
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
        categoryId: 11,
        next: {
          level: 1,
          durationSeconds: 180,
          costs: [{ resourceTypeId: 1, amount: 400 }],
          effects: [],
          energyBalanceAfter: -3,
          energyOutputAfter: 30,
          energyConsumptionAfter: 33,
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
              isClosed: false,
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
        categoryId: 12,
        next: {
          level: 2,
          durationSeconds: 90,
          costs: [{ resourceTypeId: 1, amount: 128 }],
          effects: [],
          energyBalanceAfter: 4,
          energyOutputAfter: 34,
          energyConsumptionAfter: 30,
          canEnqueue: false,
          reasonCodes: ['planets_level_capped'],
          unmetRequirements: [],
          missingResources: [],
        },
      },
    ],
    // The categories come in an order unlike their `sortOrder`, and one of them has no building.
    buildingCategories: [
      { id: 11, code: 'group_8', nameKey: 'planets.building_category.group_8.name', sortOrder: 8, imageKey: null },
      { id: 13, code: 'group_9', nameKey: 'planets.building_category.group_9.name', sortOrder: 9, imageKey: null },
      { id: 15, code: 'group_5', nameKey: 'planets.building_category.group_5.name', sortOrder: 5, imageKey: null },
      { id: 12, code: 'group_1', nameKey: 'planets.building_category.group_1.name', sortOrder: 1, imageKey: null },
    ],
    populationMax: 0,
    name: 'Nova',
    typeId: 5,
    coordinates: { sectorId: 1, systemId: 3, orbit: 2 },
    population: 310,
    populationGrowthPerHour: 12,
    timeToMaximum: '1.02:03:04',
    depositRichness: [{ resourceTypeId: 1, coefficient: 1.5 }],
    resourceTypes: [
      { id: 1, code: 'resource_1', nameKey: 'world.resource.resource_1.name', resourceGroupId: 1 },
      { id: 3, code: 'resource_3', nameKey: 'world.resource.resource_3.name', resourceGroupId: 1 },
      { id: 18, code: 'resource_18', nameKey: 'world.resource.resource_18.name', resourceGroupId: null },
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
            missingResources: [],
          },
        ],
        currentEffects: [],
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
 * The population of a body for the tests (task 00015): it grows to the largest population
 * of the body and reaches it in three minutes and twenty seconds.
 */
export function samplePopulation(overrides: Partial<PopulationFacts> = {}): PopulationFacts {
  return {
    planetId: 10,
    amount: 750,
    maximum: 900,
    growthPerHour: 45,
    overLimit: false,
    timeToMaximum: '0:03:20',
    ...overrides,
  };
}

/**
 * The balance of the credits of a civilization for the tests (task 00015).
 */
export function sampleBalance(overrides: Partial<CurrencyBalanceFacts> = {}): CurrencyBalanceFacts {
  return {
    playerId: 'user-1',
    resourceTypeId: 18,
    amount: 234,
    incomePerHour: 25,
    ...overrides,
  };
}

/**
 * The economy of a player for the tests (task 00015): the population of one body and the
 * balance of the credits.
 */
export function sampleEconomyOverview(overrides: Partial<EconomyOverview> = {}): EconomyOverview {
  return {
    populations: [samplePopulation()],
    balances: [sampleBalance()],
    gameNow: '2026-10-26T12:00:00Z',
    ...overrides,
  };
}

/**
 * An economy API whose every method is a spy; a test overrides what it needs.
 */
export function fakeEconomyApi(overrides: Partial<EconomyApi> = {}): EconomyApi {
  return {
    overview: vi.fn().mockResolvedValue(sampleEconomyOverview()),
    population: vi.fn().mockResolvedValue(samplePopulation()),
    ...overrides,
  };
}

/**
 * The overview of the market for the tests (task 00026): the clan level closed, the sector and the
 * galaxy levels open, and one body with a trade centre and a portal.
 */
export function sampleMarketOverview(overrides: Partial<MarketOverview> = {}): MarketOverview {
  return {
    levels: [
      { id: 1, visibility: 'clan', commissionPercent: 1, isActive: false, sortOrder: 1 },
      { id: 2, visibility: 'sector', commissionPercent: 5, isActive: true, sortOrder: 2 },
      { id: 3, visibility: 'galactic', commissionPercent: 10, isActive: true, sortOrder: 3 },
    ],
    creditsResourceTypeId: 18,
    tradeResourceTypeId: 19,
    energyResourceTypeId: 3,
    lotLimit: { used: 1, limit: 3 },
    planets: [{ planetId: 10, hasTradeCentre: true, hasPortal: true }],
    gameNow: '2026-10-26T12:00:00Z',
    timeMultiplier: 1,
    ...overrides,
  };
}

/**
 * A lot of another player for the tests (task 00026).
 */
export function sampleMarketLot(overrides: Partial<MarketLot> = {}): MarketLot {
  return {
    id: 101,
    seller: { playerId: 'user-2', nickname: 'Vektor_Prime', raceNameKey: 'world.race.race_1.name', raceColorKey: 'race-1' },
    sellerPlanetId: 20,
    marketLevelId: 2,
    resourceTypeId: 1,
    partTypeId: null,
    quantity: 1200,
    pricePerUnit: 4,
    totalPrice: 4800,
    createdAt: '2026-10-26T10:00:00Z',
    partConditionPercent: null,
    deliverySeconds: 720,
    energyRodAmount: 6,
    unavailableReason: null,
    ...overrides,
  };
}

/**
 * A lot of the player for the tests (task 00026).
 */
export function sampleMyLot(overrides: Partial<MyLot> = {}): MyLot {
  return {
    id: 201,
    sellerPlanetId: 10,
    marketLevelId: 2,
    resourceTypeId: 1,
    partTypeId: null,
    quantity: 600,
    pricePerUnit: 5,
    totalPrice: 3000,
    state: 'open',
    createdAt: '2026-10-26T10:00:00Z',
    partConditionPercent: null,
    ...overrides,
  };
}

/**
 * A market API whose every method is a spy; a test overrides what it needs.
 */
export function fakeMarketApi(overrides: Partial<MarketApi> = {}): MarketApi {
  return {
    overview: vi.fn().mockResolvedValue(sampleMarketOverview()),
    lots: vi.fn().mockResolvedValue({ items: [sampleMarketLot()], energyRodBalance: 8, destinationHasPortal: true, total: 1, offset: 0, limit: 40 }),
    lotGroups: vi.fn().mockResolvedValue([{ resourceTypeId: 1, partTypeId: null, lotCount: 3, minPricePerUnit: 4, totalQuantity: 1500 }]),
    myLots: vi.fn().mockResolvedValue([sampleMyLot()]),
    deliveries: vi.fn().mockResolvedValue({ incoming: [], outgoing: [] }),
    deals: vi.fn().mockResolvedValue({ items: [], total: 0, offset: 0, limit: 40 }),
    rate: vi.fn().mockResolvedValue({ rate: 100, commissionPercent: 5, creditsResourceTypeId: 18, tradeResourceTypeId: 19 }),
    rateHistory: vi.fn().mockResolvedValue({ points: [], dayChangePercent: null }),
    quote: vi.fn().mockResolvedValue({ sum: 4800, commission: 240, energyResourceTypeId: 3, energyRodAmount: 6, deliverySeconds: 720 }),
    priceLimit: vi.fn().mockResolvedValue({ min: 80, max: 120 }),
    listLot: vi.fn().mockResolvedValue([sampleMyLot()]),
    cancelLot: vi.fn().mockResolvedValue(sampleMyLot({ state: 'cancelled' })),
    buy: vi.fn().mockResolvedValue({ id: 1, lotId: 101, direction: 'bought' }),
    exchange: vi.fn().mockResolvedValue({ id: 1, fromResourceTypeId: 18, toResourceTypeId: 19, fromAmount: 1000, toAmount: 9, rate: 100, commissionPercent: 5, createdAt: '2026-10-26T12:00:00Z' }),
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
    recipes: vi.fn().mockResolvedValue(sampleProductionRecipes()),
    orders: vi.fn().mockResolvedValue(sampleProductionOrders()),
    characteristics: vi.fn().mockResolvedValue(sampleCharacteristics()),
    characteristicParts: vi.fn().mockResolvedValue(samplePartCharacteristics()),
    enqueue: vi.fn().mockResolvedValue(sampleProductionFactory()),
    cancel: vi.fn().mockResolvedValue(sampleProductionFactory()),
    useCapsule: vi.fn().mockResolvedValue({ planetId: 1, partTypeId: 9, inhabitants: 100, population: 200 }),
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
        partGroupId: null,
        partGroupNameKey: null,
        partGroupCode: null,
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
        partGroupId: 10,
        partGroupNameKey: 'production.part_group.part_group_10.name',
        partGroupCode: 'part_group_10',
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
    storage: { capacity: 100, kinds: [{ partTypeId: 3, quantity: 5, capacity: 100, isFull: false }] },
    gameNow: '2026-10-26T12:00:00Z',
    ...overrides,
  };
}

/**
 * Every recipe of every factory of a body for the tests (task 00032): the two recipes of the
 * factory of `sampleProductionFactory` and a third one whose factory the body has not built.
 */
export function sampleProductionRecipes(overrides: Partial<ProductionRecipeList> = {}): ProductionRecipeList {
  const factory = sampleProductionFactory();

  return {
    planetId: 10,
    recipes: [
      ...factory.recipes,
      {
        recipeId: 3,
        code: 'recipe_3',
        nameKey: 'production.recipe.recipe_3.name',
        descriptionKey: 'production.recipe.recipe_3.description',
        buildingTypeId: 14,
        partTypeId: 4,
        resourceTypeId: null,
        outputQuantity: 1,
        durationSeconds: 300,
        isAvailable: false,
        reasons: [{ code: 'production_factory_not_built', targetId: 14, targetLevel: 0, targetNameKey: 'planets.building.building_14.name' }],
        imageKey: null,
        unitSeconds: 300,
        ingredients: [{ resourceTypeId: 2, partTypeId: null, amount: 7, nameKey: 'world.resource.resource_2.name' }],
        partGroupId: 2,
        partGroupNameKey: 'production.part_group.part_group_2.name',
        partGroupCode: 'part_group_2',
      },
    ],
    factories: sampleProductionFactories(),
    storage: factory.storage,
    gameNow: factory.gameNow,
    groups: sampleProductionGroups(),
    ...overrides,
  };
}

/**
 * The tree of the groups of the parts for the tests (task 00043): the sections of the ship
 * modules and the ship hulls, and the weapons inside the ship modules with the energy weapons,
 * the kinetic weapons and the ammunition inside them.
 */
export function sampleProductionGroups(): ProductionPartGroup[] {
  const group = (partGroupId: number, parentGroupId: number | null, sortOrder: number): ProductionPartGroup => ({
    partGroupId,
    code: `part_group_${partGroupId}`,
    nameKey: `production.part_group.part_group_${partGroupId}.name`,
    parentGroupId,
    sortOrder,
  });

  return [group(19, null, 1), group(20, null, 2), group(2, 19, 3), group(22, 19, 4), group(3, 22, 1), group(4, 22, 2), group(7, 22, 3), group(10, 19, 8), group(12, 20, 2)];
}

/**
 * A recipe of a part for the tests (task 00043).
 *
 * @param recipeId The recipe; its name is the text of `production.recipe.recipe_<id>.name`.
 * @param partGroupId The group of the part it makes.
 */
export function sampleProductionPartRecipe(recipeId: number, partGroupId: number): ProductionRecipe {
  return {
    recipeId,
    code: `recipe_${recipeId}`,
    nameKey: `production.recipe.recipe_${recipeId}.name`,
    descriptionKey: `production.recipe.recipe_${recipeId}.description`,
    buildingTypeId: 13,
    partTypeId: recipeId,
    resourceTypeId: null,
    outputQuantity: 1,
    durationSeconds: 60,
    isAvailable: true,
    reasons: [],
    imageKey: null,
    unitSeconds: 60,
    ingredients: [{ resourceTypeId: 1, partTypeId: null, amount: 2, nameKey: 'world.resource.resource_1.name' }],
    partGroupId,
    partGroupNameKey: `production.part_group.part_group_${partGroupId}.name`,
    partGroupCode: `part_group_${partGroupId}`,
  };
}

/**
 * The batches of all the factories of a body for the tests (task 00020, section 7.5): the same
 * batch as the queue of `sampleProductionFactory`, with the places of the queue.
 */
export function sampleProductionOrders(overrides: Partial<ActiveOrders> = {}): ActiveOrders {
  return {
    planetId: 10,
    queueSlots: 2,
    queueUsedSlots: 1,
    orders: [
      {
        orderId: 700,
        planetBuildingId: 500,
        buildingNameKey: 'planets.building.building_13.name',
        recipeId: 1,
        nameKey: 'production.recipe.recipe_1.name',
        quantity: 3,
        producedQuantity: 1,
        position: 1,
        unitSeconds: 40,
        startsAt: '2026-10-26T11:59:00Z',
        nextUnitAt: '2026-10-26T12:00:00Z',
        finishesAt: '2026-10-26T12:02:00Z',
        isBlocked: false,
      },
    ],
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
    stacks: [{ partTypeId: 3, markLevel: 2, hasRaceBonus: true, conditionPercent: 100, quantity: 4, nameKey: 'production.part_type.part_3.name', imageKey: null, partGroupId: 2, partGroupNameKey: 'production.part_group.part_group_2.name', capsuleInhabitants: 0, capsuleUsable: false }],
    storage: { capacity: 100, kinds: [{ partTypeId: 3, quantity: 4, capacity: 100, isFull: false }] },
    gameNow: '2026-10-26T12:00:00Z',
    producedResourceTypeIds: [],
    ...overrides,
  };
}

/**
 * The characteristics of the parts for the tests (task 00027): two of them, both of the group of
 * the hull modules, the second one not required.
 */
export function sampleCharacteristics(overrides: Partial<Characteristic>[] = []): Characteristic[] {
  const rows: Characteristic[] = [
    {
      characteristicId: 1,
      code: 'characteristic_1',
      nameKey: 'production.characteristic.characteristic_1.name',
      descriptionKey: 'production.characteristic.characteristic_1.description',
      unitKey: 'production.characteristic.characteristic_1.unit',
      imageKey: null,
      sortOrder: 1,
      isArchived: false,
      groups: [{ partGroupId: 2, code: 'part_group_2', nameKey: 'production.part_group.part_group_2.name', sortOrder: 1, isRequired: true }],
    },
    {
      characteristicId: 2,
      code: 'characteristic_2',
      nameKey: 'production.characteristic.characteristic_2.name',
      descriptionKey: 'production.characteristic.characteristic_2.description',
      unitKey: 'production.characteristic.characteristic_2.unit',
      imageKey: null,
      sortOrder: 2,
      isArchived: false,
      groups: [{ partGroupId: 2, code: 'part_group_2', nameKey: 'production.part_group.part_group_2.name', sortOrder: 2, isRequired: false }],
    },
  ];

  return rows.map((row, index) => ({ ...row, ...overrides[index] }));
}

/**
 * The weight and the characteristics of every part for the tests (task 00027): the hull module
 * weighs 12.5 tonnes and has the thrust and the fuel use.
 */
export function samplePartCharacteristics(overrides: Partial<PartCharacteristics>[] = []): PartCharacteristics[] {
  const rows: PartCharacteristics[] = [
    {
      partTypeId: 3,
      code: 'part_3',
      nameKey: 'production.part_type.part_3.name',
      partGroupId: 2,
      weight: 12.5,
      characteristics: [
        { characteristicId: 1, numberValue: 120, levels: {} },
        { characteristicId: 2, numberValue: 3.5, levels: {} },
      ],
    },
  ];

  return rows.map((row, index) => ({ ...row, ...overrides[index] }));
}

/**
 * A processes API whose every method is a spy; a test overrides what it needs.
 */
export function fakeProcessesApi(overrides: Partial<ProcessesApi> = {}): ProcessesApi {
  return {
    buildings: vi.fn().mockResolvedValue(sampleProcessesBuildings()),
    building: vi.fn().mockResolvedValue(sampleProcessesBuilding()),
    orders: vi.fn().mockResolvedValue(sampleProcessOrders()),
    enqueue: vi.fn().mockResolvedValue(sampleProcessesBuilding()),
    cancel: vi.fn().mockResolvedValue(sampleProcessesBuilding()),
    terraforming: vi.fn().mockResolvedValue(sampleTerraforming()),
    terraform: vi.fn().mockResolvedValue({
      targetPlanetId: 70,
      shipId: 5,
      partTypeId: 34,
      changedDeposits: 2,
      depositIds: [
        { resourceTypeId: 1, oldCoefficient: 1.1, newCoefficient: 1.5 },
        { resourceTypeId: 3, oldCoefficient: 1.2, newCoefficient: 1.8 },
      ],
      terraforming: sampleTerraforming({ targets: [sampleTerraformingTarget({ isTerraformed: true, shipIds: [] })] }),
    }),
    ...overrides,
  };
}

/**
 * A free body a device may be turned on at, with the ships of the player that stand by it and
 * carry a device (task 00014, section 11).
 */
export function sampleTerraformingTarget(overrides: Partial<TerraformingTarget> = {}): TerraformingTarget {
  return {
    planetId: 70,
    systemId: 3,
    objectTypeId: 2,
    orbit: 4,
    name: 'Kepler-4',
    isTerraformed: false,
    shipIds: [
      {
        shipId: 5,
        devices: [{ partTypeId: 34, nameKey: 'production.part_type.part_34.name', markLevel: 1, quantity: 2 }],
      },
    ],
    ...overrides,
  };
}

/**
 * The terraforming of a player for the tests (task 00014, sections 9 and 11): a free body with a
 * ship by it, a free body that is terraformed already and the range of the multiplier.
 */
export function sampleTerraforming(overrides: Partial<TerraformingState> = {}): TerraformingState {
  return {
    targets: [
      sampleTerraformingTarget(),
      sampleTerraformingTarget({
        planetId: 71,
        orbit: 5,
        name: 'Kepler-5',
        isTerraformed: true,
        shipIds: [],
      }),
    ],
    multiplierMin: 1.4,
    multiplierMax: 1.9,
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
        processTypeNameKey: 'processes.process_type.ship_repair.name',
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
 * The orders of all the buildings of a body that run a process for the tests (task 00020,
 * section 7.5): the same order as the queue of `sampleProcessesBuilding`, with the places.
 */
export function sampleProcessOrders(overrides: Partial<ActiveOrders<ProcessActiveOrder>> = {}): ActiveOrders<ProcessActiveOrder> {
  return {
    planetId: 10,
    queueSlots: 2,
    queueUsedSlots: 1,
    orders: [
      {
        orderId: 800,
        planetBuildingId: 600,
        buildingNameKey: 'planets.building.building_20.name',
        recipeId: 1,
        nameKey: 'processes.process_recipe.recipe_1.name',
        processTypeNameKey: 'processes.process_type.ship_repair.name',
        quantity: 2,
        producedQuantity: 1,
        position: 1,
        unitSeconds: 60,
        startsAt: '2026-10-26T11:59:00Z',
        nextUnitAt: '2026-10-26T12:00:00Z',
        finishesAt: '2026-10-26T12:01:00Z',
        isBlocked: false,
      },
    ],
    gameNow: '2026-10-26T12:00:00Z',
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
 * A row of a reference table for the tests (task 00010): the client reads the name and the
 * picture of a body type from here.
 */
export function sampleReference(overrides: Partial<Reference> = {}): Reference {
  return {
    id: 1,
    code: 'object_type_1',
    nameKey: 'world.object_type.object_type_1.name',
    descriptionKey: 'world.object_type.object_type_1.description',
    groupCode: null,
    groupId: null,
    imageKey: null,
    ...overrides,
  };
}

/**
 * The reference tables of the world for the tests (task 00010).
 */
export function sampleWorldReference(overrides: Partial<WorldReference> = {}): WorldReference {
  return {
    resourceGroups: [sampleReference({ id: 1, code: 'resource_group_1', nameKey: 'world.resource_group.resource_group_1.name' })],
    resources: [
      sampleReference({ id: 1, code: 'resource_1', nameKey: 'world.resource.resource_1.name', groupCode: 'resource_group_1', groupId: 1 }),
      sampleReference({ id: 3, code: 'resource_3', nameKey: 'world.resource.resource_3.name', groupCode: 'resource_group_1', groupId: 1 }),
    ],
    objectTypes: [
      sampleReference({ id: 5, code: 'object_type_5', nameKey: 'world.object_type.object_type_5.name', imageKey: 'art-12a' }),
      sampleReference({ id: 10, code: 'object_type_10', nameKey: 'world.object_type.object_type_10.name', imageKey: 'art-12f' }),
    ],
    sizeClasses: [],
    ...overrides,
  };
}

/**
 * A body of the player for the tests (task 00010).
 */
export function sampleOwnedPlanet(overrides: Partial<OwnedPlanet> = {}): OwnedPlanet {
  return {
    planetId: 10,
    name: 'Nova',
    objectTypeId: 5,
    sectorId: 1,
    systemId: 3,
    orbit: 2,
    sizeClassId: 1,
    size: 4,
    isTerraformed: false,
    deposits: [{ resourceTypeId: 1, coefficient: 1.2 }],
    ...overrides,
  };
}

/**
 * A world API whose every method is a spy; a test overrides what it needs (task 00010).
 */
export function fakeWorldApi(overrides: Partial<WorldApi> = {}): WorldApi {
  return {
    reference: vi.fn().mockResolvedValue(sampleWorldReference()),
    planets: vi.fn().mockResolvedValue([sampleOwnedPlanet()]),
    ...overrides,
  };
}

/**
 * The map of the galaxy for the tests (task 00034): two sectors, the first is the one of the
 * player's home body.
 */
export function sampleMapGalaxy(overrides: Partial<MapGalaxy> = {}): MapGalaxy {
  return {
    sectors: [
      { id: 1, name: 'Aldara', x: 0, y: 0, z: 0, isNewbie: true, systemCount: 4, ownPlanetCount: 1 },
      { id: 2, name: 'Borea', x: 1500, y: 0, z: 0, isNewbie: false, systemCount: 6, ownPlanetCount: 0 },
    ],
    sectorSize: 1000,
    seesOnlyOwn: false,
    ...overrides,
  };
}

/**
 * The map of the first sector for the tests: the home system, an open one, two stars the player
 * only knows of, a nebula, a free field and the range of a radar.
 */
export function sampleMapSector(overrides: Partial<MapSectorView> = {}): MapSectorView {
  return {
    sectorId: 1,
    name: 'Aldara',
    sectorSize: 1000,
    systems: [
      { id: 3, number: 1, name: 'Lyra', x: 200, y: 200, z: 0, inNebula: false, state: 'open', objectCount: 4, ownPlanetCount: 1, distance: 0, travelSeconds: 600 },
      { id: 4, number: 2, name: 'Vega', x: 420, y: 260, z: 0, inNebula: true, state: 'open', objectCount: 2, ownPlanetCount: 0, distance: 230, travelSeconds: 8280 },
      { id: 5, number: 3, name: null, x: 700, y: 640, z: 0, inNebula: false, state: 'known', objectCount: 0, ownPlanetCount: 0, distance: 640, travelSeconds: 23040 },
      { id: 6, number: 4, name: null, x: 760, y: 700, z: 0, inNebula: false, state: 'known', objectCount: 0, ownPlanetCount: 0, distance: 700, travelSeconds: 25200 },
    ],
    nebulas: [{ id: 1, objectId: 90, name: 'Arena', x: 450, y: 250, z: 0, radius: 120, blocksJumpDrive: true }],
    fields: [{ objectId: 91, objectTypeId: 10, name: 'Field A-12', x: 300, y: 500, z: 0, mass: 1200000, distance: 300, travelSeconds: 10800 }],
    radars: [{ planetId: 10, x: 200, y: 200, z: 0, radius: 1500 }],
    seesOnlyOwn: false,
    ...overrides,
  };
}

/**
 * The map of the home system for the tests: the star, the player's body, a free body, a body of
 * another player, a place the player only knows of and a field on the outer orbit.
 */
export function sampleMapSystem(overrides: Partial<MapSystemView> = {}): MapSystemView {
  const none = { planetId: null as number | null, sizeClassId: null as number | null, size: null as number | null, isOwn: false, isFree: false, isTerraformed: false, owner: null as MapOwner | null, stationLimit: null, mass: null as number | null, resources: [] as MapResourceShare[], distance: 0.6 as number | null, travelSeconds: 600 as number | null };

  return {
    system: { id: 3, number: 1, name: 'Lyra', x: 200, y: 200, z: 0, inNebula: false, state: 'open', objectCount: 5, ownPlanetCount: 1, distance: 0, travelSeconds: 600 },
    sectorName: 'Aldara',
    orbitCount: 12,
    objects: [
      { ...none, id: 100, state: 'open', orbit: null, objectClass: 'star', objectTypeId: 5, name: 'Lyra', planetId: null },
      { ...none, id: 101, state: 'open', orbit: 2, objectClass: 'planet', objectTypeId: 5, name: 'Nova', planetId: 10, sizeClassId: 1, size: 4, isOwn: true, resources: [{ resourceId: 1, coefficient: 1.2 }], distance: 0 },
      { ...none, id: 102, state: 'open', orbit: 3, objectClass: 'planet', objectTypeId: 5, name: 'Lyra-3', planetId: 11, sizeClassId: 1, size: 3, isFree: true, resources: [{ resourceId: 3, coefficient: 0.9 }] },
      { ...none, id: 103, state: 'open', orbit: 5, objectClass: 'planet', objectTypeId: 5, name: 'Icar', planetId: 12, sizeClassId: 1, size: 4, owner: { nickname: 'Krell_77', raceCode: 'race_3', raceNameKey: 'world.race.race_3.name', raceColorKey: 'race-3' }, distance: 1.8 },
      { ...none, id: 104, state: 'known', orbit: 9, objectClass: 'unknown', objectTypeId: null, name: null, planetId: null },
      { ...none, id: 105, state: 'open', orbit: 12, objectClass: 'asteroid_field', objectTypeId: 10, name: 'Lyra Belt', mass: 800000, resources: [{ resourceId: 1, coefficient: 0.5 }], distance: 6 },
    ],
    ...overrides,
  };
}

/**
 * A map API whose every method is a spy; a test overrides what it needs (task 00034).
 */
export function fakeMapApi(overrides: Partial<MapApi> = {}): MapApi {
  return {
    galaxy: vi.fn().mockResolvedValue(sampleMapGalaxy()),
    sector: vi.fn().mockResolvedValue(sampleMapSector()),
    system: vi.fn().mockResolvedValue(sampleMapSystem()),
    search: vi.fn().mockResolvedValue([{ kind: 'system', sectorId: 1, systemId: 3, objectId: null, objectTypeId: null, name: 'Lyra', systemName: 'Lyra', orbit: null }]),
    target: vi.fn().mockResolvedValue({ kind: 'point', targetId: null, sectorId: 1, x: 500, y: 500, z: 0, name: null, nearSystemId: 3, nearSystemName: 'Lyra', inNebula: false, jumpForbidden: false, state: 'unknown', distance: 420.5, travelSeconds: 15138, errors: [] }),
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
    accountTier: 'standard',
    civilization: { raceId: 1, raceNameKey: 'world.race.race_1.name', planetId: 10, planetName: 'Nova', systemId: 3, sectorId: 1 },
    ...overrides,
  };
}

/**
 * The fake server interfaces of the tests that a test may override.
 */
type FakeApis = { auth?: AuthApi; content?: ContentApi; economy?: EconomyApi; game?: GameApi; map?: MapApi; market?: MarketApi; planets?: PlanetApi; processes?: ProcessesApi; production?: ProductionApi; research?: ResearchApi; texts?: TextsApi; world?: WorldApi };

/**
 * The server interfaces of a test: every one is a fake unless the test gives its own.
 */
function buildApi(options: FakeApis): Api {
  return {
    auth: options.auth ?? fakeAuthApi(),
    content: options.content ?? mockContentApi,
    economy: options.economy ?? fakeEconomyApi(),
    game: options.game ?? fakeGameApi(),
    map: options.map ?? fakeMapApi(),
    market: options.market ?? fakeMarketApi(),
    planets: options.planets ?? fakePlanetApi(),
    processes: options.processes ?? fakeProcessesApi(),
    production: options.production ?? fakeProductionApi(),
    research: options.research ?? fakeResearchApi(),
    texts: options.texts ?? fakeTextsApi(),
    world: options.world ?? fakeWorldApi(),
  };
}

/**
 * Renders the site at an address with fake server interfaces.
 */
export function renderPage(path: string, options: FakeApis = {}): RenderResult & { api: Api } {
  window.history.replaceState(null, '', path);

  const api = buildApi(options);
  const result = render(
    <AppProviders api={api}>
      <Pages />
    </AppProviders>,
  );

  return { ...result, api };
}

/**
 * Renders one component, not a page, inside the providers of the site with fake server
 * interfaces, for a component that no page shows at the moment.
 */
export function renderPanel(panel: ReactNode, options: FakeApis = {}): RenderResult & { api: Api } {
  window.history.replaceState(null, '', '/');

  const api = buildApi(options);
  const result = render(<AppProviders api={api}>{panel}</AppProviders>);

  return { ...result, api };
}
