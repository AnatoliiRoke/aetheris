import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import {
  fakeAuthApi,
  fakeFleetApi,
  renderPage,
  sampleFleet,
  sampleFleetShip,
  sampleFleetsView,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * A fleet of three ships of two projects, and four free ships of two projects, all on the home body.
 * Two of the free ships share a project, so the screen shows them as one line with a number.
 */
const richView = () =>
  sampleFleetsView({
    limit: 3,
    used: 1,
    fleets: [
      sampleFleet({
        id: 1,
        crew: 24,
        upkeepPerHour: 4,
        ships: [
          sampleFleetShip({ id: 1, designId: 7, name: 'Barracuda', fleetId: 1 }),
          sampleFleetShip({ id: 2, designId: 7, name: 'Barracuda', fleetId: 1 }),
          sampleFleetShip({ id: 5, designId: 8, name: 'Piranha', fleetId: 1 }),
        ],
      }),
    ],
    freeShips: [
      sampleFleetShip({ id: 3, designId: 9, name: 'Marlin' }),
      sampleFleetShip({ id: 4, designId: 9, name: 'Marlin' }),
      sampleFleetShip({ id: 6, designId: 10, name: 'Sailfish' }),
    ],
  });

const apis = (view = richView()) => {
  const fleet = fakeFleetApi({ fleets: vi.fn().mockResolvedValue(view) });

  return { auth: signedIn(), fleet };
};

/**
 * Types a number into the field of a project in an open window.
 */
async function quantity(user: ReturnType<typeof userEvent.setup>, dialog: HTMLElement, project: string, value: number) {
  const field = within(dialog).getByLabelText(`Кількість кораблів проєкту «${project}»`);

  await user.clear(field);
  await user.type(field, String(value));
}

describe('the fleets screen (task 00040)', () => {
  it('counts the fleets of the player against the limit of the spaceports', async () => {
    renderPage('/fleets', apis());

    expect(await screen.findByText('Флоти: 1 з 3')).toBeInTheDocument();
  });

  it('shows a fleet with its home body, its place, its ships and its upkeep', async () => {
    renderPage('/fleets', apis());

    const card = await screen.findByRole('button', { name: /Перший ескадрон/ });

    expect(within(card).getByText('Nova')).toBeInTheDocument();
    expect(within(card).getByText(/На орбіті:/)).toBeInTheDocument();
    expect(within(card).getByText('3 кор.')).toBeInTheDocument();
    expect(within(card).getByText('Легкі ×3')).toBeInTheDocument();
    expect(within(card).getByText(/Без командира/)).toBeInTheDocument();
    expect(within(card).getByText('4 кр./год')).toBeInTheDocument();
    expect(within(card).getByText('Готовий')).toBeInTheDocument();
  });

  it('shows the panel of the fleet with its ships by class and project, not one by one', async () => {
    renderPage('/fleets', apis());

    const panel = await screen.findByRole('complementary', { name: 'Деталі флоту' });
    const ships = within(panel).getByRole('heading', { name: 'Кораблі' }).closest('section')!;

    expect(within(ships).getAllByText('Легкі')).toHaveLength(1);
    expect(within(ships).getAllByText('Barracuda')).toHaveLength(1);
    expect(within(ships).getByText('× 2')).toBeInTheDocument();
    expect(within(ships).getByText('Piranha')).toBeInTheDocument();
    expect(within(ships).getByText('× 1')).toBeInTheDocument();
  });

  it('shows what the fleet carries and deals, from the answer of the server', async () => {
    renderPage('/fleets', apis());

    const panel = await screen.findByRole('complementary', { name: 'Деталі флоту' });

    expect(within(panel).getByText('1 200 т')).toBeInTheDocument();
    expect(within(panel).getByText('264 енерг. · 420 кінет.')).toBeInTheDocument();
    expect(within(panel).getByText('15 300')).toBeInTheDocument();
    expect(within(panel).getByText('3 200')).toBeInTheDocument();
  });

  it('shows the commander and the orders switched off with the reason', async () => {
    renderPage('/fleets', apis());

    const panel = await screen.findByRole('complementary', { name: 'Деталі флоту' });

    expect(within(panel).getByRole('button', { name: 'Призначити командира' })).toBeDisabled();
    expect(within(panel).getByText('Скоро: командири з’являться пізніше', { selector: 'p' })).toBeInTheDocument();

    for (const order of ['Летіти', 'Видобувати', 'Атакувати', 'Ескортувати', 'Колонізувати', 'Додому']) {
      expect(within(panel).getByRole('button', { name: order })).toBeDisabled();
    }

    expect(within(panel).getByText('Польоти з’являться пізніше', { selector: 'p' })).toBeInTheDocument();
    expect(within(panel).getByRole('link', { name: 'Показати на карті' })).toHaveAttribute('href', '/map?sector=1&system=3&object=10');
  });

  it('lists the free ships by class and project with a number, and no ship alone', async () => {
    renderPage('/fleets', apis());

    const panel = (await screen.findByRole('heading', { name: 'Вільні кораблі' })).closest('section')!;

    expect(within(panel).getAllByText('Marlin')).toHaveLength(1);
    expect(within(panel).getByText('× 2')).toBeInTheDocument();
    expect(within(panel).getByText('Sailfish')).toBeInTheDocument();
    expect(within(panel).getByText('3 вільних')).toBeInTheDocument();
    expect(within(panel).queryByRole('checkbox')).not.toBeInTheDocument();
  });

  it('names the state without credits and filters by it', async () => {
    const user = userEvent.setup();

    renderPage('/fleets', apis(sampleFleetsView({ hasCredits: false, fleets: [sampleFleet()] })));

    const card = await screen.findByRole('button', { name: /Перший ескадрон/ });

    expect(within(card).getByText('Немає кредитів: політ неможливий')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Готові' }));

    expect(screen.queryByRole('button', { name: /Перший ескадрон/ })).not.toBeInTheDocument();
    expect(screen.getByText('Немає флотів за вашими фільтрами.')).toBeInTheDocument();
  });

  it('finds a fleet by its name', async () => {
    const user = userEvent.setup();

    renderPage(
      '/fleets',
      apis(
        sampleFleetsView({
          fleets: [sampleFleet({ id: 1, name: 'Перший' }), sampleFleet({ id: 2, name: 'Другий', ships: [sampleFleetShip({ id: 9, fleetId: 2 })] })],
        }),
      ),
    );

    await user.type(await screen.findByRole('searchbox', { name: 'Пошук за назвою флоту' }), 'друг');

    expect(screen.getByRole('button', { name: /Другий/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Перший/ })).not.toBeInTheDocument();
  });

  it('forms a fleet of the number of ships the player types for each project', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({ fleets: vi.fn().mockResolvedValue(richView()), createFleet: vi.fn().mockResolvedValue(richView()) });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('button', { name: 'Створити флот' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити флот' });
    const create = within(dialog).getByRole('button', { name: 'Створити' });

    expect(create).toBeDisabled();

    await user.type(within(dialog).getByLabelText('Назва флоту'), 'Другий');

    expect(create).toBeDisabled();

    await quantity(user, dialog, 'Marlin', 2);
    await quantity(user, dialog, 'Sailfish', 1);

    expect(create).toBeEnabled();

    await user.click(create);

    expect(fleet.createFleet).toHaveBeenCalledWith({ planetId: 10, name: 'Другий', shipIds: [3, 4, 6] });
    expect(await screen.findByText('Флот створено.')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('does not let the player type more ships than the project has', async () => {
    const user = userEvent.setup();

    renderPage('/fleets', apis());

    await user.click(await screen.findByRole('button', { name: 'Створити флот' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити флот' });

    await quantity(user, dialog, 'Marlin', 9);

    expect(within(dialog).getByLabelText('Кількість кораблів проєкту «Marlin»')).toHaveValue(2);
    expect(within(dialog).getByText('з 2')).toBeInTheDocument();
  });

  it('switches the button off with the reason when the limit is reached', async () => {
    renderPage('/fleets', apis(sampleFleetsView({ limit: 1, used: 1, freeShips: [sampleFleetShip({ id: 3 })] })));

    const button = await screen.findByRole('button', { name: 'Створити флот' });

    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', 'Ліміт флотів вичерпано');
    expect(screen.getByText('Ліміт флотів вичерпано', { selector: 'span' })).toBeInTheDocument();
  });

  it('shows the reason of a refusal of the server in the window', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(richView()),
      createFleet: vi.fn().mockRejectedValue(new ApiError(422, 'fleet_fleet_limit_reached')),
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('button', { name: 'Створити флот' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити флот' });

    await user.type(within(dialog).getByLabelText('Назва флоту'), 'Третій');
    await quantity(user, dialog, 'Marlin', 1);
    await user.click(within(dialog).getByRole('button', { name: 'Створити' }));

    expect(await within(dialog).findByText('Ліміт флотів вичерпано: його задає рівень Космопорту')).toBeInTheDocument();
  });

  it('adds free ships to the fleet by project and number', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({ fleets: vi.fn().mockResolvedValue(richView()), addFleetShips: vi.fn().mockResolvedValue(richView()) });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('button', { name: 'Додати кораблі' }));

    const dialog = await screen.findByRole('dialog');
    const add = within(dialog).getByRole('button', { name: 'Додати' });

    expect(add).toBeDisabled();

    await quantity(user, dialog, 'Marlin', 1);
    await user.click(add);

    expect(fleet.addFleetShips).toHaveBeenCalledWith({ fleetId: 1, shipIds: [3] });
    expect(await screen.findByText('Кораблі додано до флоту.')).toBeInTheDocument();
  });

  it('adds the free ships of a body to the fleet of that body, asking which fleet when there are several', async () => {
    const user = userEvent.setup();
    const view = richView();
    const two = {
      ...view,
      used: 2,
      fleets: [...view.fleets, sampleFleet({ id: 2, name: 'Другий', ships: [sampleFleetShip({ id: 8, designId: 11, fleetId: 2 })] })],
    };
    const fleet = fakeFleetApi({ fleets: vi.fn().mockResolvedValue(two), addFleetShips: vi.fn().mockResolvedValue(two) });

    renderPage('/fleets', { auth: signedIn(), fleet });

    const panel = (await screen.findByRole('heading', { name: 'Вільні кораблі' })).closest('section')!;

    await user.click(within(panel).getByRole('button', { name: 'Додати до флоту…' }));

    const dialog = await screen.findByRole('dialog', { name: 'Додати кораблі до флоту' });
    const add = within(dialog).getByRole('button', { name: 'Додати' });

    await quantity(user, dialog, 'Sailfish', 1);

    expect(add).toBeDisabled();

    await user.click(within(dialog).getByRole('radio', { name: /Другий/ }));
    await user.click(add);

    expect(fleet.addFleetShips).toHaveBeenCalledWith({ fleetId: 2, shipIds: [6] });
  });

  it('switches the button of a body without a fleet off with the reason', async () => {
    renderPage('/fleets', apis(sampleFleetsView({ used: 0, fleets: [], freeShips: [sampleFleetShip({ id: 3, designId: 9, name: 'Marlin' })] })));

    const panel = (await screen.findByRole('heading', { name: 'Вільні кораблі' })).closest('section')!;
    const button = within(panel).getByRole('button', { name: 'Додати до флоту…' });

    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', 'На цій планеті ще немає флоту');
  });

  it('takes ships out of the fleet by project and number', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({ fleets: vi.fn().mockResolvedValue(richView()), removeFleetShips: vi.fn().mockResolvedValue(richView()) });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('button', { name: 'Вилучити кораблі' }));

    const dialog = await screen.findByRole('dialog', { name: /Вилучити кораблі з флоту/ });

    await quantity(user, dialog, 'Barracuda', 1);
    await user.click(within(dialog).getByRole('button', { name: 'Вилучити' }));

    expect(fleet.removeFleetShips).toHaveBeenCalledWith({ fleetId: 1, shipIds: [1] });
    expect(await screen.findByText('Кораблі вилучено з флоту.')).toBeInTheDocument();
  });

  it('does not let the player take every ship out of a fleet and points to the disbanding', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({ fleets: vi.fn().mockResolvedValue(richView()), removeFleetShips: vi.fn() });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('button', { name: 'Вилучити кораблі' }));

    const dialog = await screen.findByRole('dialog', { name: /Вилучити кораблі з флоту/ });

    await quantity(user, dialog, 'Barracuda', 2);
    await quantity(user, dialog, 'Piranha', 1);

    expect(within(dialog).getByRole('button', { name: 'Вилучити' })).toBeDisabled();
    expect(within(dialog).getByText('Флот має мати хоча б один корабель: щоб вилучити всі, розформуйте флот.')).toBeInTheDocument();
    expect(fleet.removeFleetShips).not.toHaveBeenCalled();
  });

  it('disbands a fleet after the player confirms and leaves the ships on the body', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(richView()),
      dissolveFleet: vi.fn().mockResolvedValue(sampleFleetsView({ used: 0, fleets: [], freeShips: richView().fleets[0].ships })),
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('button', { name: 'Розформувати флот' }));

    const dialog = await screen.findByRole('dialog', { name: 'Розформувати флот?' });

    expect(within(dialog).getByText('Перший ескадрон')).toBeInTheDocument();
    expect(within(dialog).getByText('Кораблі залишаться на планеті як вільні.')).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Розформувати флот' }));

    expect(fleet.dissolveFleet).toHaveBeenCalledWith(1);
    expect(await screen.findByText('Створіть свій перший флот')).toBeInTheDocument();
  });

  it('moves ships to another fleet on the same body by project and number', async () => {
    const user = userEvent.setup();
    const view = sampleFleetsView({
      limit: 3,
      used: 2,
      fleets: [
        sampleFleet({
          id: 1,
          name: 'Перший',
          ships: [sampleFleetShip({ id: 1, designId: 7, name: 'Barracuda', fleetId: 1 }), sampleFleetShip({ id: 2, designId: 7, name: 'Barracuda', fleetId: 1 })],
        }),
        sampleFleet({ id: 2, name: 'Другий', ships: [sampleFleetShip({ id: 5, designId: 12, name: 'Tuna', fleetId: 2 })] }),
      ],
    });
    const fleet = fakeFleetApi({ fleets: vi.fn().mockResolvedValue(view), transferFleetShips: vi.fn().mockResolvedValue(view) });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('button', { name: 'Передати в інший флот' }));

    const dialog = await screen.findByRole('dialog', { name: /Передати кораблі в інший флот/ });

    // One other fleet stands on the body, so the window does not ask where the ships go.
    expect(within(dialog).queryByRole('radio')).not.toBeInTheDocument();

    await quantity(user, dialog, 'Barracuda', 1);
    await user.click(within(dialog).getByRole('button', { name: 'Передати' }));

    expect(fleet.transferFleetShips).toHaveBeenCalledWith({ fleetId: 1, targetFleetId: 2, shipIds: [1] });
    expect(await screen.findByText('Кораблі передано в інший флот.')).toBeInTheDocument();
  });

  it('switches the moving off when no other fleet stands on the body', async () => {
    renderPage('/fleets', apis());

    expect(await screen.findByRole('button', { name: 'Передати в інший флот' })).toBeDisabled();
  });

  it('renames the fleet', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({ fleets: vi.fn().mockResolvedValue(richView()), renameFleet: vi.fn().mockResolvedValue(richView()) });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('button', { name: 'Перейменувати флот' }));

    const field = screen.getByLabelText('Назва флоту');

    await user.clear(field);
    await user.type(field, 'Авангард');
    await user.click(screen.getByRole('button', { name: 'Зберегти' }));

    expect(fleet.renameFleet).toHaveBeenCalledWith({ fleetId: 1, name: 'Авангард' });
    expect(await screen.findByText('Назву змінено.')).toBeInTheDocument();
  });

  it('sends a player without ships to the shipyard', async () => {
    renderPage('/fleets', apis(sampleFleetsView({ used: 0, fleets: [], freeShips: [] })));

    expect(await screen.findByText('Зберіть корабель на верфі')).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: 'Верф' }).some((link) => link.getAttribute('href') === '/shipyard')).toBe(true);
  });

  it('offers the first fleet when there are free ships and no fleet', async () => {
    renderPage('/fleets', apis(sampleFleetsView({ used: 0, fleets: [], freeShips: [sampleFleetShip({ id: 3, name: 'Marlin' })] })));

    expect(await screen.findByText('Створіть свій перший флот')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Створити перший флот' })).toBeEnabled();
  });

  it('is an active item of the menu and no longer a placeholder', async () => {
    const { container } = renderPage('/fleets', apis());

    await screen.findByRole('heading', { name: 'Флоти', level: 1 });

    const menu = container.querySelector<HTMLElement>('.civ-side')!;

    expect(within(menu).getByRole('link', { name: 'Флоти' })).toHaveAttribute('href', '/fleets');
    expect(within(menu).getByRole('link', { name: 'Флоти' })).toHaveAttribute('aria-current', 'page');
  });
});
