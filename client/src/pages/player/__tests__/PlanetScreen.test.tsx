import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LANGUAGE_STORAGE_KEY } from '../../../i18n/I18nProvider';
import { fakeAuthApi, fakePlanetApi, fakeResearchApi, renderPage, samplePlanet, sampleResearchTree, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('PlanetPage: the screen of the planet', () => {
  it('shows the card of the body with the type, the place, the population and the richness', async () => {
    const planets = fakePlanetApi({ planet: vi.fn().mockResolvedValue(samplePlanet({ populationMax: 600 })) });
    renderPage('/planet', { auth: signedIn(), planets });

    const card = await screen.findByRole('region', { name: 'Планета' });

    expect(within(card).getByText('Вулканічна')).toBeInTheDocument();
    expect(within(card).getByText('Сектор 1 · Система 3 · Орбіта 2')).toBeInTheDocument();
    expect(within(card).getByText('310 / 600')).toBeInTheDocument();
    expect(within(card).getByText('1,5×')).toBeInTheDocument();
    // A resource the body has no deposit of shows a dash, never a made-up coefficient.
    expect(within(card).getByText('—')).toBeInTheDocument();
  });

  it('shows the growth of the resources in one block and the storage in another', async () => {
    renderPage('/planet', { auth: signedIn() });

    const resources = await screen.findByRole('region', { name: 'Ресурси' });
    const storage = screen.getByRole('region', { name: 'Склад' });

    expect(within(resources).getByText('+60')).toBeInTheDocument();
    expect(within(resources).getByText('не видобувається')).toBeInTheDocument();
    // The growth block has no amounts and no capacities: those belong to the storage.
    expect(within(resources).queryByText(/10 000/)).not.toBeInTheDocument();

    // Under the name of the group every resource has its own row with the amount against the
    // capacity of the resource, with a thin space between the thousands, and a bar.
    expect(within(storage).getByRole('heading', { name: 'Видобувні' })).toBeInTheDocument();
    expect(within(storage).getByText('900 / 10 000')).toBeInTheDocument();
    // The space between the thousands is the thin space of the design, not an ordinary one.
    expect(storage.textContent).toContain('900 / 10\u2009000');
    expect(within(storage).getByText('500 / 10 000')).toBeInTheDocument();
    expect(within(storage).getByRole('progressbar', { name: 'Залізо' })).toHaveAttribute('aria-valuenow', '9');
    expect(within(storage).getByRole('progressbar', { name: 'Мідь' })).toHaveAttribute('aria-valuenow', '5');
    expect(within(storage).queryByText('+60')).not.toBeInTheDocument();
    expect(within(storage).queryByText('Повний')).not.toBeInTheDocument();
  });

  it('marks only the full resource with the badge and names it in the alarm of the resources block', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(
        samplePlanet({
          resources: [
            { resourceTypeId: 1, resourceGroupId: 1, amount: 4200, ratePerHour: 0, capacity: 4200, isFull: true, fullAt: '2026-10-26T11:00:00Z' },
            { resourceTypeId: 3, resourceGroupId: 1, amount: 1200, ratePerHour: 20, capacity: 4200, isFull: false, fullAt: '2026-10-26T20:00:00Z' },
          ],
        }),
      ),
    });
    renderPage('/planet', { auth: signedIn(), planets });

    const storage = await screen.findByRole('region', { name: 'Склад' });
    const resources = screen.getByRole('region', { name: 'Ресурси' });

    // The iron is full and holds the capacity of the group; the copper of the same group has the same
    // maximum and no badge.
    const iron = within(storage).getByText('Залізо').closest('li')!;
    const copper = within(storage).getByText('Мідь').closest('li')!;

    expect(within(iron).getByText('Повний')).toBeInTheDocument();
    expect(within(iron).getByText('4 200 / 4 200')).toBeInTheDocument();
    expect(within(copper).queryByText('Повний')).not.toBeInTheDocument();
    expect(within(copper).getByText('1 200 / 4 200')).toBeInTheDocument();
    expect(within(storage).getAllByText('Повний')).toHaveLength(1);

    // The alarm is in the resources block and names the resource that stands still, not the group.
    expect(within(resources).getByText('Склад заповнений, видобуток зупинено: Залізо')).toBeInTheDocument();
    expect(within(storage).queryByText(/видобуток зупинено/)).not.toBeInTheDocument();
    expect(within(resources).queryByText('Повний')).not.toBeInTheDocument();
  });

  it('draws no alarm while no resource is full', async () => {
    renderPage('/planet', { auth: signedIn() });

    await screen.findByRole('region', { name: 'Ресурси' });

    expect(screen.queryByText(/видобуток зупинено/)).not.toBeInTheDocument();
  });

  it('shows the energy and the population blocks', async () => {
    renderPage('/planet', { auth: signedIn() });

    expect(await screen.findByRole('region', { name: 'Енергія' })).toBeInTheDocument();
    expect(await screen.findByRole('region', { name: 'Населення' })).toBeInTheDocument();
  });

  it('shows both queues in the standard panels with the way to the choice', async () => {
    const research = fakeResearchApi({
      tree: vi.fn().mockResolvedValue(
        sampleResearchTree({
          queue: [
            {
              id: 3,
              technologyId: 1,
              technologyNameKey: 'research.technology.tech_1.name',
              targetLevel: 1,
              planetId: 10,
              position: 1,
              startsAt: '2026-10-26T12:00:00Z',
              finishesAt: '2026-10-26T12:05:00Z',
              costs: [{ resourceTypeId: 1, amount: 200 }],
            },
          ],
          queueUsedSlots: 1,
        }),
      ),
    });
    renderPage('/planet', { auth: signedIn(), research });

    const building = await screen.findByRole('region', { name: 'Черга будівництва' });
    expect(within(building).getByText('Шахта заліза, рівень 1')).toBeInTheDocument();
    expect(within(building).getByRole('timer')).toBeInTheDocument();
    expect(within(building).getByRole('link', { name: 'Обрати будівлю в чергу' })).toHaveAttribute('href', '/planet/build');

    const researches = await screen.findByRole('region', { name: 'Черга досліджень' });
    expect(await within(researches).findByText('Глибинний видобуток, рівень 1')).toBeInTheDocument();
    expect(within(researches).getByRole('timer')).toBeInTheDocument();
    expect(within(researches).getByRole('link', { name: 'Обрати дослідження' })).toHaveAttribute('href', '/planet/research');
  });

  it('cancels a record of the construction queue through the standard panel', async () => {
    const user = userEvent.setup();
    const planets = fakePlanetApi();
    renderPage('/planet', { auth: signedIn(), planets });

    await user.click(await screen.findByRole('button', { name: /Скасувати: Шахта заліза, рівень 1/ }));

    expect(planets.cancel).toHaveBeenCalledWith(10, 7);
    expect(await screen.findByText('Скасовано, ресурси повернено.')).toBeInTheDocument();
  });

  it('shows the empty queues with the free slots', async () => {
    const planets = fakePlanetApi({ planet: vi.fn().mockResolvedValue(samplePlanet({ queue: { slotCount: 2, items: [] } })) });
    renderPage('/planet', { auth: signedIn(), planets });

    const building = await screen.findByRole('region', { name: 'Черга будівництва' });
    expect(within(building).getByText('Черга порожня.')).toBeInTheDocument();
    expect(within(building).getAllByText('Вільне місце')).toHaveLength(2);

    const researches = await screen.findByRole('region', { name: 'Черга досліджень' });
    expect(await within(researches).findByText('Черга досліджень порожня.')).toBeInTheDocument();
  });

  it('shows the loading state while the body is not here yet', async () => {
    const planets = fakePlanetApi({ planet: vi.fn().mockReturnValue(new Promise(() => undefined)) });
    renderPage('/planet', { auth: signedIn(), planets });

    // The page of the game is here once the heading is; the account loads first with its own wait.
    await screen.findByRole('heading', { name: 'Планета Nova' });

    expect(screen.getByText('Завантаження…')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Черга будівництва' })).not.toBeInTheDocument();
  });

  it('shows the error and reads the body again on retry', async () => {
    const user = userEvent.setup();
    const planet = vi.fn().mockRejectedValue(new Error('down'));
    renderPage('/planet', { auth: signedIn(), planets: fakePlanetApi({ planet }) });

    expect(await screen.findByText('Не вдалося завантажити планету.')).toBeInTheDocument();

    planet.mockResolvedValue(samplePlanet());

    await user.click(screen.getByRole('button', { name: 'Спробувати ще раз' }));

    expect(await screen.findByRole('region', { name: 'Черга будівництва' })).toBeInTheDocument();
  });

  it('says that the research queue did not load and keeps the rest of the screen', async () => {
    const research = fakeResearchApi({ tree: vi.fn().mockRejectedValue(new Error('down')) });
    renderPage('/planet', { auth: signedIn(), research });

    const researches = await screen.findByRole('region', { name: 'Черга досліджень' });

    expect(await within(researches).findByText('Не вдалося завантажити дослідження.')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Черга будівництва' })).toBeInTheDocument();
  });

  it('speaks English when the player chose it', async () => {
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, 'en');
    renderPage('/planet', { auth: signedIn() });

    expect(await screen.findByRole('region', { name: 'Construction queue' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pick a building for the queue' })).toBeInTheDocument();
    expect(await screen.findByRole('region', { name: 'Research queue' })).toBeInTheDocument();
  });

  it('draws only the common frame of the game and no frame of its own', async () => {
    const { container } = renderPage('/planet', { auth: signedIn() });

    await screen.findByRole('region', { name: 'Черга будівництва' });

    expect(container.querySelectorAll('header')).toHaveLength(1);
    expect(container.querySelectorAll('header.civ-topbar')).toHaveLength(1);
    expect(container.querySelectorAll('nav.civ-side')).toHaveLength(1);
    expect(container.querySelectorAll('footer.civ-bottombar')).toHaveLength(1);
    expect(container.querySelectorAll('main')).toHaveLength(1);
    expect(container.querySelector('.player')).toBeNull();

    const menu = container.querySelector('nav.civ-side')!;

    await waitFor(() => expect(within(menu as HTMLElement).getByRole('link', { name: 'Планета' })).toHaveAttribute('aria-current', 'page'));
  });
});
