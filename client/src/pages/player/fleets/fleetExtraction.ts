import type { Fleet, ShipCatalog, ShipDesign } from '../../../api/fleetApi';

/**
 * The extraction of a fleet on the client (task 00050, task 00055).
 *
 * A fleet extracts with the modules of extraction of its ships, so the order of extraction may be
 * offered only when a ship of the fleet carries such a module. A module of extraction is one that
 * the server says takes from some kind of place (the rows of the module in the catalog); the client
 * knows no module and no group of modules.
 */

/**
 * Whether a project of a ship carries a module of extraction (task 00050).
 *
 * @param design The project of the ship.
 * @param catalog The catalog of the ships, which says what every module takes.
 * @returns Whether the project has a module of extraction.
 */
export function designExtracts(design: ShipDesign | undefined, catalog: ShipCatalog | null): boolean {
  if (design === undefined || catalog === null) {
    return false;
  }

  const extracting = new Set(catalog.modules.filter((module) => (module.extractionPlaceKinds ?? []).length > 0).map((module) => module.id));

  return design.slots.some((slot) => extracting.has(slot.partTypeId));
}

/**
 * Whether a fleet may extract (task 00050): one of its ships has a module of extraction.
 *
 * @param fleet The fleet.
 * @param designs The projects of the ships of the player.
 * @param catalog The catalog of the ships, which says what every module takes.
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

/**
 * The orders of a flight plan that the modules of a fleet open (task 00058): a ship carries a module that the
 * server marks with the order it opens (the hyperdrive, the jump drive, the scanners). The client knows no module:
 * it reads the mark from the catalog.
 *
 * @param fleet The fleet.
 * @param designs The projects of the ships of the player.
 * @param catalog The catalog of the ships, which says what every module opens.
 * @returns The codes of the orders the fleet may be given because of its modules.
 */
export function fleetOpenedOrders(fleet: Fleet | null, designs: readonly ShipDesign[], catalog: ShipCatalog | null): ReadonlySet<string> {
  const opened = new Set<string>();

  if (fleet === null || catalog === null) {
    return opened;
  }

  const orderOfModule = new Map(catalog.modules.flatMap((module) => (module.flightOrder ? [[module.id, module.flightOrder] as const] : [])));
  const byId = new Map(designs.map((design) => [design.id, design]));

  for (const ship of fleet.ships) {
    for (const slot of byId.get(ship.designId)?.slots ?? []) {
      const order = orderOfModule.get(slot.partTypeId);

      if (order !== undefined) {
        opened.add(order);
      }
    }
  }

  return opened;
}

/** The best radius and duration of the fleet's system scanners. */
export function fleetScanCapabilities(fleet: Fleet | null, designs: readonly ShipDesign[], catalog: ShipCatalog | null): { radius: number; duration: number } | null {
  if (!fleet || !catalog) return null;
  const byDesign = new Map(designs.map((design) => [design.id, design]));
  const fitted = new Set(fleet.ships.flatMap((ship) => (byDesign.get(ship.designId)?.slots ?? []).map((slot) => slot.partTypeId)));
  const scanners = catalog.modules.filter((module) => fitted.has(module.id) && module.scanTarget === 'systems').map((module) => ({
    radius: module.facts?.find((fact) => fact.kind === 'scan_radius')?.amount ?? 0,
    duration: module.facts?.find((fact) => fact.kind === 'scan_duration')?.amount ?? 0,
  })).filter((scanner) => scanner.radius > 0 && scanner.duration > 0);
  return scanners.length ? { radius: Math.max(...scanners.map((entry) => entry.radius)), duration: Math.min(...scanners.map((entry) => entry.duration)) } : null;
}
