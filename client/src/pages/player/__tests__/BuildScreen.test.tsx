import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { LANGUAGE_STORAGE_KEY } from '../../../i18n/I18nProvider';
import { fakeAuthApi, fakePlanetApi, renderPage, samplePlanet, signedInMe } from '../../../test/renderPage';

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('BuildPage: the screen of the construction (task 00020)', () => {
  it('shows the queue on the whole width and the buildings grouped by the categories of the data', async () => {
    renderPage('/planet/build', { auth: signedIn() });

    expect(await screen.findByRole('heading', { level: 1, name: 'Будівництво: Nova' })).toBeInTheDocument();
    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });
    expect(within(queue).getByText('Шахта заліза, рівень 1')).toBeInTheDocument();
    expect(within(queue).getByText('Скасування повертає всю вартість.')).toBeInTheDocument();

    // The names of the groups come from the texts of the game data; a category without a
    // building makes no group, and nothing falls into "other" while every building has a category.
    expect(await screen.findByRole('region', { name: 'Дослідження' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Видобуток' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Населення' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Торгівля' })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Інше' })).not.toBeInTheDocument();
    const research = screen.getByRole('region', { name: 'Дослідження' });
    expect(within(research).getByRole('heading', { name: 'Науковий центр' })).toBeInTheDocument();
  });

  it('orders the groups by the order of the data, not by the order of the answer', async () => {
    renderPage('/planet/build', { auth: signedIn() });

    await screen.findByRole('region', { name: 'Дослідження' });
    const titles = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent);

    // The answer lists the research group first; `sortOrder` puts the extraction first.
    expect(titles.filter((title) => ['Видобуток', 'Населення', 'Дослідження'].includes(title ?? ''))).toEqual(['Видобуток', 'Населення', 'Дослідження']);
  });

  it('groups by the category and never by the class or the effects of a building', async () => {
    // The mine and the science centre swap categories: the tiles follow the categories, whatever they are.
    const planet = samplePlanet();
    const swapped = {
      ...planet,
      buildings: planet.buildings.map((building) =>
        building.buildingTypeId === 5 ? { ...building, categoryId: 11 } : building.buildingTypeId === 2 ? { ...building, categoryId: 12 } : building,
      ),
    };
    renderPage('/planet/build', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(swapped) }) });

    const research = await screen.findByRole('region', { name: 'Дослідження' });
    expect(within(research).getByRole('heading', { name: 'Шахта заліза' })).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Видобуток' })).getByRole('heading', { name: 'Науковий центр' })).toBeInTheDocument();
  });

  it('puts a building without a known category into "other" only when the data gives none', async () => {
    const planet = samplePlanet();
    const unknown = { ...planet, buildings: planet.buildings.map((building) => (building.buildingTypeId === 5 ? { ...building, categoryId: 999 } : building)) };
    renderPage('/planet/build', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(unknown) }) });

    const other = await screen.findByRole('region', { name: 'Інше' });
    expect(within(other).getByRole('heading', { name: 'Шахта заліза' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Видобуток' })).not.toBeInTheDocument();
  });

  it('keeps a closed building inactive and says why', async () => {
    renderPage('/planet/build', { auth: signedIn() });

    const card = (await screen.findByRole('heading', { name: 'Науковий центр' })).closest('li')!;
    const button = within(card).getByRole('button', { name: 'Збудувати' });

    expect(button).toBeDisabled();
    expect(within(card).getByText('Потрібно: Адміністративний центр, рівень 2')).toBeInTheDocument();
    expect(within(card).getByText('Не вистачить енергії: збудуй або покращ електростанцію')).toBeInTheDocument();
    expect(button).toHaveAttribute('title', expect.stringContaining('Адміністративний центр'));
  });

  it('queues the next level and cancels a record', async () => {
    const user = userEvent.setup();
    const planets = fakePlanetApi();
    renderPage('/planet/build', { auth: signedIn(), planets });

    const card = (await screen.findByRole('heading', { name: 'Адміністративний центр' })).closest('li')!;
    await user.click(within(card).getByRole('button', { name: 'Покращити' }));

    expect(planets.enqueue).toHaveBeenCalledWith(10, 1);
    expect(await screen.findByText('У черзі: Адміністративний центр, рівень 2.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Скасувати: Шахта заліза, рівень 1/ }));

    expect(planets.cancel).toHaveBeenCalledWith(10, 7);
    expect(await screen.findByText('Скасовано, ресурси повернено.')).toBeInTheDocument();
    expect(await screen.findByText('Черга порожня. Обери будівлю нижче й постав її в чергу.')).toBeInTheDocument();
  });

  it('shows the reason the server refused', async () => {
    const user = userEvent.setup();
    const planets = fakePlanetApi({ enqueue: vi.fn().mockRejectedValue(new ApiError(422, 'planets_resources_short')) });
    renderPage('/planet/build', { auth: signedIn(), planets });

    const card = (await screen.findByRole('heading', { name: 'Адміністративний центр' })).closest('li')!;
    await user.click(within(card).getByRole('button', { name: 'Покращити' }));

    expect(await screen.findByText('Не вистачає ресурсів')).toBeInTheDocument();
  });

  it('shows the loading state, then the error with a retry', async () => {
    const user = userEvent.setup();
    // The header of the frame reads the body too, so the server is down until the player retries.
    let down = true;
    const planet = vi.fn().mockImplementation(() => (down ? Promise.reject(new Error('down')) : Promise.resolve(samplePlanet())));
    renderPage('/planet/build', { auth: signedIn(), planets: fakePlanetApi({ planet }) });

    expect(await screen.findByText('Не вдалося завантажити планету.')).toBeInTheDocument();
    down = false;
    await user.click(screen.getByRole('button', { name: 'Спробувати ще раз' }));

    expect(await screen.findByRole('region', { name: 'Черга будівництва' })).toBeInTheDocument();
  });

  it('says there is nothing to build when the server offers no building', async () => {
    renderPage('/planet/build', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(samplePlanet({ buildings: [] })) }) });

    expect(await screen.findByText('На цій планеті ще немає будівель, які можна збудувати.')).toBeInTheDocument();
  });

  it('shows the loading state while the body is not here yet', async () => {
    renderPage('/planet/build', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockReturnValue(new Promise(() => undefined)) }) });

    expect(await screen.findByRole('status')).toHaveTextContent('Завантаження');
    expect(screen.queryByRole('region', { name: 'Черга будівництва' })).not.toBeInTheDocument();
  });

  it('draws only the common frame and marks the construction as the current section', async () => {
    const { container } = renderPage('/planet/build', { auth: signedIn() });

    await screen.findByRole('region', { name: 'Черга будівництва' });
    await waitFor(() => expect(container.querySelectorAll('header')).toHaveLength(1));

    expect(container.querySelectorAll('.civ-side')).toHaveLength(1);
    expect(within(container.querySelector<HTMLElement>('.civ-side')!).getByRole('link', { current: 'page' })).toHaveTextContent('Будівництво');
  });

  it('speaks English with the English names of the categories', async () => {
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, 'en');
    renderPage('/planet/build', { auth: signedIn() });

    expect(await screen.findByRole('region', { name: 'Construction queue' })).toBeInTheDocument();
    expect(await screen.findByRole('region', { name: 'Research' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Extraction' })).toBeInTheDocument();
  });
});
