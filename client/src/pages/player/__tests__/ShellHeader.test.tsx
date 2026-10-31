import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeGameApi,
  fakePlanetApi,
  fakeWorldApi,
  renderPage,
  sampleOwnedPlanet,
  samplePlanet,
  sampleRace,
  signedInMe,
} from '../../../test/renderPage';

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * The header once the data of the frame has arrived: the credits, the energy and the sign of the race.
 */
async function loadedHeader(container: HTMLElement) {
  await waitFor(() => expect(container.querySelector('.civ-topbar .civ-counter--credits:not(.is-off)')).not.toBeNull());
  await waitFor(() => expect(container.querySelector('.civ-topbar .civ-counter--energy:not(.is-off)')).not.toBeNull());
  await waitFor(() => expect(container.querySelector('.civ-topbar .nick .glyph--race-1')).not.toBeNull());

  return container.querySelector<HTMLElement>('.civ-topbar')!;
}

describe('the header of the game has every element of the design (task 00020, section 7.8)', () => {
  it('shows the logo, the nickname with the sign of the race, the plan, the counters, the alerts and the buttons', async () => {
    const { container } = renderPage('/planet', { auth: signedIn() });
    const header = await loadedHeader(container);

    expect(within(header).getByText('AETHERIS')).toBeInTheDocument();
    expect(within(header).getByRole('link', { name: 'AETHERIS — на головну' })).toHaveAttribute('href', '/');

    // The nickname with the sign of the race; the name of the race is for a screen reader.
    const nick = header.querySelector<HTMLElement>('.nick')!;
    expect(nick).toHaveTextContent('Pilot');
    expect(nick).toHaveTextContent(', раса Терани');
    expect(nick.querySelector('.glyph--race-1')).not.toBeNull();

    // The plan: no data on the server, so a dash.
    expect(header.querySelector('.civ-tariff')).toHaveTextContent('Тариф: —');

    // The counters: the credits are real, the trade currency is a dash, the energy is the balance of the body.
    const counters = within(within(header).getByRole('list', { name: 'Ресурси' }));
    expect(counters.getAllByRole('listitem')).toHaveLength(3);
    expect(header.querySelector('.civ-counter--credits')).toHaveTextContent('Кредити:234');
    expect(header.querySelector('.civ-counter--trade')).toHaveTextContent('Торгова валюта:—');
    expect(header.querySelector('.civ-counter--energy')).toHaveTextContent('Енергія:5');
    expect(header.querySelector('.civ-counter--energy')).toHaveAttribute('title', 'Виробіток 10 / споживання 5');

    // The three alerts.
    expect(within(header).getByRole('button', { name: 'Атака' })).toBeDisabled();
    expect(within(header).getByRole('button', { name: 'Бій' })).toBeDisabled();
    expect(within(header).getByRole('button', { name: 'Склад повний' })).toBeDisabled();

    // The mail, the settings and the sign-out.
    expect(within(header).getByRole('button', { name: 'Пошта' })).toBeDisabled();
    expect(within(header).getByRole('link', { name: 'Налаштування' })).toHaveAttribute('href', '/account');
    expect(within(header).getByRole('button', { name: 'Вийти' })).toBeEnabled();
  });

  it('shows a placeholder, never a made-up number, for what the server does not have', async () => {
    const { container } = renderPage('/planet', { auth: signedIn() });
    const header = await loadedHeader(container);

    const placeholders = [
      header.querySelector<HTMLElement>('.civ-tariff')!,
      header.querySelector<HTMLElement>('.civ-counter--trade')!,
      within(header).getByRole('button', { name: 'Атака' }),
      within(header).getByRole('button', { name: 'Бій' }),
      within(header).getByRole('button', { name: 'Пошта' }),
    ];

    for (const placeholder of placeholders) {
      expect(placeholder.textContent ?? '').not.toMatch(/\d/);
      // The reason is in the hint.
      expect(placeholder.getAttribute('title')).toMatch(/^Скоро: /);
    }

    const chat = container.querySelector<HTMLInputElement>('.civ-bottombar input')!;
    expect(chat).toBeDisabled();
    expect(chat.value).toBe('');
    expect(chat.getAttribute('title')).toMatch(/^Глобальний чат|з’явиться/);
    expect(within(container.querySelector<HTMLElement>('.civ-bottombar')!).getByRole('button', { name: 'Надіслати' })).toBeDisabled();
  });

  it('draws the sign of the race from the data of the race, not from the number of the race', async () => {
    // A race with the number 7 that names the colour key of the fourth race: the sign follows the data.
    const game = fakeGameApi({ races: vi.fn().mockResolvedValue([{ ...sampleRace(7), colorKey: 'race-4' }]) });
    const auth = fakeAuthApi({
      me: vi.fn().mockResolvedValue(signedInMe({ civilization: { raceId: 7, raceNameKey: 'world.race.race_7.name', planetId: 10, planetName: 'Nova', systemId: 3, sectorId: 1 } })),
    });
    const { container } = renderPage('/planet', { auth, game });

    await waitFor(() => expect(container.querySelector('.civ-topbar .nick .glyph--race-4')).not.toBeNull());
    expect(container.querySelector('.civ-topbar .nick .glyph--race-1')).toBeNull();
  });

  it('draws the sign of another race for a player of another race, and the neutral one for a race without a colour', async () => {
    const second = renderPage('/planet', {
      auth: fakeAuthApi({
        me: vi.fn().mockResolvedValue(signedInMe({ civilization: { raceId: 2, raceNameKey: 'world.race.race_2.name', planetId: 10, planetName: 'Nova', systemId: 3, sectorId: 1 } })),
      }),
    });

    await waitFor(() => expect(second.container.querySelector('.civ-topbar .nick .glyph--race-2')).not.toBeNull());
    second.unmount();

    const game = fakeGameApi({ races: vi.fn().mockResolvedValue([sampleRace(1), { ...sampleRace(5), colorKey: null }]) });
    const neutral = renderPage('/planet', {
      auth: fakeAuthApi({
        me: vi.fn().mockResolvedValue(signedInMe({ civilization: { raceId: 5, raceNameKey: 'world.race.race_5.name', planetId: 10, planetName: 'Nova', systemId: 3, sectorId: 1 } })),
      }),
      game,
    });

    await waitFor(() => expect(game.races).toHaveBeenCalled());
    await waitFor(() => expect(neutral.container.querySelector('.civ-topbar .nick .glyph--neutral')).not.toBeNull());
  });

  it('lights the alert of the full storage from the data of the body and leads to the storage', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(
        samplePlanet({
          resources: [{ resourceTypeId: 1, resourceGroupId: 1, amount: 10000, ratePerHour: 60, capacity: 10000, isFull: true, fullAt: null }],
        }),
      ),
    });
    const { container } = renderPage('/planet', { auth: signedIn(), planets });
    const header = await loadedHeader(container);

    const alert = await within(header).findByRole('link', { name: 'Склад повний' });

    expect(alert).toHaveAttribute('href', '/planet/storage');
  });

  it('signs the player out with the sign-out of the session', async () => {
    const user = userEvent.setup();
    const auth = signedIn();
    const { container } = renderPage('/planet', { auth });
    const header = await loadedHeader(container);

    await user.click(within(header).getByRole('button', { name: 'Вийти' }));

    expect(auth.logout).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(window.location.pathname).not.toBe('/planet'));
  });

  it('opens the settings of the account from the header', async () => {
    const user = userEvent.setup();
    const { container } = renderPage('/planet', { auth: signedIn() });
    const header = await loadedHeader(container);

    await user.click(within(header).getByRole('link', { name: 'Налаштування' }));

    expect(window.location.pathname).toBe('/account');
  });
});

