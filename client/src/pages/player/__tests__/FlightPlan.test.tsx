import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeFleetApi,
  fakeMapApi,
  renderPage,
  sampleFleet,
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

  it('keeps the button of carrying out the plan off, with the reason beside it', async () => {
    renderPage('/fleets', apis([sampleFlightPlan()]));

    const block = await screen.findByRole('region', { name: 'Політний лист' });
    const button = within(block).getByRole('button', { name: 'Виконати' });

    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', 'Виконання політного листа з’явиться пізніше.');
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
});
