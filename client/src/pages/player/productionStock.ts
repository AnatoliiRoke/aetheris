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

/**
 * What every ingredient of a recipe needs and what the body has, for the number of units the
 * player chose.
 *
 * @param recipe The recipe.
 * @param quantity How many units the player chose.
 * @param source The balances, the resources of the body and the stock of its parts.
 * @returns What every ingredient needs and what the body has.
 */
export function ingredientAvailability(recipe: { ingredients: ProductionIngredient[] }, quantity: number, source: BodyAvailability): IngredientAvailability[] {
  const { resources, parts } = availableAmounts(source);
  const units = Number.isFinite(quantity) && quantity > 0 ? quantity : 0;

  return recipe.ingredients.map((ingredient) => {
    const required = ingredient.amount * units;
    const available =
      ingredient.resourceTypeId !== null ? (resources.get(ingredient.resourceTypeId) ?? 0) : (parts.get(ingredient.partTypeId ?? 0) ?? 0);

    return {
      ingredient,
      required,
      available,
      missing: Math.max(0, required - available),
    };
  });
}
