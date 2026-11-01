import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, fakeProductionApi, renderPage, sampleProductionRecipes, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('the production screen (task 00032)', () => {
  it('shows the queue and the tiles of every recipe of every factory, grouped', async () => {
    renderPage('/planet/production', { auth: signedIn() });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });

    // The queue of the batches is the standard one, the same as on the planet.
    expect(await within(await screen.findByRole('region', { name: 'Черга виробництва' })).findByText(/Зроблено 1 з 3/)).toBeInTheDocument();

    // The resources and two groups of parts; the recipe of the factory the body lacks is among them, closed.
    expect(within(recipes).getByRole('heading', { name: 'Ресурси' })).toBeInTheDocument();
    expect(within(recipes).getByRole('heading', { name: 'Корпусні модулі' })).toBeInTheDocument();
    expect(within(recipes).getByRole('heading', { name: 'Реактори' })).toBeInTheDocument();
    expect(within(recipes).getByText('Будматеріали')).toBeInTheDocument();
    expect(within(recipes).getByText('Корпусний модуль')).toBeInTheDocument();
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();
    // The cost of a unit is a list of resources one by one, like on the other screens, and the time of a unit.
    const materials = within(within(recipes).getByText('Будматеріали').closest('li')!);
    const cost = materials.getByRole('list', { name: 'Потрібні ресурси' });
    expect(within(cost).getByText('Залізо')).toBeInTheDocument();
    expect(within(cost).getByText('4')).toBeInTheDocument();
    expect(materials.getByText('Час виробництва')).toBeInTheDocument();
    expect(within(recipes).queryByText(/На одиницю/)).toBeNull();
    // The description of a recipe is not on the tile.
    expect(within(recipes).queryByText('Плити й балки з сировини.')).toBeNull();
    expect(within(recipes).getByText('Спершу збудуй: Верф')).toBeInTheDocument();
    expect(within(recipes).getAllByRole('button', { name: 'Виробляти' })).toHaveLength(1);
  });

  it('filters the recipes by the search, the availability, the factory and the group', async () => {
    const user = userEvent.setup();
    renderPage('/planet/production', { auth: signedIn() });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    await within(recipes).findByText('Реактор малий');

    await user.type(within(recipes).getByRole('searchbox'), 'реактор');
    expect(within(recipes).queryByText('Будматеріали')).toBeNull();
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();
    await user.clear(within(recipes).getByRole('searchbox'));

    await user.click(within(recipes).getByRole('button', { name: 'Доступні' }));
    expect(within(recipes).getByText('Будматеріали')).toBeInTheDocument();
    expect(within(recipes).queryByText('Корпусний модуль')).toBeNull();
    await user.click(within(recipes).getByRole('button', { name: 'Усі' }));

    await user.selectOptions(within(recipes).getByLabelText('Завод'), 'Верф');
    expect(within(recipes).queryByText('Будматеріали')).toBeNull();
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();
    await user.selectOptions(within(recipes).getByLabelText('Завод'), 'Усі заводи');

    await user.selectOptions(within(recipes).getByLabelText('Група'), 'Ресурси');
    expect(within(recipes).getByText('Будматеріали')).toBeInTheDocument();
    expect(within(recipes).queryByText('Реактор малий')).toBeNull();

    await user.type(within(recipes).getByRole('searchbox'), 'немає такого');
    expect(await within(recipes).findByText('Жоден рецепт не підходить під фільтри.')).toBeInTheDocument();
  });

  it('puts the chosen number of units into the queue of the factory that makes the recipe', async () => {
    const user = userEvent.setup();
    const production = fakeProductionApi();
    renderPage('/planet/production', { auth: signedIn(), production });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    const card = (await within(recipes).findByText('Будматеріали')).closest('li')!;
    const quantity = within(card).getByLabelText('Кількість');

    await user.clear(quantity);
    await user.type(quantity, '5');
    await user.click(within(card).getByRole('button', { name: 'Виробляти' }));

    expect(production.enqueue).toHaveBeenCalledWith(10, 500, 1, 5);
    expect(await screen.findByText('Партію поставлено в чергу заводу.')).toBeInTheDocument();
    // The queue and the recipes are read again after the command.
    await waitFor(() => expect(vi.mocked(production.orders).mock.calls.length).toBeGreaterThan(1));
    await waitFor(() => expect(vi.mocked(production.recipes).mock.calls.length).toBeGreaterThan(1));
  });

  it('refuses a number that is not a whole positive one', async () => {
    const user = userEvent.setup();
    const production = fakeProductionApi();
    renderPage('/planet/production', { auth: signedIn(), production });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    const card = (await within(recipes).findByText('Будматеріали')).closest('li')!;
    const quantity = within(card).getByLabelText('Кількість');

    await user.clear(quantity);
    await user.type(quantity, '0');

    expect(within(card).getByRole('button', { name: 'Виробляти' })).toBeDisabled();
    expect(production.enqueue).not.toHaveBeenCalled();
  });

  it('says that the recipes could not be loaded and tries again', async () => {
    const user = userEvent.setup();
    const production = fakeProductionApi({
      recipes: vi.fn().mockRejectedValueOnce(new Error('down')).mockResolvedValue(sampleProductionRecipes()),
    });
    renderPage('/planet/production', { auth: signedIn(), production });

    expect(await screen.findByText('Не вдалося завантажити виробництво.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Спробувати ще раз' }));

    expect(await screen.findByRole('region', { name: 'Рецепти' })).toBeInTheDocument();
  });
});
