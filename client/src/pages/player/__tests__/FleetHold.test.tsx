import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import {
  fakeAuthApi,
  fakeFleetApi,
  renderPage,
  sampleFleet,
  sampleFleetHold,
  sampleFleetShip,
  sampleFleetsView,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * One fleet of one ship on the home body.
 */
const view = () =>
  sampleFleetsView({
    limit: 3,
    used: 1,
    fleets: [sampleFleet({ id: 1, ships: [sampleFleetShip({ id: 1, designId: 7, name: 'Barracuda', fleetId: 1 })] })],
  });

const apis = (fleetHold = vi.fn(async (fleetId: number) => sampleFleetHold({ fleetId }))) => ({
  auth: signedIn(),
  fleet: fakeFleetApi({ fleets: vi.fn().mockResolvedValue(view()), fleetHold }),
});

describe('the hold of a fleet (task 00056)', () => {
  it('has a button on the panel of the fleet and reads nothing until it is pressed', async () => {
    const options = apis();

    renderPage('/fleets', options);

    expect(await screen.findByRole('button', { name: 'Показати трюм' })).toBeInTheDocument();
    expect(options.fleet.fleetHold).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows the room taken against the room there is and what is in the holds, the greatest amount first', async () => {
    const user = userEvent.setup();
    const options = apis();

    renderPage('/fleets', options);

    await user.click(await screen.findByRole('button', { name: 'Показати трюм' }));

    const dialog = await screen.findByRole('dialog', { name: 'Трюм флоту' });

    expect(await within(dialog).findByText('Зайнято 130 з 500 т')).toBeInTheDocument();
    expect(options.fleet.fleetHold).toHaveBeenCalledTimes(1);
    expect(options.fleet.fleetHold).toHaveBeenCalledWith(1);

    const items = within(within(dialog).getByRole('list')).getAllByRole('listitem');

    expect(items.map((item) => item.textContent)).toEqual(['Мідь100 т', 'Залізо30 т']);
  });

  it('puts the items of the same amount in the order of their names', async () => {
    const user = userEvent.setup();
    const options = apis(
      vi.fn(async (fleetId: number) =>
        sampleFleetHold({
          fleetId,
          carried: 100,
          items: [
            { resourceId: 3, amount: 50 },
            { resourceId: 1, amount: 50 },
          ],
        }),
      ),
    );

    renderPage('/fleets', options);

    await user.click(await screen.findByRole('button', { name: 'Показати трюм' }));

    const dialog = await screen.findByRole('dialog', { name: 'Трюм флоту' });
    const items = within(await within(dialog).findByRole('list')).getAllByRole('listitem');

    expect(items.map((item) => item.textContent)).toEqual(['Залізо50 т', 'Мідь50 т']);
  });

  it('says the hold is empty when nothing is in it', async () => {
    const user = userEvent.setup();
    const options = apis(vi.fn(async (fleetId: number) => sampleFleetHold({ fleetId, carried: 0, items: [] })));

    renderPage('/fleets', options);

    await user.click(await screen.findByRole('button', { name: 'Показати трюм' }));

    const dialog = await screen.findByRole('dialog', { name: 'Трюм флоту' });

    expect(await within(dialog).findByText('Трюм порожній')).toBeInTheDocument();
    expect(within(dialog).getByText('Зайнято 0 з 500 т')).toBeInTheDocument();
    expect(within(dialog).queryByRole('list')).not.toBeInTheDocument();
  });

  it('says the hold could not be read when the server refuses', async () => {
    const user = userEvent.setup();
    const options = apis(vi.fn(async () => Promise.reject(new ApiError(400, 'fleet_fleet_unknown'))));

    renderPage('/fleets', options);

    await user.click(await screen.findByRole('button', { name: 'Показати трюм' }));

    expect(await screen.findByText('Не вдалося прочитати трюм.')).toBeInTheDocument();
  });

  it('closes the window with the button and leaves the screen as it was', async () => {
    const user = userEvent.setup();
    const options = apis();

    renderPage('/fleets', options);

    await user.click(await screen.findByRole('button', { name: 'Показати трюм' }));
    await user.click(within(await screen.findByRole('dialog', { name: 'Трюм флоту' })).getByRole('button', { name: 'Закрити' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(options.fleet.fleets).toHaveBeenCalledTimes(1);
  });
});
