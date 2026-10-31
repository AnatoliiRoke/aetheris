import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Planet } from '../../../api/planetApi';
import type { ResearchTree, Technology } from '../../../api/researchApi';
import {
  fakeAuthApi,
  fakePlanetApi,
  fakeProductionApi,
  fakeResearchApi,
  fakeTextsApi,
  fakeWorldApi,
  renderPage,
  sampleResearchTree,
  sampleProductionStock,
  samplePlanet,
  sampleTexts,
  sampleWorldReference,
  signedInMe,
} from '../../../test/renderPage';
import { setRenderProbe } from '../renderProbe';

/**
 * The elements of the design of the four pages that were missing in the client (task 00020,
 * section 7.9): every test names one element of the mock-up.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const withPlanet = (overrides: Partial<Planet>) => fakePlanetApi({ planet: vi.fn().mockResolvedValue(samplePlanet(overrides)) });

/** A planet with the produced group: one more group, one more resource and a storage for it. */
const withProducedGroup = (resources: Planet['resources']) =>
  samplePlanet({
    resources,
    resourceTypes: [
      { id: 1, code: 'resource_1', nameKey: 'world.resource.resource_1.name', resourceGroupId: 1 },
      { id: 3, code: 'resource_3', nameKey: 'world.resource.resource_3.name', resourceGroupId: 1 },
      { id: 4, code: 'resource_13', nameKey: 'world.resource.resource_13.name', resourceGroupId: 6 },
    ],
    resourceGroups: [
      { id: 1, code: 'resource_group_1', nameKey: 'world.resource_group.resource_group_1.name', resourceGroupId: null },
      { id: 6, code: 'resource_group_6', nameKey: 'world.resource_group.resource_group_6.name', resourceGroupId: null },
    ],
  });

const producedTexts = {
  texts: fakeTextsApi({
    ...sampleTexts,
    uk: { ...sampleTexts.uk, 'world.resource.resource_13.name': 'Будматеріали', 'world.resource_group.resource_group_6.name': 'Продукція' },
  }),
};

describe('/planet: the elements of the design', () => {
  it('shows the balance of the energy with its sign and the explanation', async () => {
    renderPage('/planet', { auth: signedIn(), planets: withPlanet({ energy: { output: 450, consumption: 380, balance: 70, queuedBalance: -25 } }) });

    const energy = await screen.findByRole('region', { name: 'Енергія' });

    expect(within(energy).getByText('+70')).toBeInTheDocument();
    expect(within(energy).getByText('-25')).toBeInTheDocument();
    expect(within(energy).getByText('Баланс після черги')).toBeInTheDocument();
    expect(within(energy).getByText('Будівля, через яку баланс стане від’ємним, у чергу не потрапить.')).toBeInTheDocument();
  });

  it('gives the resources a subtitle and an icon in every row', async () => {
    renderPage('/planet', { auth: signedIn() });

    const resources = await screen.findByRole('region', { name: 'Ресурси' });

    expect(within(resources).getByText('Видобуток за годину')).toBeInTheDocument();

    const rows = within(resources).getAllByRole('row').slice(1);

    expect(rows).toHaveLength(2);
    rows.forEach((row) => expect(row.querySelector('svg.game-icon')).not.toBeNull());
  });

  it('gives the rows of the storage an icon and shows the group of the produced resources', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(
        withProducedGroup([
          { resourceTypeId: 1, resourceGroupId: 1, amount: 900, ratePerHour: 60, capacity: 10000, isFull: false, fullAt: null },
          { resourceTypeId: 4, resourceGroupId: 6, amount: 630, ratePerHour: 0, capacity: 2500, isFull: false, fullAt: null },
        ]),
      ),
    });
    renderPage('/planet', { auth: signedIn(), planets, ...producedTexts });

    const storage = await screen.findByRole('region', { name: 'Склад' });

    expect(within(storage).getByRole('heading', { name: 'Продукція' })).toBeInTheDocument();

    const row = within(storage).getByText('Будматеріали').closest('li')!;

    expect(row.querySelector('svg.game-icon')).not.toBeNull();
    expect(within(row).getByText('630 / 2 500')).toBeInTheDocument();
  });

  it('does not call a resource without a storage full and says there is no storage', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(withProducedGroup([{ resourceTypeId: 4, resourceGroupId: 6, amount: 50, ratePerHour: 0, capacity: 0, isFull: false, fullAt: null }])),
    });
    renderPage('/planet', { auth: signedIn(), planets, ...producedTexts });

    const storage = await screen.findByRole('region', { name: 'Склад' });
    const row = within(storage).getByText('Будматеріали').closest('li')!;

    expect(within(row).queryByText('Повний')).not.toBeInTheDocument();
    expect(within(row).getByText('50 · без складу')).toBeInTheDocument();
    expect(within(row).getByRole('progressbar', { name: 'Будматеріали' })).toHaveAttribute('aria-valuenow', '0');
    expect(screen.queryByText(/Склад заповнений/)).not.toBeInTheDocument();
  });

  it('writes the free slots in the head of the queue', async () => {
    renderPage('/planet', { auth: signedIn() });

    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });

    expect(within(queue).getByText('Місць: 1 з 2')).toBeInTheDocument();
    expect(within(queue).getByText('+1 слот доступний')).toBeInTheDocument();
  });

  it('writes the free slots in the plural of the language', async () => {
    renderPage('/planet', { auth: signedIn(), planets: withPlanet({ queue: { slotCount: 5, items: [] } }) });

    expect(await screen.findByText('+5 слотів доступно')).toBeInTheDocument();
  });

  it('reads the type of the body by its identifier, not by its code', async () => {
    renderPage('/planet', { auth: signedIn() });

    const card = await screen.findByRole('region', { name: 'Планета' });

    expect(within(card).getByText('Вулканічна')).toBeInTheDocument();
  });

  it('shows an unknown type when the identifier is not in the reference table', async () => {
    renderPage('/planet', { auth: signedIn(), planets: withPlanet({ typeId: 999 }) });

    const card = await screen.findByRole('region', { name: 'Планета' });

    expect(within(card).getByText('Невідомий тип')).toBeInTheDocument();
  });
});

