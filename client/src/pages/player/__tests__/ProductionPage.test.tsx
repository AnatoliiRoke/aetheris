import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, fakeProductionApi, renderPage, sampleProductionPartRecipe, sampleProductionRecipes, signedInMe } from '../../../test/renderPage';

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
    expect(within(recipes).getByText('Будматеріали')).toBeInTheDocument();
    // The sections are the first level of the tabs: the resources, the modules of the ships, the
    // hulls of the ships and the modules of the base.
    const sections = within(recipes).getByRole('tablist', { name: 'Розділи виробництва' });

    expect(within(sections).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Ресурси', 'Модулі кораблів']);
    expect(within(recipes).queryByText('Корпусний модуль')).toBeNull();
    expect(within(recipes).queryByText('Реактор малий')).toBeNull();
    // The cost of a unit is a list of resources one by one, like on the other screens, and the time of a unit.
    const materials = within(within(recipes).getByText('Будматеріали').closest('li')!);
    const cost = materials.getByRole('list', { name: 'Потрібні ресурси' });
    expect(within(cost).getByText('Залізо')).toBeInTheDocument();
    expect(within(cost).getByText('4')).toBeInTheDocument();
    expect(materials.getByText('Час виробництва')).toBeInTheDocument();
    expect(within(recipes).queryByText(/На одиницю/)).toBeNull();
    // The description of a recipe is not on the tile.
    expect(within(recipes).queryByText('Плити й балки з сировини.')).toBeNull();
    // The recipe of the factory the body lacks is closed, and it is in the modules of the ships.
    expect(within(recipes).queryByText('Спершу збудуй: Верф')).toBeNull();
  });

  it('shows the modules of the ships in the second section, with the groups of their parts', async () => {
    const user = userEvent.setup();
    renderPage('/planet/production', { auth: signedIn() });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    await within(recipes).findByText('Будматеріали');

    await user.click(within(recipes).getByRole('tab', { name: 'Модулі кораблів' }));

    expect(within(recipes).getByText('Корпусний модуль')).toBeInTheDocument();
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();
    expect(within(recipes).queryByText('Будматеріали')).toBeNull();
    // The recipe of the factory the body lacks is closed with the reason that names it.
    expect(within(recipes).getByText('Спершу збудуй: Верф')).toBeInTheDocument();

    // The groups of the section are the second level of the tabs.
    const groups = within(recipes).getByRole('tablist', { name: 'Групи рецептів' });

    expect(within(groups).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Усі', 'Реактори', 'Інші корабельні']);
  });

  it('filters the recipes by the search, the availability, the factory and the group', async () => {
    const user = userEvent.setup();
    renderPage('/planet/production', { auth: signedIn() });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    await within(recipes).findByText('Будматеріали');

    // The sections of the screen keep the recipes of the other sections out of the way.
    await user.click(within(recipes).getByRole('tab', { name: 'Модулі кораблів' }));
    await within(recipes).findByText('Реактор малий');

    await user.type(within(recipes).getByRole('searchbox'), 'реактор');
    expect(within(recipes).queryByText('Корпусний модуль')).toBeNull();
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();
    await user.clear(within(recipes).getByRole('searchbox'));

    // Both recipes of the ships are closed, so the filter of the available ones leaves none.
    await user.click(within(recipes).getByRole('button', { name: 'Доступні' }));
    expect(within(recipes).queryByText('Реактор малий')).toBeNull();
    expect(within(recipes).queryByText('Корпусний модуль')).toBeNull();
    await user.click(within(recipes).getByRole('button', { name: 'Усі' }));

    await user.selectOptions(within(recipes).getByLabelText('Завод'), 'Верф');
    expect(within(recipes).queryByText('Корпусний модуль')).toBeNull();
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();
    await user.selectOptions(within(recipes).getByLabelText('Завод'), 'Усі заводи');

    await user.type(within(recipes).getByRole('searchbox'), 'немає такого');
    expect(await within(recipes).findByText('Жоден рецепт не підходить під фільтри.')).toBeInTheDocument();
  });

  it('switches the sections and the groups of the recipes with the two levels of tabs', async () => {
    const user = userEvent.setup();
    renderPage('/planet/production', { auth: signedIn() });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    await within(recipes).findByText('Будматеріали');

    const sections = within(recipes).getByRole('tablist', { name: 'Розділи виробництва' });

    expect(within(sections).getByRole('tab', { name: 'Ресурси' })).toHaveAttribute('aria-selected', 'true');
    expect(within(recipes).queryByLabelText('Група')).toBeNull();

    await user.click(within(sections).getByRole('tab', { name: 'Модулі кораблів' }));

    expect(within(sections).getByRole('tab', { name: 'Модулі кораблів' })).toHaveAttribute('aria-selected', 'true');
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();
    expect(within(recipes).queryByText('Будматеріали')).toBeNull();

    // The second level of the tabs holds the groups of the active section.
    const groups = within(recipes).getByRole('tablist', { name: 'Групи рецептів' });

    expect(within(groups).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Усі', 'Реактори', 'Інші корабельні']);

    await user.click(within(groups).getByRole('tab', { name: 'Реактори' }));

    expect(within(groups).getByRole('tab', { name: 'Реактори' })).toHaveAttribute('aria-selected', 'true');
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();
    expect(within(recipes).queryByText('Корпусний модуль')).toBeNull();
  });

  it('builds the sections and the tabs from the tree of the groups, with the weapons split into the energy, the kinetic and the ammunition (task 00043)', async () => {
    const user = userEvent.setup();
    const base = sampleProductionRecipes();
    const production = fakeProductionApi({
      recipes: vi.fn().mockResolvedValue({
        ...base,
        recipes: [...base.recipes, sampleProductionPartRecipe(4, 3), sampleProductionPartRecipe(5, 4), sampleProductionPartRecipe(7, 7), sampleProductionPartRecipe(6, 12)],
      }),
    });
    renderPage('/planet/production', { auth: signedIn(), production });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    await within(recipes).findByText('Будматеріали');

    // The sections are the groups of the top level that hold recipes.
    const sections = within(recipes).getByRole('tablist', { name: 'Розділи виробництва' });

    expect(within(sections).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Ресурси', 'Модулі кораблів', 'Корпуси кораблів']);

    await user.click(within(sections).getByRole('tab', { name: 'Модулі кораблів' }));

    // The weapons are a tab of the modules, and the row of the weapons is shown only inside it.
    const groups = within(recipes).getByRole('tablist', { name: 'Групи рецептів' });

    expect(within(groups).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Усі', 'Реактори', 'Зброя', 'Інші корабельні']);
    expect(within(recipes).queryByRole('tablist', { name: 'Підгрупи рецептів' })).toBeNull();
    expect(within(recipes).getByText('Легкий лазер')).toBeInTheDocument();
    expect(within(recipes).getByText('РПУ-X1')).toBeInTheDocument();
    expect(within(recipes).queryByText('Корпус «Шершень»')).toBeNull();

    await user.click(within(groups).getByRole('tab', { name: 'Зброя' }));

    const kinds = within(recipes).getByRole('tablist', { name: 'Підгрупи рецептів' });

    expect(within(kinds).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Усі', 'Енергетична зброя', 'Кінетична зброя', 'Боєприпаси']);
    expect(within(recipes).getByText('Легкий лазер')).toBeInTheDocument();
    expect(within(recipes).getByText('РПУ-X1')).toBeInTheDocument();
    expect(within(recipes).queryByText('Реактор малий')).toBeNull();

    await user.click(within(kinds).getByRole('tab', { name: 'Кінетична зброя' }));

    expect(within(kinds).getByRole('tab', { name: 'Кінетична зброя' })).toHaveAttribute('aria-selected', 'true');
    expect(within(recipes).getByText('РПУ-X1')).toBeInTheDocument();
    expect(within(recipes).queryByText('Легкий лазер')).toBeNull();
    expect(within(recipes).queryByText('Снаряди')).toBeNull();

    await user.click(within(kinds).getByRole('tab', { name: 'Боєприпаси' }));

    expect(within(recipes).getByText('Снаряди')).toBeInTheDocument();
    expect(within(recipes).queryByText('РПУ-X1')).toBeNull();

    await user.click(within(kinds).getByRole('tab', { name: 'Енергетична зброя' }));

    expect(within(recipes).getByText('Легкий лазер')).toBeInTheDocument();
    expect(within(recipes).queryByText('РПУ-X1')).toBeNull();

    // Another tab of the modules takes the row of the weapons away.
    await user.click(within(groups).getByRole('tab', { name: 'Реактори' }));

    expect(within(recipes).queryByRole('tablist', { name: 'Підгрупи рецептів' })).toBeNull();
    expect(within(recipes).getByText('Реактор малий')).toBeInTheDocument();

    await user.click(within(sections).getByRole('tab', { name: 'Корпуси кораблів' }));

    expect(within(recipes).getByText('Корпус «Шершень»')).toBeInTheDocument();
    expect(within(recipes).queryByText('РПУ-X1')).toBeNull();
    // The tabs switch on the data already read: the recipes are not read again.
    expect(vi.mocked(production.recipes).mock.calls.length).toBe(1);
  });

  it('keeps the recipes of the resources in their section with their names, and no empty section (task 00043)', async () => {
    const base = sampleProductionRecipes();
    const production = fakeProductionApi({
      recipes: vi.fn().mockResolvedValue({ ...base, recipes: base.recipes.map((recipe) => (recipe.partGroupCode === null ? { ...recipe, partGroupCode: undefined as unknown as null } : recipe)) }),
    });
    renderPage('/planet/production', { auth: signedIn(), production });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    await within(recipes).findByText('Будматеріали');

    const sections = within(recipes).getByRole('tablist', { name: 'Розділи виробництва' });

    expect(within(sections).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Ресурси', 'Модулі кораблів']);
    expect(within(recipes).queryByText(/undefined/)).toBeNull();
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
