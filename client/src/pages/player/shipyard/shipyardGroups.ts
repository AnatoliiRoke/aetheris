import type { FinishedShip } from '../../../api/fleetApi';

/**
 * The grouping of the finished ships on the shipyard (task 00039, the new look of the block of the
 * ships).
 *
 * The ships are not shown one by one: they are shown by the class of their hull (the group of the
 * hull: the light ones, the heavy ones, the civil ones) and, under the class, by the project they
 * were built from, with how many ships of the project there are.
 */

/**
 * The ships of one project: the ships a player has of one project, not one by one.
 */
export interface ShipyardProjectGroup {
  designId: number;
  name: string;
  hullNameKey: string;
  ships: FinishedShip[];
}

/**
 * The projects of one class of hulls.
 */
export interface ShipyardClassGroup {
  classNameKey: string;
  projects: ShipyardProjectGroup[];
}

/**
 * Groups ships by the class of the hull and, inside a class, by the project they were built from.
 * The classes come in the order of their keys and the projects in the order of their names, so the
 * screen does not jump when a ship comes or goes.
 *
 * @param ships The ships.
 * @returns The groups of the ships.
 */
export function groupShipsByClass(ships: readonly FinishedShip[]): ShipyardClassGroup[] {
  const classes = new Map<string, Map<number, ShipyardProjectGroup>>();

  for (const ship of ships) {
    const projects = classes.get(ship.classNameKey) ?? new Map<number, ShipyardProjectGroup>();
    const project = projects.get(ship.designId) ?? { designId: ship.designId, name: ship.name, hullNameKey: ship.hullNameKey, ships: [] };

    project.ships.push(ship);
    projects.set(ship.designId, project);
    classes.set(ship.classNameKey, projects);
  }

  return [...classes]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([classNameKey, projects]) => ({
      classNameKey,
      projects: [...projects.values()]
        .map((project) => ({ ...project, ships: [...project.ships].sort((left, right) => left.id - right.id) }))
        .sort((left, right) => left.name.localeCompare(right.name) || left.designId - right.designId),
    }));
}
