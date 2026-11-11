import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeFleetApi,
  fakeMapApi,
  renderPage,
  sampleFleet,
  sampleFlightEstimate,
  sampleFlightPlan,
  sampleFleetsView,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * One fleet of the player, so a flight plan may be made for it.
 */
const withFleet = () =>
  sampleFleetsView({
    limit: 3,
    used: 1,
    fleets: [sampleFleet({ id: 1, name: 'Ескадра', ships: [] })],
    freeShips: [],
  });

const apis = (plans: unknown[] = []) => {
  const fleet = fakeFleetApi({
    fleets: vi.fn().mockResolvedValue(withFleet()),
    flightPlans: vi.fn().mockResolvedValue(plans),
  });

  return { auth: signedIn(), fleet };
};

describe('the flight plan of a fleet (task 00050)', () => {
  it('offers the plan of the fleet in the block of the orders', async () => {
    renderPage('/fleets', apis([sampleFlightPlan()]));

    const block = await screen.findByRole('region', { name: 'Політний лист' });

    expect(within(block).getByText('Чернетка')).toBeInTheDocument();
    expect(within(block).getByText('Переліт')).toBeInTheDocument();
  });

  it('offers to create a plan when the fleet has none', async () => {
    renderPage('/fleets', apis([]));

    const block = await screen.findByRole('region', { name: 'Політний лист' });

    expect(within(block).getByText('Політного листа ще немає. Створіть його, щоб флот мав що виконувати.')).toBeInTheDocument();
    expect(within(block).getByRole('button', { name: 'Створити лист' })).toBeInTheDocument();
  });

  it('asks for the kind of the target and shows the coordinates for it', async () => {
    renderPage('/fleets', apis([]));

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));

    // The kind of the target has three choices: the coordinates, a planet and an object.
    const kind = within(dialog).getByLabelText('Ціль');

    expect(within(kind).getByRole('option', { name: 'Координати' })).toBeInTheDocument();
    expect(within(kind).getByRole('option', { name: 'Планета' })).toBeInTheDocument();
    expect(within(kind).getByRole('option', { name: 'Об’єкт' })).toBeInTheDocument();

    // The coordinates are written in a plain text field.
    expect(within(dialog).getByLabelText('Координати')).toBeInTheDocument();
    expect(within(dialog).queryByLabelText('Сектор')).not.toBeInTheDocument();
  });

  it('shows the three fields of a place when the target is a planet', async () => {
    renderPage('/fleets', apis([]));

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getAllByLabelText('Ціль')[0]!, 'planet');

    expect(within(dialog).getByLabelText('Сектор')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Система')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Планета')).toBeInTheDocument();
  });

  it('puts the three fields of a place in one row', async () => {
    renderPage('/fleets', apis([]));

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getAllByLabelText('Ціль')[0]!, 'planet');

    const row = within(dialog).getByLabelText('Сектор').closest('.pl-order__row');

    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByLabelText('Система')).toBeInTheDocument();
    expect(within(row as HTMLElement).getByLabelText('Планета')).toBeInTheDocument();
  });

  it('keeps the field of the planet off until the system is chosen', async () => {
    renderPage('/fleets', apis([]));

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getAllByLabelText('Ціль')[0]!, 'planet');

    // The sector of the fleet is the sector of the target by default (question 13 of task 00050),
    // so the list of the systems of it is already open.
    expect(within(dialog).getByLabelText('Система')).not.toBeDisabled();
    expect(within(dialog).getByLabelText('Планета')).toBeDisabled();

    await userEvent.selectOptions(within(dialog).getByLabelText('Система'), '3');

    expect(within(dialog).getByLabelText('Планета')).not.toBeDisabled();
  });

  it('offers only the sectors the radar shows and the ones the player has explored', async () => {
    renderPage('/fleets', apis([]));

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getAllByLabelText('Ціль')[0]!, 'planet');

    const sector = within(dialog).getByLabelText('Сектор');

    // The galaxy of the test holds one open sector, one known sector and one the player knows
    // nothing of.
    await waitFor(() => expect(within(sector).getAllByRole('option')).toHaveLength(3));
    expect(within(sector).getByRole('option', { name: 'Aldara' })).toBeInTheDocument();
    expect(within(sector).getByRole('option', { name: 'Borea' })).toBeInTheDocument();
  });

  it('offers only the systems a radar of the player reaches', async () => {
    renderPage('/fleets', apis([]));

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getAllByLabelText('Ціль')[0]!, 'planet');
    await userEvent.selectOptions(within(dialog).getByLabelText('Сектор'), '1');

    const system = within(dialog).getByLabelText('Система');

    // The sector of the test holds two systems the radar reaches and two it does not.
    await waitFor(() => expect(within(system).getAllByRole('option')).toHaveLength(3));
    expect(within(system).getByRole('option', { name: 'Lyra' })).toBeInTheDocument();
    expect(within(system).getByRole('option', { name: 'Vega' })).toBeInTheDocument();
  });

  it('offers the planets of the chosen system', async () => {
    renderPage('/fleets', apis([]));

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getAllByLabelText('Ціль')[0]!, 'planet');
    await userEvent.selectOptions(within(dialog).getByLabelText('Сектор'), '1');

    const system = within(dialog).getByLabelText('Система');

    await waitFor(() => expect(within(system).getByRole('option', { name: 'Lyra' })).toBeInTheDocument());
    await userEvent.selectOptions(system, '3');

    const planet = within(dialog).getByLabelText('Планета');

    // The star of the system is not a place to fly to, the planets and the field are.
    await waitFor(() => expect(within(planet).getByRole('option', { name: 'Nova' })).toBeInTheDocument());
    expect(within(planet).queryByRole('option', { name: 'Lyra' })).not.toBeInTheDocument();
  });

  it('writes a plan with the fleet the player chose and the orders of the window', async () => {
    const create = vi.fn().mockResolvedValue(sampleFlightPlan());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([]),
      createFlightPlan: create,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    // The fleet is chosen, and the player may not send the plan without it.
    expect(within(dialog).getByText('Оберіть флот')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Створити лист' })).toBeDisabled();

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Створити лист' }));

    await waitFor(() => expect(create).toHaveBeenCalled());

    const request = create.mock.calls[0]![0];

    expect(request.fleetId).toBe(1);
    expect(request.orders).toHaveLength(1);
    expect(request.orders[0].typeCode).toBe('fly');
  });

  it('does not write a plan when the coordinates of a point are not three numbers', async () => {
    const create = vi.fn().mockResolvedValue(sampleFlightPlan());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([]),
      createFlightPlan: create,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.type(within(dialog).getAllByLabelText('Координати')[0]!, '1 2');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Створити лист' }));

    expect(within(dialog).getByText('Координати мають бути трьома числами.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('asks before the plan is deleted and deletes it when the player confirms', async () => {
    const remove = vi.fn().mockResolvedValue(undefined);
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([sampleFlightPlan()]),
      deleteFlightPlan: remove,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Видалити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Видалити політний лист?' });

    expect(within(dialog).getByText('Флот лишиться без політного листа і нічого не робитиме.')).toBeInTheDocument();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Видалити лист' }));

    await waitFor(() => expect(remove).toHaveBeenCalledWith(1));
  });

  it('carries the plan out with the button of the block and reads the fleets again', async () => {
    const execute = vi.fn().mockResolvedValue(sampleFlightPlan({ stateCode: 'active', currentPosition: 1 }));
    const fleets = vi.fn().mockResolvedValue(withFleet());
    const fleet = fakeFleetApi({
      fleets,
      flightPlans: vi.fn().mockResolvedValue([sampleFlightPlan()]),
      executeFlightPlan: execute,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    const block = await screen.findByRole('region', { name: 'Політний лист' });
    const button = within(block).getByRole('button', { name: 'Виконати' });

    // The plan has an order, so the button is on.
    expect(button).toBeEnabled();

    const readsBefore = fleets.mock.calls.length;

    await userEvent.click(button);

    await waitFor(() => expect(execute).toHaveBeenCalledWith(1));

    // The fleet changed with the plan (it left), so the fleets are read again.
    await waitFor(() => expect(fleets.mock.calls.length).toBeGreaterThan(readsBefore));
  });

  it('keeps the button of carrying out the plan off while the plan is under way or has no orders', async () => {
    renderPage('/fleets', apis([sampleFlightPlan({ stateCode: 'active', currentPosition: 1 })]));

    const running = await screen.findByRole('region', { name: 'Політний лист' });

    expect(within(running).getByRole('button', { name: 'Виконати' })).toBeDisabled();
    expect(within(running).getByRole('button', { name: 'Зупинити' })).toBeEnabled();
  });

  it('keeps the button of carrying out the plan off when the plan has no orders', async () => {
    renderPage('/fleets', apis([sampleFlightPlan({ orders: [] })]));

    const block = await screen.findByRole('region', { name: 'Політний лист' });
    const button = within(block).getByRole('button', { name: 'Виконати' });

    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', 'Додайте хоча б один наказ і оберіть флот.');
  });

  it('writes the orders of the window and carries the plan out with the button of the window', async () => {
    const create = vi.fn().mockResolvedValue(sampleFlightPlan());
    const execute = vi.fn().mockResolvedValue(sampleFlightPlan({ stateCode: 'active', currentPosition: 1 }));
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([]),
      createFlightPlan: create,
      executeFlightPlan: execute,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Виконати' }));

    // The plan of the window is written first, and then the plan that was written is carried out.
    await waitFor(() => expect(execute).toHaveBeenCalledWith(1));
    expect(create).toHaveBeenCalledTimes(1);
    expect(create.mock.invocationCallOrder[0]).toBeLessThan(execute.mock.invocationCallOrder[0]!);
  });

  it('shows the time of the flight that the server counted, not a time of the window', async () => {
    const estimate = vi.fn().mockResolvedValue(sampleFlightEstimate({ currentSeconds: 7200 }));
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([]),
      flightEstimate: estimate,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.type(within(dialog).getAllByLabelText('Координати')[0]!, '10 20 0');

    // The server counts the time: the window only asks for it with the place and shows the answer.
    await waitFor(() =>
      expect(estimate).toHaveBeenCalledWith(
        expect.objectContaining({
          fleetId: 1,
          typeCode: 'fly',
          target: { kind: 'point', sectorId: 1, objectId: 0, x: 10, y: 20, z: 0 },
        }),
      ),
    );
    expect(await within(dialog).findByText('Від 1:01:08 до 3:16:40')).toBeInTheDocument();

    // The fleet carries cargo, so the time with it differs from the empty one and is shown too.
    expect(within(dialog).getByText('2:00:00')).toBeInTheDocument();
  });

  it('shows the time of the return from the server too', async () => {
    const estimate = vi.fn().mockResolvedValue(sampleFlightEstimate());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([]),
      flightEstimate: estimate,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getByLabelText('Тип наказу'), 'return');

    await waitFor(() => expect(estimate).toHaveBeenCalledWith(expect.objectContaining({ fleetId: 1, typeCode: 'return' })));
    expect(await within(dialog).findByText('Від 1:01:08 до 3:16:40')).toBeInTheDocument();
  });

  it('says where a fleet that is under way flies and when it arrives', async () => {
    const flying = sampleFleet({
      id: 1,
      name: 'Ескадра',
      ships: [],
      stateCode: 'flying',
      planetId: 0,
      flight: {
        orderId: 1,
        startedAt: '2026-11-07T00:00:00Z',
        arrivesAt: '2026-11-07T01:00:00Z',
        distance: 10,
        speed: 45,
        toSectorId: 1,
        toSystemId: null,
        toObjectId: null,
        toPlanetId: null,
        toX: 120,
        toY: 80,
        toZ: 0,
      },
    });
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(sampleFleetsView({ fleets: [flying], gameNow: '2026-11-07T00:00:00Z', timeMultiplier: 1 })),
      flightPlans: vi.fn().mockResolvedValue([sampleFlightPlan({ stateCode: 'active', currentPosition: 1 })]),
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    const panel = await screen.findByRole('complementary', { name: 'Деталі флоту' });

    expect(within(panel).getByText('У польоті')).toBeInTheDocument();
    expect(within(panel).getByText('Летить до:')).toBeInTheDocument();
    expect(within(panel).getByText('120 / 80 / 0')).toBeInTheDocument();
    expect(within(panel).getByText(/Прибуде через 0?1:00:0\d|Прибуде через 0:5\d:\d\d/)).toBeInTheDocument();

    // The ships of a fleet that is away are not changed: the buttons wait.
    expect(within(panel).getByRole('button', { name: 'Розформувати флот' })).toBeDisabled();
    expect(within(panel).getByRole('button', { name: 'Додати кораблі' })).toBeDisabled();
  });

  it('says why a plan stopped by itself', async () => {
    renderPage('/fleets', apis([sampleFlightPlan({ stateCode: 'paused', currentPosition: 2, pauseCode: 'fleet_flight_target_unknown' })]));

    const block = await screen.findByRole('region', { name: 'Політний лист' });

    expect(within(block).getByText('Лист зупинено: Цілі перельоту немає на карті або вона за межами сектора')).toBeInTheDocument();
  });

  it('writes the identifiers of the place the player chose', async () => {
    const create = vi.fn().mockResolvedValue(sampleFlightPlan());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([]),
      createFlightPlan: create,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getAllByLabelText('Ціль')[0]!, 'planet');
    await userEvent.selectOptions(within(dialog).getByLabelText('Сектор'), '1');

    const system = within(dialog).getByLabelText('Система');

    await waitFor(() => expect(within(system).getByRole('option', { name: 'Lyra' })).toBeInTheDocument());
    await userEvent.selectOptions(system, '3');

    const planet = within(dialog).getByLabelText('Планета');

    await waitFor(() => expect(within(planet).getByRole('option', { name: 'Nova' })).toBeInTheDocument());
    await userEvent.selectOptions(planet, '101');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Створити лист' }));

    await waitFor(() => expect(create).toHaveBeenCalled());

    const order = create.mock.calls[0]![0].orders[0];

    expect(order.targetKind).toBe('planet');
    expect(order.targetSectorId).toBe(1);
    expect(order.targetSystemId).toBe(3);
    expect(order.targetObjectId).toBe(101);
  });

  it('does not write a plan when the place of the target is not chosen whole', async () => {
    const create = vi.fn().mockResolvedValue(sampleFlightPlan());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([]),
      createFlightPlan: create,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));
    await userEvent.selectOptions(within(dialog).getAllByLabelText('Ціль')[0]!, 'planet');
    await userEvent.selectOptions(within(dialog).getByLabelText('Сектор'), '1');

    const system = within(dialog).getByLabelText('Система');

    await waitFor(() => expect(within(system).getByRole('option', { name: 'Lyra' })).toBeInTheDocument());
    await userEvent.selectOptions(system, '3');

    // The system is chosen, but the planet of it is not: the target names nothing.
    await userEvent.click(within(dialog).getByRole('button', { name: 'Створити лист' }));

    expect(create).not.toHaveBeenCalled();
  });

  it('reads the map of the world for the places of the target', async () => {
    const map = fakeMapApi();

    renderPage('/fleets', { auth: signedIn(), fleet: fakeFleetApi({ fleets: vi.fn().mockResolvedValue(withFleet()) }), map });

    await userEvent.click(await screen.findByRole('button', { name: 'Створити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Створити політний лист' });

    await userEvent.selectOptions(within(dialog).getByLabelText('Оберіть флот'), '1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Додати наказ' }));

    await waitFor(() => expect(map.galaxy).toHaveBeenCalled());
  });

  it('asks for the time only when the order is the extraction: no sector, system or place', async () => {
    const write = vi.fn().mockResolvedValue(sampleFlightPlan());
    const mine = sampleFlightPlan({
      orders: [
        {
          id: 5,
          position: 1,
          typeCode: 'mine',
          targetKind: 'object',
          targetSectorId: 1,
          targetSystemId: 0,
          targetObjectId: 77,
          targetX: 0,
          targetY: 0,
          targetZ: 0,
          durationSeconds: 1800,
          parameters: '{}',
        },
      ],
    });
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([mine]),
      createFlightPlan: write,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Змінити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Політний лист флоту' });

    // The only field of the order is the time, and the hint says where the extraction starts.
    expect(within(dialog).getByLabelText('Час видобування, хв')).toHaveValue(30);
    expect(within(dialog).getByText(/Видобування стартує там, де стоїть флот/)).toBeInTheDocument();
    expect(within(dialog).queryByLabelText('Сектор')).not.toBeInTheDocument();
    expect(within(dialog).queryByLabelText('Система')).not.toBeInTheDocument();
    expect(within(dialog).queryByLabelText('Місце видобутку')).not.toBeInTheDocument();
    expect(within(dialog).queryByLabelText('Ціль')).not.toBeInTheDocument();

    await userEvent.clear(within(dialog).getByLabelText('Час видобування, хв'));
    await userEvent.type(within(dialog).getByLabelText('Час видобування, хв'), '45');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Змінити лист' }));

    await waitFor(() => expect(write).toHaveBeenCalled());

    // The order is written with the time and without a target, whatever the old plan carried.
    const order = write.mock.calls[0]![0].orders[0];

    expect(order.typeCode).toBe('mine');
    expect(order.durationSeconds).toBe(2700);
    expect(order.targetKind).toBe('none');
    expect(order.targetObjectId).toBe(0);
    expect(order.targetSectorId).toBe(0);
  });

  it('writes an extraction without a time: the time is optional and means no limit', async () => {
    const write = vi.fn().mockResolvedValue(sampleFlightPlan());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([
        sampleFlightPlan({
          orders: [
            {
              id: 5,
              position: 1,
              typeCode: 'mine',
              targetKind: 'none',
              targetSectorId: 0,
              targetSystemId: 0,
              targetObjectId: 0,
              targetX: 0,
              targetY: 0,
              targetZ: 0,
              durationSeconds: 0,
              parameters: '{}',
            },
          ],
        }),
      ]),
      createFlightPlan: write,
    });

    renderPage('/fleets', { auth: signedIn(), fleet });

    await userEvent.click(await screen.findByRole('button', { name: 'Змінити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Політний лист флоту' });

    // The field is empty and nothing asks for a time: the plan is written as it is.
    expect(within(dialog).getByLabelText('Час видобування, хв')).toHaveValue(null);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Змінити лист' }));

    await waitFor(() => expect(write).toHaveBeenCalled());

    const order = write.mock.calls[0]![0].orders[0];

    expect(order.typeCode).toBe('mine');
    expect(order.durationSeconds).toBe(0);
    expect(order.targetKind).toBe('none');
  });
});
