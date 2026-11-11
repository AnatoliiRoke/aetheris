import { describe, expect, it } from 'vitest';
import { sampleFleet, sampleFleetShip, sampleShipCatalog, sampleShipDesign, sampleShipModule } from '../../../test/renderPage';
import { designExtracts, fleetExtracts } from '../fleets/fleetExtraction';

/**
 * A project whose slots hold the given modules.
 */
function designOf(...partTypeIds: number[]) {
  return sampleShipDesign({
    id: 7,
    slots: partTypeIds.map((partTypeId, slotIndex) => ({ slotIndex, partTypeId, markLevel: 1, hasRaceBonus: false })),
  });
}

describe('which fleets may extract (task 00055)', () => {
  const drive = sampleShipModule({ id: 1, code: 'drive', partGroupCode: 'group_drives' });
  const extractor = sampleShipModule({ id: 2, code: 'extractor', partGroupCode: 'any_group', extractionPlaceKinds: ['nebula', 'gas_giant'] });
  const catalog = sampleShipCatalog({ modules: [drive, extractor] });

  it('takes a module as a module of extraction when the server says what it takes, whatever its group is', () => {
    expect(designExtracts(designOf(1, 2), catalog)).toBe(true);
    expect(designExtracts(designOf(1), catalog)).toBe(false);
    expect(designExtracts(designOf(), catalog)).toBe(false);
  });

  it('does not take a module with no kinds of places for a module of extraction', () => {
    const none = sampleShipCatalog({ modules: [sampleShipModule({ id: 2, extractionPlaceKinds: [] })] });

    expect(designExtracts(designOf(2), none)).toBe(false);
  });

  it('knows nothing without the project or the catalog', () => {
    expect(designExtracts(undefined, catalog)).toBe(false);
    expect(designExtracts(designOf(2), null)).toBe(false);
  });

  it('lets a fleet extract when one of its ships has a module of extraction', () => {
    const fleet = sampleFleet({ ships: [sampleFleetShip({ id: 1, designId: 7 })] });

    expect(fleetExtracts(fleet, [designOf(1, 2)], catalog)).toBe(true);
    expect(fleetExtracts(fleet, [designOf(1)], catalog)).toBe(false);
    expect(fleetExtracts(null, [designOf(1, 2)], catalog)).toBe(false);
    expect(fleetExtracts(fleet, [designOf(1, 2)], null)).toBe(false);
  });
});
