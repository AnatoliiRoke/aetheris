import { describe, expect, it } from 'vitest';
import { sampleBalance, samplePlanet, sampleProductionStock } from '../../../test/renderPage';
import { availableAmounts, ingredientAvailability } from '../productionStock';

/**
 * The body of the tests: the balances of the civilization, the resources of the body and the stock
 * of its parts.
 */
const body = {
  balances: [sampleBalance({ resourceTypeId: 18, amount: 234 })],
  planet: samplePlanet(),
  stock: sampleProductionStock(),
};

describe('what a body has for the ingredients of a recipe', () => {
  it('adds the resources of the body, the balances of the civilization and the stacks of the parts', () => {
    const amounts = availableAmounts(body);

    // The resources of the body: iron and copper.
    expect(amounts.resources.get(1)).toBe(900);
    expect(amounts.resources.get(3)).toBe(500);
    // The balance of the civilization: the credits.
    expect(amounts.resources.get(18)).toBe(234);
    // The parts of the body lie in the stacks of its stock.
    expect(amounts.parts.get(3)).toBe(4);
  });

  it('multiplies the ingredient of a unit by the number of the units', () => {
    const [entry] = ingredientAvailability({ ingredients: [{ resourceTypeId: 1, partTypeId: null, amount: 4, nameKey: 'world.resource.resource_1.name' }] }, 300, body);

    expect(entry.required).toBe(1200);
    expect(entry.available).toBe(900);
    expect(entry.missing).toBe(300);
  });

  it('counts no missing amount when the body has enough', () => {
    const [entry] = ingredientAvailability({ ingredients: [{ resourceTypeId: 1, partTypeId: null, amount: 4, nameKey: 'world.resource.resource_1.name' }] }, 1, body);

    expect(entry.required).toBe(4);
    expect(entry.missing).toBe(0);
  });

  it('counts the parts of the stock of the body', () => {
    const [entry] = ingredientAvailability({ ingredients: [{ resourceTypeId: null, partTypeId: 3, amount: 2, nameKey: 'production.part.part_3.name' }] }, 4, body);

    expect(entry.required).toBe(8);
    expect(entry.available).toBe(4);
    expect(entry.missing).toBe(4);
  });

  it('needs nothing when the number of the units is not a whole positive one', () => {
    const [entry] = ingredientAvailability({ ingredients: [{ resourceTypeId: 1, partTypeId: null, amount: 4, nameKey: 'world.resource.resource_1.name' }] }, 0, body);

    expect(entry.required).toBe(0);
    expect(entry.missing).toBe(0);
  });

  it('counts the missing amount when the body has nothing at all', () => {
    const [entry] = ingredientAvailability({ ingredients: [{ resourceTypeId: 99, partTypeId: null, amount: 7, nameKey: 'world.resource.resource_99.name' }] }, 2, {
      balances: [],
      planet: null,
      stock: null,
    });

    expect(entry.required).toBe(14);
    expect(entry.available).toBe(0);
    expect(entry.missing).toBe(14);
  });
});