describe('/planet/build: the elements of the design', () => {
  it('has the breadcrumbs from the planet to the page', async () => {
    renderPage('/planet/build', { auth: signedIn() });

    const crumbs = await screen.findByRole('navigation', { name: 'Навігація сторінкою' });

    expect(within(crumbs).getByRole('link', { name: 'Планета' })).toHaveAttribute('href', '/planet');
    expect(crumbs.querySelector('[aria-current="page"]')?.textContent).toBe('Nova');
  });

  it('writes the free slots in the head of the queue', async () => {
    renderPage('/planet/build', { auth: signedIn() });

    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });

    expect(within(queue).getByText('+1 слот доступний')).toBeInTheDocument();
  });

  it('labels the effects and the required resources, and shows what is short in each row', async () => {
    const base = samplePlanet();
    const [first, ...rest] = base.buildings;
    const planet = samplePlanet({
      buildings: [
        {
          ...first,
          next: {
            ...first.next,
            costs: [
              { resourceTypeId: 1, amount: 510 },
              { resourceTypeId: 3, amount: 120 },
            ],
            canEnqueue: false,
            reasonCodes: ['planets_not_enough_resources'],
            missingResources: [{ resourceTypeId: 1, amount: 40 }],
          },
        },
        ...rest,
      ],
    });
    renderPage('/planet/build', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(planet) }) });

    const card = (await screen.findByRole('heading', { name: 'Адміністративний центр' })).closest('li')!;
    const costs = within(card).getByRole('list', { name: 'Потрібні ресурси' });
    const iron = within(costs).getByText('Залізо').closest('li')!;
    const copper = within(costs).getByText('Мідь').closest('li')!;

    expect(within(card).getByText('Ефекти')).toBeInTheDocument();
    expect(within(card).getAllByText('Потрібні ресурси').length).toBeGreaterThan(0);
    expect(within(iron).getByText('не вистачає 40')).toBeInTheDocument();
    expect(within(copper).queryByText(/не вистачає/)).not.toBeInTheDocument();
    expect(within(card).getByRole('button', { name: 'Покращити' })).toBeDisabled();
  });

  it('shows the energy after the building as the output and the consumption', async () => {
    renderPage('/planet/build', { auth: signedIn() });

    const card = (await screen.findByRole('heading', { name: 'Адміністративний центр' })).closest('li')!;

    expect(within(card).getByText('Енергія після завершення')).toBeInTheDocument();
    expect(within(card).getByText('40 / 32')).toBeInTheDocument();
  });
});

const technology = (overrides: Partial<Technology>): Technology => ({ ...sampleResearchTree().technologies[0], ...overrides });

const researchTexts = {
  texts: fakeTextsApi({
    uk: {
      ...sampleTexts.uk,
      'research.branch.economy.name': 'Економіка',
      'research.technology.tech_1.name': 'Глибинний видобуток',
      'research.technology.tech_1.description': 'Шахти видобувають більше металів.',
      'research.technology.tech_2.name': 'Нова технологія',
      'research.technology.tech_2.description': 'Цього опису не видно.',
    },
  }),
};

const treeWith = (technologies: Technology[]): ResearchTree => sampleResearchTree({ technologies });