describe('the menu, the tabs of the phone and the bottom bar of the design (task 00020, section 7.8)', () => {
  const menuOrder = ['Огляд', 'Планета', 'Будівництво', 'Склад', 'Виробництво', 'Верф', 'Флоти', 'Карта', 'Дослідження', 'Ринок', 'Рейтинг', 'Бій', 'Клан', 'Пошта', 'Допомога', 'Налаштування'];

  it('lists the sections in the order of the design; a section without a screen is inactive with the reason', async () => {
    const { container } = renderPage('/planet', { auth: signedIn() });
    const menu = await waitFor(() => {
      const found = container.querySelector<HTMLElement>('.civ-side');

      expect(found).not.toBeNull();

      return found!;
    });

    const labels = [...menu.querySelectorAll('.civ-nav__label')].map((label) => label.textContent);
    expect(labels).toEqual(menuOrder);

    const withScreen = ['Огляд', 'Планета', 'Будівництво', 'Склад', 'Дослідження', 'Налаштування'];

    for (const label of menuOrder) {
      if (withScreen.includes(label)) {
        expect(within(menu).getByRole('link', { name: label })).toBeInTheDocument();
      } else {
        const item = within(menu).getByRole('button', { name: label });

        expect(item).toBeDisabled();
        expect(item.getAttribute('title')).toMatch(/^Скоро: /);
      }
    }
  });

  it.each([
    { path: '/game', current: 'Огляд' },
    { path: '/planet', current: 'Планета' },
    { path: '/planet/build', current: 'Будівництво' },
    { path: '/planet/research', current: 'Дослідження' },
    { path: '/planet/storage', current: 'Склад' },
  ])('marks $current as the current section in the menu on $path', async ({ path, current }) => {
    const { container } = renderPage(path, { auth: signedIn() });

    await waitFor(() => expect(container.querySelector('.civ-side')).not.toBeNull());

    const menu = container.querySelector<HTMLElement>('.civ-side')!;

    expect(within(menu).getAllByRole('link', { current: 'page' })).toHaveLength(1);
    expect(within(menu).getByRole('link', { current: 'page' })).toHaveTextContent(current);
  });

  it('opens the construction from the menu and shows no label on the inactive items, which are only inactive', async () => {
    const { container } = renderPage('/planet', { auth: signedIn() });
    const menu = await waitFor(() => {
      const found = container.querySelector<HTMLElement>('.civ-side');

      expect(found).not.toBeNull();

      return found!;
    });

    expect(within(menu).getByRole('link', { name: 'Будівництво' })).toHaveAttribute('href', '/planet/build');
    expect(menu.querySelectorAll('.civ-nav__tag')).toHaveLength(0);
    expect(within(menu).queryByText('Скоро')).toBeNull();
    expect(within(menu).getByRole('button', { name: 'Верф' })).toBeDisabled();
  });

  it('opens every section of the menu on the phone through the tab "More" and marks the current tab', async () => {
    const user = userEvent.setup();
    const { container } = renderPage('/planet/build', { auth: signedIn() });

    await waitFor(() => expect(container.querySelector('.civ-tabbar')).not.toBeNull());

    const tabs = container.querySelector<HTMLElement>('.civ-tabbar')!;

    expect(within(tabs).getByRole('link', { current: 'page' })).toHaveTextContent('Будівництво');
    expect(within(tabs).getByRole('button', { name: 'Карта' })).toBeDisabled();

    await user.click(within(tabs).getByRole('button', { name: 'Ще' }));

    const more = container.querySelector<HTMLElement>('.civ-more')!;

    expect(within(more).getByRole('link', { name: 'Склад' })).toHaveAttribute('href', '/planet/storage');
    expect(within(more).getByRole('link', { name: 'Дослідження' })).toHaveAttribute('href', '/planet/research');
    expect(within(more).getByRole('link', { current: 'page' })).toHaveTextContent('Будівництво');
  });

  it('switches the planets with the thumbnails and outlines the current one', async () => {
    const user = userEvent.setup();
    const world = fakeWorldApi({
      planets: vi.fn().mockResolvedValue([sampleOwnedPlanet({ planetId: 10, name: 'Nova' }), sampleOwnedPlanet({ planetId: 11, name: 'Vega' })]),
    });
    const planets = fakePlanetApi({ planet: vi.fn().mockImplementation(async (planetId: number) => samplePlanet({ planetId, name: planetId === 11 ? 'Vega' : 'Nova' })) });
    const { container } = renderPage('/planet', { auth: signedIn(), world, planets });

    const footer = await waitFor(() => {
      const found = container.querySelector<HTMLElement>('.civ-bottombar');

      expect(found?.querySelectorAll('.civ-thumb')).toHaveLength(2);

      return found!;
    });
    const group = within(footer).getByRole('group', { name: 'Перемикач планет' });

    expect(within(group).getByRole('button', { name: 'Nova' })).toHaveAttribute('aria-pressed', 'true');
    expect(within(group).getByRole('button', { name: 'Vega' })).toHaveAttribute('aria-pressed', 'false');
    expect(footer.querySelector('.civ-current strong')).toHaveTextContent('Nova');

    await user.click(within(group).getByRole('button', { name: 'Vega' }));

    expect(within(group).getByRole('button', { name: 'Vega' })).toHaveAttribute('aria-pressed', 'true');
    expect(footer.querySelector('.civ-current strong')).toHaveTextContent('Vega');
    // The screen of the body follows the chosen body.
    expect(await screen.findByRole('heading', { name: /Vega/ })).toBeInTheDocument();
    expect(planets.planet).toHaveBeenCalledWith(11);
  });
});
