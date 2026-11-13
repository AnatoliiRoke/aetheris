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

/**
 * A saved hyperflight to a point of open space (task 00072): the server replaced the command by real
 * commands, a flight to the edge of the system and the hyperflight to the point, each with its own
 * end and its time; an ordinary wait follows them.
 */
const savedHyperflight = () =>
  sampleFlightPlan({
    orders: [
      {
        id: 1,
        position: 1,
        typeCode: 'fly',
        targetKind: 'system',
        targetSectorId: 1,
        targetSystemId: 4,
        targetObjectId: 0,
        targetX: 0,
        targetY: 0,
        targetZ: 0,
        durationSeconds: 137,
        parameters: '{}',
        groupKey: '33333333-3333-3333-3333-333333333333',
        command: 'hyperfly',
        leg: 'to_edge',
      },
      {
        id: 2,
        position: 2,
        typeCode: 'hyperfly',
        targetKind: 'point',
        targetSectorId: 1,
        targetSystemId: 0,
        targetObjectId: 0,
        targetX: 181.6,
        targetY: 127.8,
        targetZ: 0,
        durationSeconds: 2540,
        parameters: '{}',
        groupKey: '33333333-3333-3333-3333-333333333333',
        command: 'hyperfly',
        leg: 'ftl',
      },
      {
        id: 3,
        position: 3,
        typeCode: 'wait',
        targetKind: 'none',
        targetSectorId: 0,
        targetSystemId: 0,
        targetObjectId: 0,
        targetX: 0,
        targetY: 0,
        targetZ: 0,
        durationSeconds: 600,
        parameters: '{}',
      },
    ],
  });

describe('the legs of a flight and the fuel of a fleet (task 00072)', () => {
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
  it('shows the commands of a saved hyperflight one by one with their ends and their times', async () => {
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([savedHyperflight()]),
    });

    renderPage('/fleets', { auth: signedIn(), fleet, map: fakeMapApi() });

    const list = await waitFor(() => {
      const found = document.querySelector<HTMLElement>('ol.pl-orders');

      if (found === null) {
        throw new Error('The list of the orders is not on the screen.');
      }

      return found;
    });

    // Every command is a row of its own: the flight to the edge of the system with its time, and the
    // hyperflight to the point with its own time.
    const rows = within(list).getAllByRole('listitem');

    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent('Переліт');
    expect(rows[0]).toHaveTextContent('Край системи');
    expect(rows[0]).toHaveTextContent('0:02:17');
    expect(rows[1]).toHaveTextContent('Гіперперліт');
    expect(rows[1]).toHaveTextContent('181.6 / 127.8 / 0');
    expect(rows[1]).toHaveTextContent('0:42:20');
  });

  it('gives the editor the commands of a saved hyperflight as they are, each with its own end', async () => {
    const write = vi.fn().mockResolvedValue(sampleFlightPlan());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([savedHyperflight()]),
      createFlightPlan: write,
      saveFlightPlan: write,
    });

    renderPage('/fleets', { auth: signedIn(), fleet, map: fakeMapApi() });

    await userEvent.click(await screen.findByRole('button', { name: 'Змінити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Політний лист флоту' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Змінити лист' }));

    await waitFor(() => expect(write).toHaveBeenCalled());

    const orders = write.mock.calls[0]![0].orders;

    expect(orders).toHaveLength(3);
    expect(orders[0]).toMatchObject({ typeCode: 'fly', targetKind: 'system', targetSystemId: 4 });
    expect(orders[1]).toMatchObject({ typeCode: 'hyperfly', targetKind: 'point', targetX: 181.6, targetY: 127.8 });
    expect(orders[2]).toMatchObject({ typeCode: 'wait' });
  });

  it('removes the flight to the edge together with the hyperflight it belongs to', async () => {
    const write = vi.fn().mockResolvedValue(sampleFlightPlan());
    const fleet = fakeFleetApi({
      fleets: vi.fn().mockResolvedValue(withFleet()),
      flightPlans: vi.fn().mockResolvedValue([savedHyperflight()]),
      createFlightPlan: write,
      saveFlightPlan: write,
    });

    renderPage('/fleets', { auth: signedIn(), fleet, map: fakeMapApi() });

    await userEvent.click(await screen.findByRole('button', { name: 'Змінити лист' }));

    const dialog = await screen.findByRole('dialog', { name: 'Політний лист флоту' });

    // The second command is the hyperflight: removing it removes the flight to the edge as well.
    await userEvent.click(within(dialog).getAllByRole('button', { name: 'Прибрати наказ' })[1]!);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Змінити лист' }));

    await waitFor(() => expect(write).toHaveBeenCalled());

    const orders = write.mock.calls[0]![0].orders;

    expect(orders).toHaveLength(1);
    expect(orders[0]).toMatchObject({ typeCode: 'wait' });
  });
});
