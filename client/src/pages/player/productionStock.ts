import type { CurrencyBalanceFacts } from '../../api/economyApi';
import type { Planet } from '../../api/planetApi';
import type { ProductionIngredient, ProductionStock } from '../../api/productionApi';

/**
 * What a body has for one ingredient of a recipe: the amount the ingredient needs and the amount
 * the body has.
 */
export interface IngredientAvailability {
  /** The ingredient of the recipe. */
  ingredient: ProductionIngredient;
  /** How many of the ingredient the batch needs, with the number of units the player chose. */
  required: number;
  /** How many of the ingredient the body has now. */
  available: number;
  /** How many of the ingredient are missing; zero when the body has enough. */
  missing: number;
}

/**
 * The things a body has: the balances of the civilization, the stock of the body and the parts of
 * its stock.
 */
export interface BodyAvailability {
  /** The balances of the currencies of the civilization. */
  balances: readonly CurrencyBalanceFacts[];
  /** The resources of the body. */
  planet: Planet | null;
  /** The stock of the parts of the body. */
  stock: ProductionStock | null;
}

/**
 * The amounts a body has, by resource and by part.
 *
 * @param source The balances, the resources of the body and the stock of its parts.
 * @returns The amount of every resource and every part of the body.
 */
export function availableAmounts(source: BodyAvailability): { resources: Map<number, number>; parts: Map<number, number> } {
  const resources = new Map<number, number>();
  const parts = new Map<number, number>();

  // The resources of the body lie in its stock, the balances of the civilization are the currencies.
  for (const resource of source.planet?.resources ?? []) {
    resources.set(resource.resourceTypeId, resource.amount);
  }

  for (const balance of source.balances) {
    resources.set(balance.resourceTypeId, balance.amount);
  }

  // The parts of the body lie in the stacks of its stock: every stack of a kind adds up.
  for (const stack of source.stock?.stacks ?? []) {
    parts.set(stack.partTypeId, (parts.get(stack.partTypeId) ?? 0) + stack.quantity);
  }

  return { resources, parts };
}

/** Credits stand first on every recipe plaque. */
const creditsCode = 'resource_27';

/** Population stands second, after the credits. */
const populationCode = 'resource_30';

/**
 * The catalog code carried by a localisation key, such as `resource_27` or `part_3`.
 *
 * @param nameKey The localisation key of the ingredient.
 * @returns The code, or `null` when the key has none.
 */
function catalogCode(nameKey: string): string | null {
  const matched = /((?:resource|part)_\d+)/.exec(nameKey);

  return matched?.[1] ?? null;
}

/**
 * Where an ingredient stands on a recipe plaque: credits, then population, then the other
 * resources in the order of the catalog, then the parts.
 *
 * @param code The catalog code of the ingredient, or `null` for a part named only by its id.
 * @param partTypeId The part, or `null` when the ingredient is a resource.
 * @returns The group and the place inside the group. A smaller pair stands higher.
 */
function ingredientPlace(code: string | null, partTypeId: number | null): readonly [number, number] {
  if (code === creditsCode) {
    return [0, 0];
  }

  if (code === populationCode) {
    return [1, 0];
  }

  const resourceNumber = code === null ? null : /^resource_(\d+)$/.exec(code);

  if (resourceNumber) {
    return [2, Number(resourceNumber[1])];
  }

  const partNumber = code === null ? null : /^part_(\d+)$/.exec(code);

  return [3, partNumber ? Number(partNumber[1]) : (partTypeId ?? 0)];
}

/**
 * Compares two ingredients for the one order of every recipe plaque.
 *
 * @param left The first ingredient.
 * @param right The second ingredient.
 * @returns A negative number when the first stands higher.
 */
export function compareRecipeIngredients(
  left: { nameKey?: string | null; code?: string | null; partTypeId: number | null },
  right: { nameKey?: string | null; code?: string | null; partTypeId: number | null },
): number {
  const leftPlace = ingredientPlace(left.code ?? (left.nameKey ? catalogCode(left.nameKey) : null), left.partTypeId);
  const rightPlace = ingredientPlace(right.code ?? (right.nameKey ? catalogCode(right.nameKey) : null), right.partTypeId);

  return leftPlace[0] - rightPlace[0] || leftPlace[1] - rightPlace[1];
}

/**
 * Puts resource costs in the one plaque order: credits, population, then the catalog code.
 * Display only. The list the server sent stays as it was.
 *
 * @param costs The costs to show.
 * @param codeByResourceId The catalog code of a resource, or `null` when the catalog has none.
 * @returns A copy of the costs in the plaque order.
 */
export function orderCatalogCosts<T extends { resourceTypeId: number }>(
  costs: readonly T[],
  codeByResourceId: (resourceTypeId: number) => string | null,
): T[] {
  return [...costs].sort((left, right) =>
    compareRecipeIngredients(
      { code: codeByResourceId(left.resourceTypeId), partTypeId: null },
      { code: codeByResourceId(right.resourceTypeId), partTypeId: null },
    ),
  );
}

/**
 * What every ingredient of a recipe needs and what the body has, for the number of units the
 * player chose. The rows stand in one order: credits, population, the other resources, the parts.
 *
 * @param recipe The recipe.
 * @param quantity How many units the player chose.
 * @param source The balances, the resources of the body and the stock of its parts.
 * @returns What every ingredient needs and what the body has.
 */
export function ingredientAvailability(recipe: { ingredients: ProductionIngredient[] }, quantity: number, source: BodyAvailability): IngredientAvailability[] {
  const { resources, parts } = availableAmounts(source);
  const units = Number.isFinite(quantity) && quantity > 0 ? quantity : 0;

  return recipe.ingredients
    .map((ingredient) => {
      const required = ingredient.amount * units;
      const available =
        ingredient.resourceTypeId !== null ? (resources.get(ingredient.resourceTypeId) ?? 0) : (parts.get(ingredient.partTypeId ?? 0) ?? 0);

      return {
        ingredient,
        required,
        available,
        missing: Math.max(0, required - available),
      };
    })
    .sort((left, right) => compareRecipeIngredients(left.ingredient, right.ingredient));
}
