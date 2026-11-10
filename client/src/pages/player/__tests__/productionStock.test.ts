import { describe, expect, it } from 'vitest';
import { sampleBalance, samplePlanet, sampleProductionStock } from '../../../test/renderPage';
import { availableAmounts, ingredientAvailability, outputQuantity, partQuantity } from '../productionStock';

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

describe('how many parts of one kind the body has (task 00049)', () => {
  it('adds up every stack of the part', () => {
    const stock = sampleProductionStock({
      stacks: [
        { partTypeId: 7, markLevel: 1, hasRaceBonus: false, conditionPercent: 100, quantity: 3, nameKey: 'production.part_type.part_7.name', imageKey: null, partGroupId: 2, partGroupNameKey: 'production.part_group.part_group_2.name', capsuleInhabitants: 0, capsuleUsable: false },
        { partTypeId: 7, markLevel: 1, hasRaceBonus: true, conditionPercent: 100, quantity: 2, nameKey: 'production.part_type.part_7.name', imageKey: null, partGroupId: 2, partGroupNameKey: 'production.part_group.part_group_2.name', capsuleInhabitants: 0, capsuleUsable: false },
      ],
    });

    expect(partQuantity(stock, 7)).toBe(5);
  });

  it('counts nothing for a part the body does not have', () => {
    expect(partQuantity(sampleProductionStock(), 999)).toBe(0);
  });

  it('counts nothing when the stock has not arrived', () => {
    expect(partQuantity(null, 3)).toBe(0);
  });
});

describe('how many of the thing a recipe makes the body has (task 00049)', () => {
  it('counts the parts of the stock of the body', () => {
    expect(outputQuantity(body, { partTypeId: 3, resourceTypeId: null })).toBe(4);
  });

  it('counts the resources of the body and the balances of the civilization', () => {
    expect(outputQuantity(body, { partTypeId: null, resourceTypeId: 1 })).toBe(900);
    expect(outputQuantity(body, { partTypeId: null, resourceTypeId: 18 })).toBe(234);
  });

  it('counts nothing when the body has no such resource', () => {
    expect(outputQuantity(body, { partTypeId: null, resourceTypeId: 99 })).toBe(0);
  });

  it('counts nothing when the recipe makes neither a part nor a resource', () => {
    expect(outputQuantity(body, { partTypeId: null, resourceTypeId: null })).toBeNull();
  });

  it('reads a field the server left out as no part at all (task 00049)', () => {
    // The server leaves out a field that is null, so a recipe of a resource has no part:
    // the field is missing, not null.
    const recipe = { partTypeId: undefined as unknown as number | null, resourceTypeId: 1 };

    expect(outputQuantity(body, recipe)).toBe(900);
  });
});
