import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeFleetApi,
  fakeMapApi,
  renderPage,
  sampleFlightFuelEstimate,
  sampleFlightPlan,
  sampleFleetsView,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * One fleet of the player, so a flight plan may be shown for it.
 */
const withFleet = () =>
  sampleFleetsView({
    limit: 3,
    used: 1,
    fleets: [
      {
        id: 1,
        name: 'Ескадра',
        homePlanetId: 10,
        planetId: 10,
        sectorId: 1,
        stateCode: 'stationed',
        crew: 12,
        upkeepPerHour: 2,
        createdAt: '2026-11-07T00:00:00Z',
        ships: [],
        holdCapacity: 1200,
        integrity: 15300,
        damageMin: 264,
        damageMax: 420,
        shieldCapacity: 3200,
      },
    ],
    freeShips: [],
  });

/**
 * A plan of a hyperflight: the legs of the command share one key (task 00072).
 */
const hyperflightPlan = () =>
  sampleFlightPlan({
    orders: [
      {
        id: 1,
        position: 1,
        typeCode: 'hyperfly',
        targetKind: 'none',
        targetSectorId: 0,
        targetSystemId: 0,
        targetObjectId: 0,
        targetX: 0,
        targetY: 0,
        targetZ: 0,
        durationSeconds: 0,
        parameters: '{}',
        groupKey: '11111111-1111-1111-1111-111111111111',
        command: 'hyperfly',
        leg: 'to_edge',
      },
      {
        id: 2,
        position: 2,
        typeCode: 'hyperfly',
        targetKind: 'none',
        targetSectorId: 0,
        targetSystemId: 0,
        targetObjectId: 0,
        targetX: 0,
        targetY: 0,
        targetZ: 0,
        durationSeconds: 0,
        parameters: '{}',
        groupKey: '11111111-1111-1111-1111-111111111111',
        command: 'hyperfly',
        leg: 'ftl',
      },
      {
        id: 3,
        position: 3,
        typeCode: 'hyperfly',
        targetKind: 'none',
        targetSectorId: 0,
        targetSystemId: 0,
        targetObjectId: 0,
        targetX: 0,
        targetY: 0,
        targetZ: 0,
        durationSeconds: 0,
        parameters: '{}',
        groupKey: '11111111-1111-1111-1111-111111111111',
        command: 'hyperfly',
        leg: 'from_edge',
      },
    ],
  });

describe('the legs of a flight and the fuel of a fleet (task 00072)', () => {
  it('shows the legs of a hyperflight as one row that opens', async () => {
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([hyperflightPlan()]),
      fuelEstimate: vi.fn().mockResolvedValue(sampleFlightFuelEstimate()),
    });

    renderPage('/fleets', { auth: signedIn(), fleet, map: fakeMapApi() });

    const list = await waitFor(() => {
      const found = document.querySelector<HTMLElement>('ol.pl-orders');

      if (found === null) {
        throw new Error('The list of the orders is not on the screen.');
      }

      return found;
    });

    // The three legs of the command are one row of the list (task 00072).
    await waitFor(() => {
      expect(within(list).getAllByRole('listitem')).toHaveLength(1);
    });

    // The row opens and shows the legs under it.
    await userEvent.click(within(list).getByRole('button'));

    await waitFor(() => {
      expect(within(list).getByText(/до краю системи/i)).toBeInTheDocument();
    });
  });

  it('shows the need of the fuel and what the hold holds', async () => {
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([hyperflightPlan()]),
      fuelEstimate: vi.fn().mockResolvedValue(
        sampleFlightFuelEstimate({
          fuelNeed: [{ resourceTypeId: 12, amount: 120, have: 500 }],
          shortages: [],
        }),
      ),
    });

    renderPage('/fleets', { auth: signedIn(), fleet, map: fakeMapApi() });

    await waitFor(() => {
      expect(screen.getByText(/потрібно 120/i)).toBeInTheDocument();
    });
  });

  it('keeps the button of sending dark when the hold holds too little fuel', async () => {
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([hyperflightPlan()]),
      fuelEstimate: vi.fn().mockResolvedValue(
        sampleFlightFuelEstimate({
          fuelNeed: [{ resourceTypeId: 12, amount: 900, have: 100 }],
          shortages: [{ resourceTypeId: 12, need: 900, have: 100 }],
        }),
      ),
    });

    renderPage('/fleets', { auth: signedIn(), fleet, map: fakeMapApi() });

    const send = await screen.findByRole('button', { name: /виконати/i });

    await waitFor(() => {
      expect(send).toBeDisabled();
    });

    // The shortage is told to the player (task 00072).
    expect(screen.getAllByText(/бракує палива/i).length).toBeGreaterThan(0);
  });
  it('gives a saved hyperflight back to the editor as the one command it was made from', async () => {
    // The server wrote the command as its legs, and every leg keeps the target of the command.
    const leg = (id: number, typeCode: string, legRole: string) => ({
      id,
      position: id,
      typeCode,
      targetKind: 'point',
      targetSectorId: 3,
      targetSystemId: 0,
      targetObjectId: 0,
      targetX: 180.2,
      targetY: 117.5,
      targetZ: 0,
      durationSeconds: 0,
      parameters: '{}',
      groupKey: '22222222-2222-2222-2222-222222222222',
      command: 'hyperfly',
      leg: legRole,
    });
    const write = vi.fn().mockResolvedValue(sampleFlightPlan());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([sampleFlightPlan({ orders: [leg(1, 'fly', 'to_edge'), leg(2, 'hyperfly', 'ftl')] })]),
      createFlightPlan: write,
      saveFlightPlan: write,
    });

    renderPage('/fleets', { auth: signedIn(), fleet, map: fakeMapApi() });

    await userEvent.click(await screen.findByRole('button', { name: 'Змінити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Політний лист флоту' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Змінити лист' }));

    await waitFor(() => expect(write).toHaveBeenCalled());

    // The editor holds the command, not its legs: the save spreads it over the legs again.
    const orders = write.mock.calls[0]![0].orders;

    expect(orders).toHaveLength(1);
    expect(orders[0]!.typeCode).toBe('hyperfly');
    expect(orders[0]!.targetX).toBe(180.2);
    expect(orders[0]!.targetY).toBe(117.5);
  });
});
