import type { ProductionFactorySummary, ProductionReason, ProductionRecipe } from '../../api/productionApi';
import type { Language } from '../../i18n/context';
import type { EntityText } from '../../texts/context';
import { lookup } from './planetText';

/**
 * The value of a filter that keeps everything.
 */
export const allRecipes = 'all';

/**
 * The value of the filter by group that keeps the recipes of the resources, which have no group of parts.
 */
export const resourcesGroupKey = 'resources';

/**
 * The chips of the filter by availability: the identifier of the chip and what it keeps.
 */
export const availabilityFilters = [
  { id: 'available', isAvailable: true },
  { id: 'closed', isAvailable: false },
] as const;

/**
 * What the player chose in the filters of the production screen.
 */
export interface RecipeFilter {
  /** The text in the search: a part of the name or the description. */
  query: string;
  /** The identifier of the chip of the availability, or {@link allRecipes}. */
  availability: string;
  /** The type of the factory, or {@link allRecipes}. */
  factory: string;
  /** The identifier of the group of the parts, {@link resourcesGroupKey} or {@link allRecipes}. */
  group: string;
}

/**
 * The key a recipe is grouped by: the group of its part, or {@link resourcesGroupKey}.
 *
 * @param recipe The recipe.
 */
export function groupKeyOf(recipe: ProductionRecipe): string {
  return recipe.partGroupId === null ? resourcesGroupKey : String(recipe.partGroupId);
}

/**
 * Keeps the recipes that fit every filter.
 *
 * @param recipes All the recipes.
 * @param filter What the player chose.
 * @param textOf The name and the description of a recipe, joined.
 */
export function filterRecipes(recipes: ProductionRecipe[], filter: RecipeFilter, textOf: (recipe: ProductionRecipe) => string): ProductionRecipe[] {
  const needle = filter.query.trim().toLocaleLowerCase();
  const availability = availabilityFilters.find((entry) => entry.id === filter.availability) ?? null;

  return recipes.filter(
    (recipe) =>
      (availability === null || recipe.isAvailable === availability.isAvailable) &&
      (filter.factory === allRecipes || String(recipe.buildingTypeId) === filter.factory) &&
      (filter.group === allRecipes || groupKeyOf(recipe) === filter.group) &&
      (needle === '' || textOf(recipe).toLocaleLowerCase().includes(needle)),
  );
}

/**
 * The recipes grouped by the group of the part they make, in the order the groups first appear.
 *
 * @param recipes The recipes, in the order of the server.
 */
export function groupRecipes(recipes: ProductionRecipe[]): { key: string; nameKey: string | null; recipes: ProductionRecipe[] }[] {
  const groups = new Map<string, { key: string; nameKey: string | null; recipes: ProductionRecipe[] }>();

  for (const recipe of recipes) {
    const key = groupKeyOf(recipe);
    const group = groups.get(key) ?? { key, nameKey: recipe.partGroupNameKey, recipes: [] };

    group.recipes.push(recipe);
    groups.set(key, group);
  }

  return [...groups.values()];
}

/**
 * The factory of the body that makes a recipe: the one of the type the recipe names, if the body has it.
 *
 * @param factories The factories of the body.
 * @param recipe The recipe.
 */
export function factoryOf(factories: ProductionFactorySummary[], recipe: ProductionRecipe): ProductionFactorySummary | null {
  return factories.find((factory) => factory.buildingTypeId === recipe.buildingTypeId && factory.level > 0) ?? null;
}

/**
 * The text of a reason why a recipe is closed, with the name and the level of what it needs.
 *
 * @param language The language of the interface.
 * @param reason The reason.
 * @param text The texts of the entities.
 */
export function reasonText(language: Language, reason: ProductionReason, text: EntityText): string {
  const name = reason.targetNameKey ? (text(reason.targetNameKey) ?? reason.targetNameKey) : String(reason.targetId);

  return lookup(language, `reason.${reason.code}`, reason.code, { name, level: reason.targetLevel });
}
