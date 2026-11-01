import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, fakeProductionApi, renderPage, sampleProductionStock, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('ProductionPanel', () => {
  it('shows the stock of the parts with the way to the production screen', async () => {
    renderPage('/planet', { auth: signedIn() });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });
    const stock = (await within(panel).findByRole('heading', { name: 'Склад деталей' })).closest('div')!;

    expect(within(panel).getByRole('link', { name: 'Відкрити виробництво' })).toHaveAttribute('href', '/planet/production');
    expect(within(stock).getByText('Місткість кожного виду деталі: 100')).toBeInTheDocument();
    expect(within(stock).queryByText('Склад заповнений')).not.toBeInTheDocument();
    expect(within(stock).getByText('З бонусом раси')).toBeInTheDocument();
    expect(within(stock).getByText('Мк. II')).toBeInTheDocument();
    expect(within(stock).getByText('Корпус «Оса» Мк. II')).toBeInTheDocument();
    // The recipes live on the production screen, not on the planet.
    expect(within(panel).queryByRole('heading', { name: 'Рецепти' })).toBeNull();
  });

  it('says that the production could not be loaded', async () => {
    const production = fakeProductionApi({ stock: vi.fn().mockRejectedValue(new Error('down')) });

    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });

    expect(await within(panel).findByText('Не вдалося завантажити виробництво.')).toBeInTheDocument();
  });

  it('uses a capsule of inhabitants from the stock and tells how many joined', async () => {
    const user = userEvent.setup();
    const [first] = sampleProductionStock().stacks;
    const production = fakeProductionApi({
      stock: vi.fn().mockResolvedValue(
        sampleProductionStock({
          stacks: [first, { ...first, partTypeId: 9, markLevel: 1, hasRaceBonus: false, quantity: 2, nameKey: 'production.part_type.part_m_80.name', capsuleInhabitants: 100, capsuleUsable: true }],
        }),
      ),
    });

    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });
    const stock = (await within(panel).findByRole('heading', { name: 'Склад деталей' })).closest('div')!;

    // Only the stack of the capsule has the button.
    const buttons = within(stock).getAllByRole('button', { name: /Використати капсулу/ });
    expect(buttons).toHaveLength(1);
    expect(buttons[0]).toHaveTextContent('+100 мешканців');

    await user.click(buttons[0]!);

    expect(production.useCapsule).toHaveBeenCalledWith(10, 9);
    expect(await screen.findByText(/Капсулу використано: до населення додано 100 мешканців/)).toBeInTheDocument();
  });

  it('says why a capsule was refused', async () => {
    const user = userEvent.setup();
    const [first] = sampleProductionStock().stacks;
    const production = fakeProductionApi({
      stock: vi.fn().mockResolvedValue(sampleProductionStock({ stacks: [{ ...first!, capsuleInhabitants: 100, capsuleUsable: true }] })),
      useCapsule: vi.fn().mockRejectedValue(new ApiError(400, 'production_part_missing')),
    });

    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });
    await user.click(await within(panel).findByRole('button', { name: /Використати капсулу/ }));

    expect(await screen.findByText('Такої деталі на планеті немає')).toBeInTheDocument();
  });

  it('shows a capsule of another race as one that can be sold but not deployed', async () => {
    const [first] = sampleProductionStock().stacks;
    const production = fakeProductionApi({
      stock: vi.fn().mockResolvedValue(sampleProductionStock({ stacks: [{ ...first!, capsuleInhabitants: 100, capsuleUsable: false }] })),
    });

    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });

    expect(await within(panel).findByText('Інша раса: можна продати, не розгорнути')).toBeInTheDocument();
    expect(within(panel).queryByRole('button', { name: /Використати капсулу/ })).toBeNull();
  });
});
