import type { Fleet, ShipCatalog, ShipDesign } from '../../../api/fleetApi';

/**
 * The extraction of a fleet on the client (task 00050).
 *
 * A fleet extracts with the modules of extraction of its ships, so the order of extraction may be
 * offered only when a ship of the fleet carries such a module. The modules of extraction are the
 * ones of the group of extraction of the catalog.
 */

/**
 * The neutral code of the group of the modules of extraction (task 00050).
 */
export const EXTRACTION_GROUP_CODE = 'pg_8';

/**
 * Whether a project of a ship carries a module of extraction (task 00050).
 *
 * @param design The project of the ship.
 * @param catalog The catalog of the ships, which names the group of every module.
 * @returns Whether the project has a module of extraction.
 */
export function designExtracts(design: ShipDesign | undefined, catalog: ShipCatalog | null): boolean {
  if (design === undefined || catalog === null) {
    return false;
  }

  const groups = new Map(catalog.modules.map((module) => [module.id, module.partGroupCode]));

  return design.slots.some((slot) => groups.get(slot.partTypeId) === EXTRACTION_GROUP_CODE);
}

/**
 * Whether a fleet may extract (task 00050): one of its ships has a module of extraction.
 *
 * @param fleet The fleet.
 * @param designs The projects of the ships of the player.
 * @param catalog The catalog of the ships, which names the group of every module.
 * @returns Whether the fleet may extract.
 */
export function fleetExtracts(
  fleet: Fleet | null,
  designs: readonly ShipDesign[],
  catalog: ShipCatalog | null,
): boolean {
  if (fleet === null || catalog === null) {
    return false;
  }

  const byId = new Map(designs.map((design) => [design.id, design]));

  return fleet.ships.some((ship) => designExtracts(byId.get(ship.designId), catalog));
}
