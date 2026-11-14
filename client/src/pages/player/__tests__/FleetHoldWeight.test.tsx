import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { maxLoadable } from '../fleets/flightPlanCargo';
import {
  fakeAuthApi,
  fakeFleetApi,
  fakePlanetApi,
  renderPage,
  sampleFleet,
  sampleFleetHold,
  sampleFleetShip,
  sampleFleetsView,
  samplePlanet,
  signedInMe,
} from '../../../test/renderPage';

/**
 * The hold of a fleet counts the room of a load by the weight of each resource (author's rule, 14.11.2026):
 * the weight comes from the resource, the room from the project of the ships, in the same tonnes.
 */
describe('the weight of a resource in the hold', () => {
  it('counts how many units fit by the weight of a unit', () => {
    expect(maxLoadable(100, 2.5, null)).toBe(40);
    expect(maxLoadable(100, 0.5, null)).toBe(200);
    expect(maxLoadable(100, 0.5, 150)).toBe(150);
    expect(maxLoadable(-5, 2.5, 10)).toBe(0);
    expect(maxLoadable(100, 0, 10)).toBeNull();
  });

  it('shows in the window of the hold how much of a heavy and of a light resource still fits', async () => {
    const user = userEvent.setup();
    const fleetHold = vi.fn(async (fleetId: number) =>
      sampleFleetHold({
        fleetId,
        capacity: 500,
        carried: 265,
        items: [
          { resourceId: 3, amount: 100 },
          { resourceId: 1, amount: 30 },
        ],
        resourceWeights: [
          { resourceId: 1, weight: 0.5 },
          { resourceId: 3, weight: 2.5 },
        ],
      }),
    );
    const view = sampleFleetsView({
      limit: 3,
      used: 1,
      fleets: [sampleFleet({ id: 1, planetId: 10, ships: [sampleFleetShip({ id: 1, designId: 7, name: 'Barracuda', fleetId: 1 })] })],
    });

    renderPage('/fleets', {
      auth: fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) }),
      fleet: fakeFleetApi({ fleets: vi.fn().mockResolvedValue(view), fleetHold }),
      planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(samplePlanet()) }),
    });

    await user.click(await screen.findByRole('button', { name: 'Показати трюм' }));

    const dialog = await screen.findByRole('dialog', { name: 'Трюм флоту' });

    expect(await within(dialog).findByText('Зайнято 265 з 500 т')).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Завантажити зі складу' }));
    await user.click(within(dialog).getByRole('button', { name: 'Додати позицію' }));

    const thing = within(dialog).getByRole('combobox', { name: 'Що перевозити' });

    // The holds carry 100 × 2.5 + 30 × 0.5 = 265 t of 500 t: 235 t are free.
    await within(dialog).findByRole('option', { name: /Мідь/ });
    await user.selectOptions(thing, 'r:3');

    expect(within(dialog).getByText('Є 500 · влізе 94')).toBeInTheDocument();

    await user.selectOptions(thing, 'r:1');

    expect(within(dialog).getByText('Є 900 · влізе 470')).toBeInTheDocument();
  });
});
