import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, fakeMapApi, renderPage, sampleMapGalaxy, sampleMapSector, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });


describe('the map screen (task 00034)', () => {
  it('opens from the menu on the sector of the body and draws the stars, the nebula, the radar and the layers', async () => {
    const map = fakeMapApi();
    renderPage('/map', { auth: signedIn(), map });

    // A visit without a place shows the sector of the body the player looks from.
    await waitFor(() => expect(window.location.search).toContain('sector=1'));
    expect(await screen.findByRole('application', { name: /Карта\./ })).toBeInTheDocument();
    expect(map.sector).toHaveBeenCalledWith(1, 10);

    expect(await screen.findByRole('button', { name: /Lyra.*Зоряна система.*Моє/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Vega\. Зоряна система/ })).toBeInTheDocument();
    // The stars the player only knows of have no name.
    expect(screen.getAllByRole('button', { name: /Невідома зірка.*Невідомо: потрібен радар/ }).length).toBeGreaterThan(0);
    expect(screen.getByText(/Туманність Arena/)).toBeInTheDocument();
    expect(document.querySelector('.map-radar')).not.toBeNull();
    expect(document.querySelector('.map-nebula')).not.toBeNull();

    // The six layers work, the four that wait for fleets and clans are off with a reason.
    const layers = screen.getByRole('region', { name: 'Шари' });
    await userEvent.setup().click(within(layers).getByRole('button', { name: /Шари/ }));
    expect(within(layers).getByRole('checkbox', { name: 'Мої' })).toBeChecked();
    expect(within(layers).getByRole('checkbox', { name: 'Флоти' })).toBeDisabled();
    expect(within(layers).getByRole('checkbox', { name: 'Пірати' })).toBeDisabled();
  });

  it('switches a layer off and draws the stars of that layer no more', async () => {
    const user = userEvent.setup();
    renderPage('/map?sector=1', { auth: signedIn() });

    await screen.findByRole('button', { name: /Lyra.*Моє/ });
    await user.click(within(screen.getByRole('region', { name: 'Шари' })).getByRole('button', { name: /Шари/ }));
    await user.click(within(screen.getByRole('region', { name: 'Шари' })).getByRole('checkbox', { name: 'Мої' }));

    expect(screen.queryByRole('button', { name: /Lyra.*Моє/ })).toBeNull();
    expect(screen.getByRole('button', { name: /^Vega\./ })).toBeInTheDocument();
  });

  it('opens the card of a star with the distance and the time of the flight and the actions that need a fleet off', async () => {
    const user = userEvent.setup();
    renderPage('/map?sector=1', { auth: signedIn() });

    await user.click(await screen.findByRole('button', { name: /^Vega\./ }));

    const card = await screen.findByRole('complementary', { name: 'Картка об’єкта' });
    expect(within(card).getByRole('heading', { name: 'Vega' })).toBeInTheDocument();
    expect(within(card).getByText(/230 св\. р\./)).toBeInTheDocument();
    expect(within(card).getByText(/~2:18:00/)).toBeInTheDocument();
    // The actions that need a fleet are finished buttons that are off, with the reason.
    for (const name of ['Відправити флот', 'Колонізувати', 'Видобувати', 'Розвідати', 'Атакувати', 'Супровід']) {
      expect(within(card).getByRole('button', { name })).toBeDisabled();
    }
    expect(within(card).getByText('Потрібен флот: флоти з’являться пізніше')).toBeInTheDocument();
  });

  it('refuses to open a star the player only knows of', async () => {
    const user = userEvent.setup();
    renderPage('/map?sector=1', { auth: signedIn() });

    await user.click((await screen.findAllByRole('button', { name: /Невідома зірка/ }))[0]!);

    const card = await screen.findByRole('complementary', { name: 'Картка об’єкта' });
    expect(within(card).getByRole('button', { name: 'Відкрити систему' })).toBeDisabled();
    expect(within(card).getByText('Невідомо: потрібен радар')).toBeInTheDocument();
  });

  it('goes down into the system, draws the orbits and the objects and goes back by the breadcrumbs', async () => {
    const user = userEvent.setup();
    const map = fakeMapApi();
    renderPage('/map?sector=1', { auth: signedIn(), map });

    await user.click(await screen.findByRole('button', { name: /Lyra.*Моє/ }));
    await user.click(within(await screen.findByRole('complementary', { name: 'Картка об’єкта' })).getByRole('button', { name: 'Відкрити систему' }));

    expect(await screen.findByRole('button', { name: /Nova.*Моє/ })).toBeInTheDocument();
    expect(map.system).toHaveBeenCalledWith(3, 10);
    expect(window.location.search).toContain('system=3');
    expect(document.querySelectorAll('.map-orbit')).toHaveLength(12);
    expect(screen.getByRole('button', { name: /Lyra-3.*Вільна/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Icar.*Власник Krell_77/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Невідомий об’єкт, орбіта 9/ })).toBeInTheDocument();

    const crumbs = screen.getByRole('navigation', { name: 'Навігація сторінкою' });
    expect(within(crumbs).getByRole('link', { name: 'Галактика' })).toHaveAttribute('href', '/map?level=galaxy');
    expect(within(crumbs).getByRole('link', { name: 'Сектор Aldara' })).toHaveAttribute('href', '/map?sector=1');
  });

  it('opens the planet of the player from the card of their body, and shows who owns another one', async () => {
    const user = userEvent.setup();
    renderPage('/map?sector=1&system=3', { auth: signedIn() });

    await user.click(await screen.findByRole('button', { name: /Icar/ }));

    const other = await screen.findByRole('complementary', { name: 'Картка об’єкта' });
    expect(within(other).getByText('Krell_77')).toBeInTheDocument();
    expect(within(other).queryByRole('button', { name: 'Відкрити планету' })).toBeNull();

    await user.click(screen.getByRole('button', { name: /Nova.*Моє/ }));
    await user.click(within(await screen.findByRole('complementary', { name: 'Картка об’єкта' })).getByRole('button', { name: 'Відкрити планету' }));

    await waitFor(() => expect(window.location.pathname).toBe('/planet'));
  });

  it('shows the galaxy with the sectors, the badge of the newcomers and the bodies of the player', async () => {
    const user = userEvent.setup();
    renderPage('/map?level=galaxy', { auth: signedIn() });

    const sector = await screen.findByRole('button', { name: /Сектор Aldara.*Новачки.*Мої планети: 1/ });
    expect(screen.getByRole('button', { name: /Сектор Borea.*Систем: 6/ })).toBeInTheDocument();

    await user.click(sector);
    await user.click(within(await screen.findByRole('complementary', { name: 'Картка об’єкта' })).getByRole('button', { name: 'Відкрити сектор' }));

    await waitFor(() => expect(window.location.search).toContain('sector=1'));
  });

  it('shows the same places as a table and goes back to the map with the place chosen', async () => {
    const user = userEvent.setup();
    renderPage('/map?sector=1&view=list', { auth: signedIn() });

    const table = await screen.findByRole('table', { name: 'Об’єкти поточного рівня карти' });
    expect(within(table).getByRole('rowheader', { name: /Lyra/ })).toBeInTheDocument();
    // The stars the player only knows of are not in the table.
    expect(within(table).queryByText('Невідома зірка')).toBeNull();

    await user.click(within(within(table).getAllByRole('row')[2]!).getByRole('button', { name: 'На карті' }));

    await waitFor(() => expect(window.location.search).not.toContain('view=list'));
    expect(await screen.findByRole('application', { name: /Карта\./ })).toBeInTheDocument();
  });

  it('points at a target with the keyboard: the point snaps through the server, the card shows its distance and the route is drawn', async () => {
    const user = userEvent.setup();
    const map = fakeMapApi();
    renderPage('/map?sector=1', { auth: signedIn(), map });

    const canvas = await screen.findByRole('application', { name: /Карта\./ });
    await user.click(screen.getByRole('button', { name: 'Обрати ціль польоту' }));
    expect(screen.getByRole('button', { name: 'Обрати ціль польоту' })).toHaveAttribute('aria-pressed', 'true');

    canvas.focus();
    await user.keyboard('{Enter}');

    await waitFor(() => expect(map.target).toHaveBeenCalledWith(1, 500, 500, 0, 10));
    const card = await screen.findByRole('complementary', { name: 'Картка об’єкта' });
    expect(within(card).getByRole('heading', { name: 'Точка в просторі' })).toBeInTheDocument();
    expect(within(card).getByText(/420,5 св\. р\./)).toBeInTheDocument();
    expect(within(card).getByText('Невідомо: потрібен радар')).toBeInTheDocument();
    expect(within(card).getByRole('button', { name: 'Відправити флот' })).toBeDisabled();
    expect(window.location.search).toContain('target=point%3A1%3A500%3A500%3A0');
    expect(screen.getByRole('img', { name: 'Ціль польоту' })).toBeInTheDocument();

    await user.click(within(card).getByRole('button', { name: 'Скасувати ціль' }));
    expect(window.location.search).not.toContain('target=');
  });

  it('restores a target from the address', async () => {
    renderPage('/map?sector=1&target=system%3A4', { auth: signedIn() });

    expect(await screen.findByRole('img', { name: 'Ціль польоту' })).toBeInTheDocument();
  });

  it('tells a player who has no radar that they see only their own planet', async () => {
    const map = fakeMapApi({
      galaxy: vi.fn().mockResolvedValue(sampleMapGalaxy({ seesOnlyOwn: true })),
      sector: vi.fn().mockResolvedValue(sampleMapSector({ seesOnlyOwn: true, radars: [] })),
    });
    renderPage('/map?sector=1', { auth: signedIn(), map });

    expect(await screen.findByText(/Ви бачите лише свою планету/)).toBeInTheDocument();
    expect(within(screen.getByText(/Ви бачите лише свою планету/).parentElement!).getByRole('link', { name: 'Будівництво' })).toHaveAttribute('href', '/planet/build');
    expect(document.querySelector('.map-radar')).toBeNull();
  });

  it('says that the map could not be loaded and reads it again', async () => {
    const user = userEvent.setup();
    const map = fakeMapApi({ sector: vi.fn().mockRejectedValueOnce(new Error('down')).mockResolvedValue(sampleMapSector()) });
    renderPage('/map?sector=1', { auth: signedIn(), map });

    expect(await screen.findByText('Не вдалося завантажити карту.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Спробувати ще раз' }));

    expect(await screen.findByRole('button', { name: /Lyra.*Моє/ })).toBeInTheDocument();
  });

  it('finds a place by a part of the name, asks the server once the player stops typing, and goes there', async () => {
    const user = userEvent.setup();
    const map = fakeMapApi();
    renderPage('/map?sector=1', { auth: signedIn(), map });

    await screen.findByRole('application', { name: /Карта\./ });
    await user.type(screen.getByRole('searchbox', { name: 'Пошук на карті' }), 'Ly');

    const result = await screen.findByRole('option', { name: /Lyra/ }, { timeout: 2000 });
    expect(map.search).toHaveBeenCalledTimes(1);
    expect(map.search).toHaveBeenCalledWith('Ly');

    await user.click(result);
    await waitFor(() => expect(window.location.search).toContain('system=3'));
  });

  it('places the marks by numbers of the stylesheet and does not scale them, so they stay sharp at any zoom', async () => {
    const user = userEvent.setup();
    renderPage('/map?sector=1', { auth: signedIn() });

    await screen.findByRole('button', { name: /Lyra.*Моє/ });
    const world = document.querySelector<HTMLElement>('.map-world')!;
    const before = world.style.getPropertyValue('--k');

    await user.click(screen.getByRole('button', { name: 'Збільшити' }));

    expect(Number(world.style.getPropertyValue('--k'))).toBeGreaterThan(Number(before));
    expect(world.style.getPropertyValue('--tx')).toMatch(/px$/);
    // Nothing is scaled as a bitmap: the world has no transform, so the text and the pictures stay sharp.
    expect(world.style.transform).toBe('');
  });
});
