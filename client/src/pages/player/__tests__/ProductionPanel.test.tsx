import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, fakeProductionApi, renderPage, sampleProductionFactory, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('ProductionPanel', () => {
  it('shows the recipes of the factory, the queue and the stock of the parts', async () => {
    const production = fakeProductionApi();
    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });

    const queue = (await within(panel).findByRole('heading', { name: 'Черга заводу' })).closest('div')!;
    expect(within(queue).getByText('Зроблено 1 з 3')).toBeInTheDocument();

    const recipes = within(panel).getByRole('heading', { name: 'Рецепти' }).closest('div')!;
    expect(within(recipes).getByText('Будматеріали')).toBeInTheDocument();
    expect(within(recipes).getByText('Корпусний модуль')).toBeInTheDocument();
    expect(within(recipes).getByText('На одиницю: Залізо 4')).toBeInTheDocument();

    const stock = within(panel).getByRole('heading', { name: 'Склад деталей' }).closest('div')!;
    expect(within(stock).getByText('Місць: 4 з 100')).toBeInTheDocument();
    expect(within(stock).getByText('З бонусом раси')).toBeInTheDocument();
    expect(within(stock).getByText('Мк. II')).toBeInTheDocument();
    expect(within(stock).getByText('Корпус «Оса» Мк. II')).toBeInTheDocument();
  });

  it('keeps a closed recipe inactive and says why', async () => {
    renderPage('/planet', { auth: signedIn() });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });
    const closed = (await within(panel).findByText('Закритий')).closest('li')!;

    expect(within(closed).getByText('Потрібна технологія: Глибинний видобуток, рівень 1')).toBeInTheDocument();
    expect(within(closed).queryByRole('button', { name: 'Виробляти' })).toBeNull();
  });

  it('puts a batch in the queue and cancels it', async () => {
    const user = userEvent.setup();
    const production = fakeProductionApi();
    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });

    const open = (await within(panel).findByText('Доступний')).closest('li')!;
    await user.click(within(open).getByRole('button', { name: 'Виробляти' }));

    expect(production.enqueue).toHaveBeenCalledWith(10, 500, 1, 1);
    expect(await screen.findByText('Партію поставлено в чергу заводу.')).toBeInTheDocument();

    await user.click(within(panel).getByRole('button', { name: 'Скасувати' }));

    expect(production.cancel).toHaveBeenCalledWith(10, 500, 700);
  });

  it('warns when the storage of the parts is full', async () => {
    const production = fakeProductionApi({
      stock: vi.fn().mockResolvedValue({
        planetId: 10,
        stacks: [],
        storage: { capacity: 10, stored: 10, fullAt: null },
        gameNow: '2026-10-26T12:00:00Z',
      }),
    });

    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });

    await waitFor(() => expect(within(panel).getByText('Склад заповнений')).toBeInTheDocument());
  });

  it('says that the planet has no factory', async () => {
    const production = fakeProductionApi({ factories: vi.fn().mockResolvedValue([]) });

    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });

    expect(await within(panel).findByText('На планеті ще немає заводу: збудуй його, щоб виробляти.')).toBeInTheDocument();
    expect(production.factory).not.toHaveBeenCalled();
  });

  it('reads the factory again when the next unit is due', async () => {
    const production = fakeProductionApi({
      factory: vi.fn().mockResolvedValue(
        sampleProductionFactory({
          gameNow: '2026-10-26T12:00:00Z',
          queue: [
            {
              orderId: 700,
              recipeId: 1,
              quantity: 3,
              producedQuantity: 1,
              position: 1,
              startsAt: '2026-10-26T11:59:00Z',
              nextUnitAt: '2026-10-26T12:00:01Z',
              finishesAt: '2026-10-26T12:02:00Z',
              isBlocked: false,
            },
          ],
        }),
      ),
    });

    renderPage('/planet', { auth: signedIn(), production });

    await screen.findByRole('region', { name: 'Виробництво' });
    await waitFor(() => expect(production.factory).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(production.factory).toHaveBeenCalledTimes(2), { timeout: 4000 });
  });

  it('says that the factory has no recipes yet', async () => {
    const production = fakeProductionApi({
      factory: vi.fn().mockResolvedValue(sampleProductionFactory({ recipes: [] })),
    });

    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Виробництво' });

    expect(await within(panel).findByText('У цього заводу ще немає рецептів.')).toBeInTheDocument();
  });
});
