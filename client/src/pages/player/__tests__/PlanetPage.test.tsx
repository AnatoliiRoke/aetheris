import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, fakeEconomyApi, fakePlanetApi, fakeWorldApi, renderPage, sampleBalance, samplePlanet, sampleWorldReference, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('PlanetPage', () => {
  it('shows the buildings, the queue, the balance, the storage and the energy', async () => {
    const planets = fakePlanetApi();
    renderPage('/planet', { auth: signedIn(), planets });

    expect(await screen.findByRole('heading', { name: 'Планета Nova' })).toBeInTheDocument();
    // The heading comes from the account, the planet request from a passive effect that may
    // run after the heading is already on the page.
    await waitFor(() => expect(planets.planet).toHaveBeenCalledWith(10));

    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });
    expect(within(queue).getByText('Шахта заліза, рівень 1')).toBeInTheDocument();
    expect(within(queue).getByText('Місць: 1 з 2')).toBeInTheDocument();
    expect(within(queue).getByRole('timer')).toBeInTheDocument();

    const resources = screen.getByRole('region', { name: 'Ресурси' });
    expect(within(resources).getByText('Залізо')).toBeInTheDocument();
    expect(within(resources).getByText('+60')).toBeInTheDocument();

    const energy = screen.getByRole('region', { name: 'Енергія' });
    expect(within(energy).getByText('Баланс після черги')).toBeInTheDocument();
  });

  it('warns which resources are full', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(
        samplePlanet({
          resources: [
            { resourceTypeId: 1, resourceGroupId: 1, amount: 1400, ratePerHour: 0, capacity: 1400, isFull: true, fullAt: '2026-10-26T11:00:00Z' },
            { resourceTypeId: 3, resourceGroupId: 1, amount: 500, ratePerHour: 0, capacity: 1400, isFull: false, fullAt: null },
          ],
        }),
      ),
    });
    renderPage('/planet', { auth: signedIn(), planets });

    expect(await screen.findByText('Склад заповнений, видобуток зупинено: Залізо')).toBeInTheDocument();
  });

  it('shows the finances of the civilization between the energy and the population', async () => {
    const economy = fakeEconomyApi({
      overview: vi.fn().mockResolvedValue({
        populations: [],
        balances: [sampleBalance({ resourceTypeId: 27, amount: 1200, incomePerHour: 45 })],
        gameNow: '2026-10-26T12:00:00Z',
      }),
    });
    const world = fakeWorldApi({
      reference: vi.fn().mockResolvedValue(
        sampleWorldReference({
          resources: [
            { id: 27, code: 'resource_27', nameKey: 'world.resource.resource_27.name', descriptionKey: '', groupCode: null, groupId: null, imageKey: null, earnsPopulationIncome: true },
          ],
        }),
      ),
    });

    renderPage('/planet', { auth: signedIn(), economy, planets: fakePlanetApi(), world });

    const finance = await screen.findByRole('region', { name: 'Фінанси' });

    expect(within(finance).getByText('Баланс')).toBeInTheDocument();
    expect(within(finance).getByText('1 200')).toBeInTheDocument();
    expect(within(finance).getByText('Споживання')).toBeInTheDocument();
    expect(within(finance).getByText('0')).toBeInTheDocument();
    expect(within(finance).getByText('Надходження в годину')).toBeInTheDocument();
    expect(within(finance).getByText('+45')).toBeInTheDocument();

    // The panel of the finances stands between the energy and the population.
    const panels = Array.from(document.querySelectorAll('.planet-grid > .panel'));

    expect(panels.map((panel) => panel.querySelector('.panel__title')?.textContent)).toEqual(['Енергія', 'Фінанси', 'Населення']);
  });
});

describe('GamePage queue', () => {
  it('shows the real construction queue and opens the planet', async () => {
    const user = userEvent.setup();
    renderPage('/game', { auth: signedIn() });

    const queue = await screen.findByRole('region', { name: 'Будівництво' });
    expect(await within(queue).findByText('Шахта заліза, рівень 1')).toBeInTheDocument();

    // The menu has an item of the same name that opens the construction; the card opens the planet.
    const link = screen.getAllByRole('link', { name: 'Будівництво' }).find((element) => element.closest('nav') === null);

    await user.click(link!);
    await waitFor(() => expect(window.location.pathname).toBe('/planet'));
  });

  it('shows in the bar of the storage the resource that is the nearest to its own limit', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(
        samplePlanet({
          resources: [
            { resourceTypeId: 1, resourceGroupId: 1, amount: 900, ratePerHour: 60, capacity: 10000, isFull: false, fullAt: '2026-10-27T12:00:00Z' },
            { resourceTypeId: 3, resourceGroupId: 1, amount: 10000, ratePerHour: 0, capacity: 10000, isFull: true, fullAt: '2026-10-26T11:00:00Z' },
          ],
        }),
      ),
    });
    renderPage('/game', { auth: signedIn(), planets });

    expect(await screen.findByText('Склад · Мідь')).toBeInTheDocument();
    expect(screen.getByText('10 000 / 10 000 · Повний')).toBeInTheDocument();
  });

  it('keeps the construction closed until the e-mail is confirmed', async () => {
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe({ emailConfirmed: false })) });
    renderPage('/game', { auth });

    expect(await screen.findByRole('button', { name: 'Будівництво' })).toBeDisabled();
    expect(screen.queryByRole('region', { name: 'Черга будівництва' })).not.toBeInTheDocument();
  });
});
