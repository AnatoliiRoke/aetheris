import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { Planet } from '../../../api/planetApi';
import { fakeAuthApi, fakePlanetApi, fakeProductionApi, fakeWorldApi, renderPage, samplePlanet, sampleProductionStock, sampleWorldReference, signedInMe } from '../../../test/renderPage';

/**
 * The elements of the design that the last comparison of the mock-ups with the client found
 * missing (task 00020, section 7.10): every test names one element of the mock-up.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const withPlanet = (overrides: Partial<Planet>) => fakePlanetApi({ planet: vi.fn().mockResolvedValue(samplePlanet(overrides)) });

const closedBuilding = (level: number, requirement: Planet['buildings'][number]['next']['unmetRequirements'][number]): Planet['buildings'][number] => {
  const [, second] = samplePlanet().buildings;

  return {
    ...second,
    level,
    next: { ...second.next, reasonCodes: ['planets_requirement_unmet'], unmetRequirements: [requirement] },
  };
};

describe('/planet: the two columns of the design', () => {
  it('has the card, the resources, the storage, the energy and the population in the wide column and the queues in the narrow one', async () => {
    renderPage('/planet', { auth: signedIn() });

    const resources = await screen.findByRole('region', { name: 'Ресурси' });
    const queues = await screen.findByRole('complementary', { name: 'Черги' });
    const storage = screen.getByRole('region', { name: 'Склад' });
    const energy = screen.getByRole('region', { name: 'Енергія' });

    expect(queues.contains(resources)).toBe(false);
    expect(queues.contains(storage)).toBe(false);
    expect(queues.contains(energy)).toBe(false);
    expect(within(queues).getByRole('region', { name: 'Черга будівництва' })).toBeInTheDocument();
    expect(within(queues).getByRole('region', { name: 'Черга досліджень' })).toBeInTheDocument();
    expect(resources.closest('.planet-layout__main')).not.toBeNull();
    expect(queues.className).toContain('planet-layout__aside');
  });

  it('keeps the order of the design: resources, storage, energy, population, then the queues', async () => {
    renderPage('/planet', { auth: signedIn() });

    const resources = await screen.findByRole('region', { name: 'Ресурси' });
    const storage = screen.getByRole('region', { name: 'Склад' });
    const energy = screen.getByRole('region', { name: 'Енергія' });
    const population = await screen.findByRole('region', { name: 'Населення' });
    const queues = screen.getByRole('complementary', { name: 'Черги' });
    const follows = (first: HTMLElement, second: HTMLElement) => Boolean(first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING);

    expect(follows(resources, storage)).toBe(true);
    expect(follows(storage, energy)).toBe(true);
    expect(follows(energy, population)).toBe(true);
    expect(follows(population, queues)).toBe(true);
  });

  it('shows how full the population is as a bar, from the two numbers of the server', async () => {
    renderPage('/planet', { auth: signedIn() });

    const population = await screen.findByRole('region', { name: 'Населення' });
    const bar = await within(population).findByRole('progressbar', { name: 'Населення' });

    // 750 of 900 inhabitants.
    expect(bar).toHaveAttribute('aria-valuenow', '83');
  });
});

describe('the queue: a record that is finishing', () => {
  it('cannot be cancelled and says why, while a waiting record can', async () => {
    const planet = samplePlanet({
      // The clock of the server is past the end of the record under way.
      gameNow: '2026-10-26T12:05:00Z',
      queue: {
        slotCount: 3,
        items: [
          { id: 7, buildingTypeId: 5, targetLevel: 1, position: 1, durationSeconds: 60, startedAt: '2026-10-26T11:59:30Z', finishesAt: '2026-10-26T12:00:30Z', costs: [] },
          { id: 8, buildingTypeId: 1, targetLevel: 2, position: 2, durationSeconds: 60, startedAt: null, finishesAt: null, costs: [] },
        ],
      },
    });
    renderPage('/planet/build', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(planet) }) });

    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });
    const buttons = await within(queue).findAllByRole('button', { name: /^Скасувати: / });

    expect(buttons).toHaveLength(2);
    expect(buttons[0]).toBeDisabled();
    expect(buttons[0]).toHaveAttribute('title', 'Уже завершується: скасувати не можна');
    expect(buttons[1]).toBeEnabled();
  });
});

describe('/planet/build: the closed building', () => {
  it('is Closed with no cost and says what it opens with, when a building is more than a level short', async () => {
    const closed = closedBuilding(0, {
      requirementId: 1,
      requirementType: 'building_level',
      targetId: 1,
      targetLevel: 4,
      reasonCode: 'planets_requirement_building_level',
      targetNameKey: 'planets.building.building_1.name',
      isClosed: true,
    });
    const [first, , third] = samplePlanet().buildings;
    renderPage('/planet/build', { auth: signedIn(), planets: withPlanet({ buildings: [first, closed, third] }) });

    const card = (await screen.findByRole('heading', { name: 'Науковий центр' })).closest('li')!;

    expect(within(card).getByText('Закрито')).toBeInTheDocument();
    expect(within(card).queryByText('Потрібні ресурси')).not.toBeInTheDocument();
    expect(within(card).queryByText('Не збудовано')).not.toBeInTheDocument();
    expect(within(card).getByText('Відкриється з: Адміністративний центр, рівень 4')).toBeInTheDocument();
    expect(within(card).getByRole('button', { name: 'Збудувати' })).toBeDisabled();
  });

  it('is Closed behind a technology that is not learned', async () => {
    const closed = closedBuilding(0, {
      requirementId: 2,
      requirementType: 'technology_level',
      targetId: 3,
      targetLevel: 1,
      reasonCode: 'planets_requirement_technology',
      targetNameKey: null,
      isClosed: true,
    });
    const [first, , third] = samplePlanet().buildings;
    renderPage('/planet/build', { auth: signedIn(), planets: withPlanet({ buildings: [first, closed, third] }) });

    const card = (await screen.findByRole('heading', { name: 'Науковий центр' })).closest('li')!;

    expect(within(card).getByText('Закрито')).toBeInTheDocument();
    expect(within(card).getByText('Потрібна технологія, вона відкриється з дослідженнями')).toBeInTheDocument();
  });

  it('stays Not built, with the cost and what to prepare, when the requirement is one level short', async () => {
    renderPage('/planet/build', { auth: signedIn() });

    const card = (await screen.findByRole('heading', { name: 'Науковий центр' })).closest('li')!;

    expect(within(card).queryByText('Закрито')).not.toBeInTheDocument();
    expect(within(card).getByText('Не збудовано')).toBeInTheDocument();
    expect(within(card).getAllByText('Потрібні ресурси').length).toBeGreaterThan(0);
    expect(within(card).getByText('Потрібно: Адміністративний центр, рівень 2')).toBeInTheDocument();
  });

  it('does not close a building that is built', async () => {
    const built = closedBuilding(2, {
      requirementId: 1,
      requirementType: 'building_level',
      targetId: 1,
      targetLevel: 9,
      reasonCode: 'planets_requirement_building_level',
      targetNameKey: 'planets.building.building_1.name',
      isClosed: true,
    });
    const [first, , third] = samplePlanet().buildings;
    renderPage('/planet/build', { auth: signedIn(), planets: withPlanet({ buildings: [first, built, third] }) });

    const card = (await screen.findByRole('heading', { name: 'Науковий центр' })).closest('li')!;

    expect(within(card).queryByText('Закрито')).not.toBeInTheDocument();
    expect(within(card).getByText('Рівень 2')).toBeInTheDocument();
  });
});

describe('/planet/research: the note of the queue', () => {
  it('says that a research belongs to the player and works on every colony', async () => {
    renderPage('/planet/research', { auth: signedIn() });

    expect(await screen.findByText('Скасування повертає всю вартість. Дослідження належить вам — вивчене діє на кожній колонії.')).toBeInTheDocument();
  });
});

describe('/planet/storage: the headings and the unit of the design', () => {
  it('calls the blocks Ship modules and Ship hulls, as the design does', async () => {
    renderPage('/planet/storage', { auth: signedIn() });

    expect(await screen.findByRole('region', { name: 'Модулі кораблів' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Корпуси кораблів' })).toBeInTheDocument();
  });

  it('writes the unit after the quantity of a part', async () => {
    const user = userEvent.setup();
    renderPage('/planet/storage', { auth: signedIn(), production: fakeProductionApi({ stock: vi.fn().mockResolvedValue(sampleProductionStock()) }) });

    await user.click(await screen.findByRole('tab', { name: 'Модулі' }));

    const row = within(await screen.findByRole('table', { name: 'Модулі' })).getByText('Корпус «Оса» Мк. II').closest('tr')!;

    expect(within(row).getByText('шт.')).toBeInTheDocument();
  });

  it('lays the blocks out in a grid of their own', async () => {
    renderPage('/planet/storage', { auth: signedIn() });

    const resources = await screen.findByRole('region', { name: 'Ресурси' });

    expect(resources.parentElement).toHaveClass('storage-grid');
    expect(screen.getByRole('region', { name: 'Артефакти' }).parentElement).toBe(resources.parentElement);
  });
});

describe('/game: the empty queue of the design', () => {
  it('says the planet does not grow and offers to add a construction', async () => {
    const planet = samplePlanet({ queue: { slotCount: 2, items: [] } });
    renderPage('/game', {
      auth: signedIn(),
      planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(planet) }),
      world: fakeWorldApi({ reference: vi.fn().mockResolvedValue(sampleWorldReference()) }),
    });

    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });

    expect(within(queue).getByText('Черга порожня — планета не росте.')).toBeInTheDocument();
    expect(within(queue).getByRole('link', { name: '+ Додати будівництво' })).toHaveAttribute('href', '/planet/build');
  });

  it('shows the race of the player by name next to the nickname and the planets', async () => {
    renderPage('/game', { auth: signedIn(), world: fakeWorldApi({ reference: vi.fn().mockResolvedValue(sampleWorldReference()) }) });

    expect(await screen.findByText('Раса: Терани')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Мої планети' })).toBeInTheDocument();
  });
});
