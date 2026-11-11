import type { Fleet, FleetShip, FleetsView } from '../../../api/fleetApi';
import type { Language } from '../../../i18n/context';
import type { MessageKey } from '../../../i18n/uk';
import { hasKey } from '../planetText';

/**
 * The filter of the state of a fleet. The states are what the server says about a fleet and about
 * the credits of the civilization; the screen only groups them.
 */
export type StateFilter = 'all' | 'ready' | 'flying' | 'noCredits';

/**
 * The text of the state of a fleet. A civilization without credits cannot fly, whatever else the
 * fleet does; every other state is a code the server names, turned into a word of the language.
 *
 * @param language The language of the player.
 * @param translate The translator of the interface.
 * @param fleet The fleet.
 * @param hasCredits Whether the civilization has credits.
 */
export function stateText(
  language: Language,
  translate: (key: MessageKey) => string,
  fleet: Fleet,
  hasCredits: boolean,
): string {
  if (!hasCredits) {
    return translate('fleets.state.noCredits');
  }

  const key = `fleets.state.${fleet.stateCode}`;

  return hasKey(language, key) ? translate(key) : fleet.stateCode;
}

/**
 * Whether a fleet passes the filter of the state.
 *
 * @param filter The filter.
 * @param hasCredits Whether the civilization has credits.
 * @param fleet The fleet; the filter of the flights looks at its state.
 */
export function passesState(filter: StateFilter, hasCredits: boolean, fleet: Fleet): boolean {
  if (filter === 'noCredits') {
    return !hasCredits;
  }

  if (filter === 'flying') {
    return fleet.stateCode === 'flying';
  }

  return filter === 'ready' ? hasCredits : true;
}

/**
 * The classes of the hulls in a list of ships with how many ships each class has, in the order the
 * classes first appear.
 *
 * @param ships The ships.
 */
export function classCounts(ships: readonly FleetShip[]): { nameKey: string; count: number }[] {
  const counts = new Map<string, number>();

  for (const ship of ships) {
    counts.set(ship.classNameKey, (counts.get(ship.classNameKey) ?? 0) + 1);
  }

  return [...counts].map(([nameKey, count]) => ({ nameKey, count }));
}

/**
 * The ships of one project: the ships a player picks from by number, not one by one.
 */
export interface ProjectGroup {
  designId: number;
  name: string;
  hullNameKey: string;
  ships: FleetShip[];
}

/**
 * The projects of one class of hulls.
 */
export interface ClassGroup {
  classNameKey: string;
  projects: ProjectGroup[];
}

/**
 * Groups ships by the class of the hull and, inside a class, by the project they were built from. The
 * classes come in the order of their keys, the projects in the order of their names, and the ships of a
 * project in the order of their numbers, so the screen does not jump when a ship comes or goes.
 *
 * @param ships The ships.
 */
export function groupByClass(ships: readonly FleetShip[]): ClassGroup[] {
  const classes = new Map<string, Map<number, ProjectGroup>>();

  for (const ship of ships) {
    const projects = classes.get(ship.classNameKey) ?? new Map<number, ProjectGroup>();
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

/**
 * The ships a player asked for by number: for every project the first ships of it, as many as the
 * quantity says and no more than the project has.
 *
 * @param groups The groups the player picked from.
 * @param quantities How many ships of each project, by the number of the project.
 */
export function pickShipIds(groups: readonly ClassGroup[], quantities: ReadonlyMap<number, number>): number[] {
  return groups.flatMap((group) =>
    group.projects.flatMap((project) => project.ships.slice(0, Math.min(quantities.get(project.designId) ?? 0, project.ships.length)).map((ship) => ship.id)),
  );
}

/**
 * Groups the free ships by the body they stand on, the bodies in the order of their numbers.
 *
 * @param ships The free ships.
 */
export function groupByPlanet(ships: readonly FleetShip[]): { planetId: number; ships: FleetShip[] }[] {
  const groups = new Map<number, FleetShip[]>();

  for (const ship of ships) {
    groups.set(ship.planetId, [...(groups.get(ship.planetId) ?? []), ship]);
  }

  return [...groups]
    .sort(([left], [right]) => left - right)
    .map(([planetId, members]) => ({ planetId, ships: members }));
}

/**
 * The fleets that stand on a body, leaving out one fleet, for the choice of the fleet a ship moves to.
 *
 * @param view The fleets of the player.
 * @param planetId The body.
 * @param exceptFleetId The fleet to leave out, or `null`.
 */
export function fleetsOn(view: FleetsView, planetId: number, exceptFleetId: number | null): Fleet[] {
  return view.fleets.filter((fleet) => fleet.planetId === planetId && fleet.id !== exceptFleetId);
}