describe('/planet/research: the elements of the design', () => {
  afterEach(() => setRenderProbe(null));

  it('does not draw a tile again when the search only hides another one', async () => {
    const user = userEvent.setup();
    const renders = new Map<string, number>();
    setRenderProbe((name) => renders.set(name, (renders.get(name) ?? 0) + 1));
    const second = technology({ id: 2, code: 'tech_2', nameKey: 'research.technology.tech_2.name', descriptionKey: 'research.technology.tech_2.description', sortOrder: 2 });
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(treeWith([technology({}), second])) }), ...researchTexts });

    await screen.findByRole('heading', { name: 'Нова технологія' });
    const before = renders.get('TechnologyCard') ?? 0;

    await user.type(screen.getByRole('searchbox'), 'Глибинний');

    expect(screen.queryByRole('heading', { name: 'Нова технологія' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Глибинний видобуток' })).toBeInTheDocument();
    expect(before).toBeGreaterThanOrEqual(2);
    expect(renders.get('TechnologyCard') ?? 0).toBe(before);
  });

  it('shows what is short of in each row and keeps the button inactive with the reason', async () => {
    const [first] = sampleResearchTree().technologies[0].levels;
    const short = technology({
      levels: [{ ...first, costs: [{ resourceTypeId: 1, amount: 300 }, { resourceTypeId: 3, amount: 200 }], missingResources: [{ resourceTypeId: 3, amount: 50 }] }],
    });
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(treeWith([short])) }), ...researchTexts });

    const tile = (await screen.findByRole('heading', { name: 'Глибинний видобуток' })).closest('li')!;
    const copper = within(tile).getByText('Мідь').closest('li')!;
    const iron = within(tile).getByText('Залізо').closest('li')!;
    const button = within(tile).getByRole('button', { name: 'Дослідити' });

    expect(within(copper).getByText('не вистачає 50')).toBeInTheDocument();
    expect(within(iron).queryByText(/не вистачає/)).not.toBeInTheDocument();
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', 'Не вистачає ресурсів');
  });

  it('shows the effect now and the effect of a level, from the numbers of the server', async () => {
    const [first] = sampleResearchTree().technologies[0].levels;
    const effect = (value: number) => ({ effectType: 'metal_production', unlockKind: null, unlockTargetId: null, unlockNameKey: null, value });
    const learned = technology({
      learnedLevel: 2,
      currentEffects: [effect(1.16)],
      levels: [
        { ...first, level: 2, effects: [effect(1.16)] },
        { ...first, level: 3, effects: [effect(1.24)] },
      ],
    });
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(treeWith([learned])) }), ...researchTexts });

    const tile = (await screen.findByRole('heading', { name: 'Глибинний видобуток' })).closest('li')!;

    expect(within(tile).getByText('Ефект')).toBeInTheDocument();
    expect(within(tile).getByText('+16%')).toBeInTheDocument();
    expect(within(tile).getByText('+8% / рівень')).toBeInTheDocument();
  });

  it('shows an unknown technology dimmed, with the name and the branch only', async () => {
    const unknown = technology({ id: 2, code: 'tech_2', nameKey: 'research.technology.tech_2.name', descriptionKey: 'research.technology.tech_2.description', status: 'unknown', sortOrder: 2 });
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(treeWith([technology({}), unknown])) }), ...researchTexts });

    const tile = (await screen.findByRole('heading', { name: 'Нова технологія' })).closest('li')!;

    expect(tile).toHaveClass('tech-card--unknown');
    expect(within(tile).getByText('Невідома')).toBeInTheDocument();
    expect(within(tile).getByText('Гілка: Економіка')).toBeInTheDocument();
    expect(within(tile).queryByText('Цього опису не видно.')).not.toBeInTheDocument();
    expect(within(tile).queryByRole('list')).not.toBeInTheDocument();
    expect(within(tile).getByRole('button', { name: 'Дослідити' })).toBeDisabled();
  });

  it('keeps the unknown and the closed technologies under the chip Closed', async () => {
    const user = userEvent.setup();
    const closed = technology({ id: 3, code: 'tech_3', nameKey: 'research.technology.tech_3.name', status: 'unavailable', reasonCode: 'research_center_level_unmet', researchCenterLevel: 5, sortOrder: 3 });
    const unknown = technology({ id: 2, code: 'tech_2', nameKey: 'research.technology.tech_2.name', status: 'unknown', sortOrder: 2 });
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(treeWith([technology({}), unknown, closed])) }), ...researchTexts });

    await screen.findByRole('heading', { name: 'Глибинний видобуток' });
    const filter = screen.getByRole('group', { name: 'Стан' });

    expect(within(filter).getAllByRole('button').map((chip) => chip.textContent)).toEqual(['Усі', 'Доступні', 'У черзі', 'Досліджуються', 'Вивчені', 'Закриті']);

    await user.click(within(filter).getByRole('button', { name: 'Закриті' }));

    expect(screen.getAllByRole('heading', { level: 3 })).toHaveLength(2);
    expect(screen.queryByRole('heading', { name: 'Глибинний видобуток' })).not.toBeInTheDocument();
  });

  it('names the level of the science center with the hint, and has the breadcrumb', async () => {
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(sampleResearchTree()) }), ...researchTexts });

    expect(await screen.findByText('Науковий центр · рівень 2 · Вищі рівні відкривають більше технологій')).toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'Навігація сторінкою' })).getByText('Дослідження')).toBeInTheDocument();
  });

  it('writes the free slots in the head of the research queue', async () => {
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(sampleResearchTree()) }), ...researchTexts });

    const queue = await screen.findByRole('region', { name: 'Черга досліджень' });

    expect(within(queue).getByText('+2 слоти доступні')).toBeInTheDocument();
  });
});

