import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, fakePlanetApi, renderPage, samplePlanet, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('PlanetPage', () => {
  it('shows the buildings, the queue, the balance, the storage and the energy', async () => {
    const planets = fakePlanetApi();
    renderPage('/planet', { auth: signedIn(), planets });

    expect(await screen.findByRole('heading', { name: 'Планета Nova' })).toBeInTheDocument();
    expect(planets.planet).toHaveBeenCalledWith(10);

    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });
    expect(within(queue).getByText('Шахта заліза, рівень 1')).toBeInTheDocument();
    expect(within(queue).getByText('Місць: 1 з 2')).toBeInTheDocument();
    expect(within(queue).getByRole('timer')).toBeInTheDocument();

    const resources = screen.getByRole('region', { name: 'Ресурси й склад' });
    expect(within(resources).getByText('Залізо')).toBeInTheDocument();
    expect(within(resources).getByText('+60')).toBeInTheDocument();

    const energy = screen.getByRole('region', { name: 'Енергія' });
    expect(within(energy).getByText('Після черги')).toBeInTheDocument();
  });

  it('keeps a closed building inactive and says why', async () => {
    renderPage('/planet', { auth: signedIn() });

    const card = (await screen.findByRole('heading', { name: 'Науковий центр' })).closest('li')!;
    const button = within(card).getByRole('button', { name: 'Збудувати' });

    expect(button).toBeDisabled();
    expect(within(card).getByText('Потрібно: Адміністративний центр, рівень 2')).toBeInTheDocument();
    expect(within(card).getByText('Не вистачить енергії: збудуй або покращ електростанцію')).toBeInTheDocument();
    expect(button).toHaveAttribute('title', expect.stringContaining('Адміністративний центр'));
  });

  it('queues the next level and cancels a record', async () => {
    const user = userEvent.setup();
    const planets = fakePlanetApi();
    renderPage('/planet', { auth: signedIn(), planets });

    const card = (await screen.findByRole('heading', { name: 'Адміністративний центр' })).closest('li')!;
    await user.click(within(card).getByRole('button', { name: 'Покращити' }));

    expect(planets.enqueue).toHaveBeenCalledWith(10, 1);
    expect(await screen.findByText('У черзі: Адміністративний центр, рівень 2.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Скасувати: Шахта заліза, рівень 1/ }));

    expect(planets.cancel).toHaveBeenCalledWith(10, 7);
    expect(await screen.findByText('Скасовано, ресурси повернено.')).toBeInTheDocument();
    expect(await screen.findByText('Черга порожня. Обери будівлю нижче й постав її в чергу.')).toBeInTheDocument();
  });

  it('shows the reason the server refused', async () => {
    const user = userEvent.setup();
    const planets = fakePlanetApi({ enqueue: vi.fn().mockRejectedValue(new ApiError(422, 'planets_resources_short')) });
    renderPage('/planet', { auth: signedIn(), planets });

    const card = (await screen.findByRole('heading', { name: 'Адміністративний центр' })).closest('li')!;
    await user.click(within(card).getByRole('button', { name: 'Покращити' }));

    expect(await screen.findByText('Не вистачає ресурсів')).toBeInTheDocument();
  });

  it('warns when a storage group is full', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(
        samplePlanet({ storageGroups: [{ resourceGroupId: 1, capacity: 1400, stored: 1400, isFull: true, fullAt: '2026-10-26T11:00:00Z' }] }),
      ),
    });
    renderPage('/planet', { auth: signedIn(), planets });

    expect(await screen.findByText('Склад заповнений')).toBeInTheDocument();
    expect(screen.getByText('Група «Видобувні» заповнена, шахти цієї групи стоять. Збудуй або покращ склад.')).toBeInTheDocument();
  });
});

describe('GamePage queue', () => {
  it('shows the real construction queue and opens the planet', async () => {
    const user = userEvent.setup();
    renderPage('/game', { auth: signedIn() });

    const queue = await screen.findByRole('region', { name: 'Черга будівництва' });
    expect(await within(queue).findByText('Шахта заліза, рівень 1')).toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: 'Будівництво' }));
    await waitFor(() => expect(window.location.pathname).toBe('/planet'));
  });

  it('keeps the construction closed until the e-mail is confirmed', async () => {
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe({ emailConfirmed: false })) });
    renderPage('/game', { auth });

    expect(await screen.findByRole('button', { name: 'Будівництво' })).toBeDisabled();
    expect(screen.queryByRole('region', { name: 'Черга будівництва' })).not.toBeInTheDocument();
  });
});
