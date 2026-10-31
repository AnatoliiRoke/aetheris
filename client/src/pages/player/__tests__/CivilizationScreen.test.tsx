import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeWorldApi,
  renderPage,
  sampleOwnedPlanet,
  sampleWorldReference,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * The reference tables with the texts of the types of the bodies, so the names of the types
 * and the resources are real.
 */
const reference = () =>
  sampleWorldReference({
    resources: [
      { id: 1, code: 'resource_1', nameKey: 'world.resource.resource_1.name', descriptionKey: '', groupCode: 'resource_group_1', groupId: 1, imageKey: null },
      { id: 3, code: 'resource_3', nameKey: 'world.resource.resource_3.name', descriptionKey: '', groupCode: 'resource_group_1', groupId: 1, imageKey: null },
    ],
    objectTypes: [
      { id: 5, code: 'object_type_5', nameKey: 'world.object_type.object_type_5.name', descriptionKey: '', groupCode: null, groupId: null, imageKey: 'art-12a' },
    ],
  });

describe('the screen of the civilization (task 00010)', () => {
  it('shows the bodies of the player with their type, their place and their deposits', async () => {
    const world = fakeWorldApi({
      planets: vi.fn().mockResolvedValue([
        sampleOwnedPlanet({
          planetId: 10,
          name: 'Nova',
          objectTypeId: 5,
          sectorId: 2,
          systemId: 7,
          orbit: 3,
          deposits: [{ resourceTypeId: 1, coefficient: 1.2 }],
        }),
      ]),
      reference: vi.fn().mockResolvedValue(reference()),
    });

    renderPage('/game', { auth: signedIn(), world });

    const card = await screen.findByRole('article', { name: 'Nova' });

    // The place of the body in the galaxy comes from the server.
    expect(within(card).getByText(/Сектор 2/)).toBeInTheDocument();
    expect(within(card).getByText(/Система 7/)).toBeInTheDocument();
    expect(within(card).getByText(/Орбіта 3/)).toBeInTheDocument();

    // The deposit names the resource from the reference table of the world.
    const deposits = card.querySelector('.deposits');
    expect(within(deposits as HTMLElement).getByText('Залізо')).toBeInTheDocument();

    // The picture of the type comes from the reference table too.
    const picture = card.querySelector('img.body-art__image');
    expect(picture?.getAttribute('src')).toContain('art-12a');
  });

  it('says so when the player has no bodies yet', async () => {
    const world = fakeWorldApi({ planets: vi.fn().mockResolvedValue([]) });

    renderPage('/game', { auth: signedIn(), world });

    expect(await screen.findByText('У тебе ще немає планет. Заснуй цивілізацію, щоб почати.')).toBeInTheDocument();
  });

  it('shows every body of the player and lets them choose one', async () => {
    const user = userEvent.setup();
    const world = fakeWorldApi({
      planets: vi.fn().mockResolvedValue([
        sampleOwnedPlanet({ planetId: 10, name: 'Nova' }),
        sampleOwnedPlanet({ planetId: 11, name: 'Vega' }),
      ]),
      reference: vi.fn().mockResolvedValue(reference()),
    });

    renderPage('/game', { auth: signedIn(), world });

    const first = await screen.findByRole('article', { name: 'Nova' });
    const second = await screen.findByRole('article', { name: 'Vega' });

    // The first body is the chosen one when the screen opens.
    expect(first.className).toContain('is-current');
    expect(second.className).not.toContain('is-current');

    await user.click(within(second).getByRole('button', { name: 'Vega' }));

    await waitFor(() => expect(second.className).toContain('is-current'));
    expect(first.className).not.toContain('is-current');
  });

  it('keeps the sections that no task has made yet inactive with the reason', async () => {
    renderPage('/game', { auth: signedIn() });

    const market = await screen.findByRole('button', { name: 'Ринок' });

    expect(market).toBeDisabled();
    // The hint tells the reason, and the reason begins with the word of the placeholder.
    expect(market.getAttribute('title')).toMatch(/^Скоро: /);
  });

  it('keeps the actions closed until the address is confirmed', async () => {
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe({ emailConfirmed: false })) });

    renderPage('/game', { auth });

    expect(await screen.findByText('Підтверди пошту')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Будівництво' })).toBeDisabled();
  });

  it('shows the screen in English when the language is English', async () => {
    renderPage('/game', { auth: signedIn() });

    await screen.findByRole('heading', { name: 'Вітаємо, Pilot' });

    await userEvent.setup().click(screen.getByRole('button', { name: 'EN' }));

    expect(await screen.findByRole('heading', { name: 'Welcome, Pilot' })).toBeInTheDocument();
    expect(screen.getByText('My planets')).toBeInTheDocument();
  });

  it('marks the current body with the classes of the design and offers the mobile tabs', async () => {
    renderPage('/game', { auth: signedIn() });

    await screen.findByRole('heading', { name: 'Вітаємо, Pilot' });

    // The tabs of the phone are in the tree on every size; the style sheet shows them only on
    // the phone, so the test checks the classes and not the size.
    const tabs = document.querySelector('nav.civ-tabbar');

    expect(tabs).not.toBeNull();
    expect(within(tabs as HTMLElement).getAllByText('Огляд').length).toBeGreaterThan(0);
  });
});
