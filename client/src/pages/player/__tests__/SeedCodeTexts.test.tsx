import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeProductionApi,
  fakeTextsApi,
  renderPage,
  renderPanel,
  sampleCharacteristics,
  samplePartCharacteristics,
  sampleProductionPartRecipe,
  sampleProductionRecipes,
  sampleTexts,
  signedInMe,
} from '../../../test/renderPage';
import { useTexts } from '../../../texts/useTexts';
import { resetCharacteristicsCache, useCharacteristics } from '../useCharacteristics';

/**
 * The codes of the starting data of the server (task 00052): the groups of the parts are
 * `pg_<n>`, the modules `part_m_<n>`, the hulls `part_h_<n>`, the recipes `recipe_<n>` and
 * `recipe_r_<n>`, the characteristics `char_<name>`. The keys of the texts are built from them.
 */
const seedKeys = {
  group: 'production.part_group.pg_2.name',
  part: 'production.part_type.part_m_8.name',
  recipe: 'production.recipe.recipe_r_113.name',
  thrust: 'production.characteristic.char_thrust',
  fuel: 'production.characteristic.char_fuel_use',
};

/**
 * The pack of the texts the server sends for a database made from the starting data: the keys
 * hold the codes of the file, and no key of the older counter form (`part_3`, `characteristic_1`)
 * is in it.
 */
const seedPack: Record<string, Record<string, string>> = {
  uk: {
    ...Object.fromEntries(Object.entries(sampleTexts.uk).filter(([key]) => !key.startsWith('production.characteristic.') && !key.startsWith('production.part_type.'))),
    [seedKeys.group]: 'Реактори',
    [seedKeys.part]: 'Корпус «Оса» Мк. II',
    [seedKeys.recipe]: 'Реактор великий',
    [`${seedKeys.recipe.slice(0, -'.name'.length)}.description`]: 'Великий реактор для корабля.',
    [`${seedKeys.thrust}.name`]: 'Тяга',
    [`${seedKeys.thrust}.description`]: 'Сила тяги рушія.',
    [`${seedKeys.thrust}.unit`]: 'кН',
    [`${seedKeys.fuel}.name`]: 'Витрата палива',
    [`${seedKeys.fuel}.description`]: 'Скільки палива рушій витрачає.',
    [`${seedKeys.fuel}.unit`]: 'т/год',
  },
};

/**
 * Shows what the screens of the production draw from the texts of the server: the name and the unit
 * of every characteristic of the part, the name of the part and the name of its group.
 */
function Probe() {
  const characteristics = useCharacteristics();
  const { name } = useTexts();

  if (!characteristics.loaded) {
    return null;
  }

  return (
    <ul aria-label="probe">
      {characteristics.rows(3).map((row) => (
        <li key={row.characteristicId}>{[characteristics.name(row.characteristicId), characteristics.unit(row.characteristicId)].join(' ')}</li>
      ))}
      <li>{name(seedKeys.part)}</li>
      <li>{name(seedKeys.group)}</li>
    </ul>
  );
}

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('the names of the production from a pack with the codes of the starting data (task 00052)', () => {
  beforeEach(() => {
    resetCharacteristicsCache();
  });

  it('names the characteristics, the part and the group of the part, not their keys', async () => {
    const production = fakeProductionApi({
      characteristics: vi.fn().mockResolvedValue(
        sampleCharacteristics([
          { code: 'char_thrust', nameKey: `${seedKeys.thrust}.name`, descriptionKey: `${seedKeys.thrust}.description`, unitKey: `${seedKeys.thrust}.unit` },
          { code: 'char_fuel_use', nameKey: `${seedKeys.fuel}.name`, descriptionKey: `${seedKeys.fuel}.description`, unitKey: `${seedKeys.fuel}.unit` },
        ]),
      ),
      characteristicParts: vi.fn().mockResolvedValue(samplePartCharacteristics([{ code: 'part_m_8', nameKey: seedKeys.part }])),
    });

    renderPanel(<Probe />, { auth: signedIn(), production, texts: fakeTextsApi(seedPack) });

    const probe = await screen.findByRole('list', { name: 'probe' });

    expect(await within(probe).findByText('Тяга кН')).toBeInTheDocument();
    expect(within(probe).getByText('Витрата палива т/год')).toBeInTheDocument();
    expect(within(probe).getByText('Корпус «Оса» Мк. II')).toBeInTheDocument();
    expect(within(probe).getByText('Реактори')).toBeInTheDocument();
    expect(document.body.textContent).not.toContain('production.');
  });

  it('shows the name of a recipe with a seed code and the name of its group, not their keys', async () => {
    const user = userEvent.setup();
    const list = sampleProductionRecipes();
    const production = fakeProductionApi({
      recipes: vi.fn().mockResolvedValue({
        ...list,
        recipes: [
          ...list.recipes,
          { ...sampleProductionPartRecipe(3, 2), recipeId: 113, code: 'recipe_r_113', nameKey: seedKeys.recipe, descriptionKey: `${seedKeys.recipe.slice(0, -'.name'.length)}.description` },
        ],
      }),
    });

    renderPage('/planet/production', { auth: signedIn(), production, texts: fakeTextsApi(seedPack) });

    const recipes = await screen.findByRole('region', { name: 'Рецепти' });
    await within(recipes).findByText('Будматеріали');
    await user.click(within(recipes).getByRole('tab', { name: 'Модулі кораблів' }));

    expect(await within(recipes).findByText('Реактор великий')).toBeInTheDocument();
    expect(within(within(recipes).getByRole('tablist', { name: 'Групи рецептів' })).getByRole('tab', { name: 'Реактори' })).toBeInTheDocument();
    expect(document.body.textContent).not.toContain('production.recipe');
    expect(document.body.textContent).not.toContain('production.part_group');
  });
});