describe('/planet/storage: the elements of the design', () => {
  const producedPlanet = withProducedGroup([
    { resourceTypeId: 1, resourceGroupId: 1, amount: 4200, ratePerHour: 0, capacity: 4200, isFull: true, fullAt: '2026-10-26T11:00:00Z' },
    { resourceTypeId: 4, resourceGroupId: 6, amount: 630, ratePerHour: 0, capacity: 2500, isFull: false, fullAt: null },
  ]);
  const apis = () => ({
    auth: signedIn(),
    planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(producedPlanet) }),
    production: fakeProductionApi({ stock: vi.fn().mockResolvedValue(sampleProductionStock({ producedResourceTypeIds: [4] })) }),
    ...producedTexts,
  });

  it('has the breadcrumbs, and the rate per hour and the fill in the table of the resources', async () => {
    renderPage('/planet/storage', apis());

    const crumbs = await screen.findByRole('navigation', { name: 'Навігація сторінкою' });

    expect(within(crumbs).getByRole('link', { name: 'Планета' })).toHaveAttribute('href', '/planet');

    const table = await screen.findByRole('table', { name: 'Ресурси' });
    const iron = within(table).getByText('Залізо').closest('tr')!;

    expect(within(iron).getByText('зупинено')).toBeInTheDocument();
    expect(within(iron).getByText('100%')).toBeInTheDocument();
    expect(within(iron).getByText('Повний')).toBeInTheDocument();
  });

  it('shows the produced resources as the real data on the production tab and not among the raw ones', async () => {
    const user = userEvent.setup();
    renderPage('/planet/storage', apis());

    const raw = await screen.findByRole('table', { name: 'Ресурси' });

    await waitFor(() => expect(within(raw).queryByText('Будматеріали')).not.toBeInTheDocument());
    await user.click(screen.getByRole('tab', { name: 'Продукція' }));

    const production = await screen.findByRole('table', { name: 'Продукція' });
    const row = within(production).getByText('Будматеріали').closest('tr')!;

    expect(within(production).getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual(['Назва', 'Кількість', 'Заповнення']);
    expect(within(row).getByText('630 / 2 500')).toBeInTheDocument();
    expect(within(row).getByText('25%')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Недоступно' })).not.toBeInTheDocument();
  });

  it('shows the alarm with the advice and the way to the construction', async () => {
    renderPage('/planet/storage', apis());

    const alarm = await screen.findByText(/^Склад заповнений, видобуток зупинено: Залізо\. Збудуйте більше місткості\./);

    expect(within(alarm).getByRole('link', { name: 'До будівництва' })).toHaveAttribute('href', '/planet/build');
  });

  it('draws no alarm while no resource is full', async () => {
    renderPage('/planet/storage', { auth: signedIn() });

    await screen.findByRole('table', { name: 'Ресурси' });

    expect(screen.queryByText(/Збудуйте більше місткості/)).not.toBeInTheDocument();
  });

  it('shows a resource without a storage with no fill and no badge', async () => {
    const planet = withProducedGroup([{ resourceTypeId: 1, resourceGroupId: 1, amount: 50, ratePerHour: 10, capacity: 0, isFull: false, fullAt: null }]);
    renderPage('/planet/storage', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(planet) }) });

    const row = (await screen.findByRole('table', { name: 'Ресурси' })).querySelector('tbody')!.querySelectorAll('tr')[1] as HTMLElement;

    expect(within(row).getByText('50 · без складу')).toBeInTheDocument();
    expect(within(row).queryByRole('progressbar')).not.toBeInTheDocument();
    expect(within(row).queryByText('Повний')).not.toBeInTheDocument();
  });
});

describe('/game: the elements of the design', () => {
  it('has the collect button of the design, inactive with the reason in the hint', async () => {
    renderPage('/game', { auth: signedIn(), world: fakeWorldApi({ reference: vi.fn().mockResolvedValue(sampleWorldReference()) }) });

    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });
    const button = within(queue).getByRole('button', { name: 'Забрати' });

    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', expect.stringMatching(/^Скоро: /));
  });
});
