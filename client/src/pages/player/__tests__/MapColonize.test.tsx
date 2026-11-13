import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeFleetApi,
  fakeMapApi,
  renderPage,
  sampleFleetHold,
  sampleFlightPlan,
  sampleMapSystem,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * The home system with the free body «Lyra-3» marked as a planet the race of the player may colonize.
 */
const systemWithColony = () => {
  const system = sampleMapSystem();

  return { ...system, objects: system.objects.map((object) => (object.planetId === 11 ? { ...object, suitableForRace: true } : object)) };
};

describe('the colonization from the map (task 00073)', () => {
  it('offers the colonization on a free planet that suits the race and opens the draft of the chosen fleet', async () => {
    const user = userEvent.setup();
    const colonize = vi.fn().mockResolvedValue(sampleFlightPlan({ fleetId: 1 }));
    const fleet = fakeFleetApi({
      fleetHold: vi.fn().mockResolvedValue(sampleFleetHold({ fleetId: 1, availableOrders: ['colonize'] })),
      colonizeFlightPlan: colonize,
    });

    renderPage('/map?sector=1&system=3', { auth: signedIn(), fleet, map: fakeMapApi({ system: vi.fn().mockResolvedValue(systemWithColony()) }) });

    await user.click(await screen.findByRole('button', { name: /Lyra-3/ }));

    const card = await screen.findByRole('complementary', { name: 'Картка об’єкта' });

    await user.click(within(card).getByRole('button', { name: 'Колонізувати' }));

    // The window lists the fleets that carry a colony module; the chosen one gets the draft of the plan.
    const chooser = await screen.findByRole('dialog', { name: 'Колонізація планети' });

    await user.click(await within(chooser).findByRole('button', { name: 'Перший ескадрон' }));

    await waitFor(() => expect(colonize).toHaveBeenCalledWith({ fleetId: 1, planetId: 11 }));
    expect(await screen.findByRole('dialog', { name: 'Політний лист флоту' })).toBeInTheDocument();
  });

  it('does not offer the colonization on a free planet that does not suit the race', async () => {
    const user = userEvent.setup();

    renderPage('/map?sector=1&system=3', { auth: signedIn(), map: fakeMapApi({ system: vi.fn().mockResolvedValue(sampleMapSystem()) }) });

    await user.click(await screen.findByRole('button', { name: /Lyra-3/ }));

    const card = await screen.findByRole('complementary', { name: 'Картка об’єкта' });

    expect(within(card).queryByRole('button', { name: 'Колонізувати' })).toBeNull();
  });
});
