/**
 * The places of the world the player may name as the target of a flight (task 00050).
 *
 * The player sees only what the radar shows or what he has explored: the catalog of the world
 * holds nothing but that.
 */

/**
 * A star system of a sector as the target picker shows it.
 */
export interface SystemPlace {
  /** The identifier of the system. */
  id: number;
  /** The number of the system in its sector, as the world writes it. */
  number: number;
  /** The name of the system; empty when the world has no name for it. */
  name: string | null;
}

/**
 * A body of a star system as the target picker shows it.
 */
export interface ObjectPlace {
  /** The identifier of the body. */
  id: number;
  /** The orbit of the body in its system; `null` when the body has no orbit. */
  orbit: number | null;
  /** The name of the body; empty when the world has no name for it. */
  name: string | null;
}

/**
 * How a place is written in the picker (task 00050).
 *
 * @param name The name of the place.
 * @param number The number of the place, when it has one.
 * @returns The text of the place.
 */
export function placeText(name: string | null, number: number | null): string {
  if (name !== null && name.trim() !== '') {
    return name.trim();
  }

  return number === null ? '' : String(number);
}

/**
 * How a star system is written in the picker (task 00050): the name the world gave it, or its
 * number in the sector when the world has no name for it.
 *
 * @param system The system of the picker.
 * @returns The text of the system.
 */
export function systemText(system: SystemPlace): string {
  return placeText(system.name, system.number);
}

/**
 * How a body of a star system is written in the picker (task 00050): the name the world gave it, or
 * its orbit in the system when the world has no name for it.
 *
 * @param object The body of the picker.
 * @returns The text of the body.
 */
export function objectText(object: ObjectPlace): string {
  return placeText(object.name, object.orbit);
}

/**
 * The systems of a sector the player may choose (task 00050): the ones a radar of the player
 * reaches and the ones with a body of the player in them.
 *
 * A system the player knows nothing of is not in the list at all: the server marks it `unknown`.
 *
 * @param systems The systems of the sector.
 * @returns The systems the player may choose.
 */
export function visibleSystems(
  systems: readonly { id: number; number: number; name: string | null; state: string }[],
): SystemPlace[] {
  return systems
    .filter((system) => system.state === 'open')
    .map((system) => ({ id: system.id, number: system.number, name: system.name }));
}

/**
 * The bodies of a system the player may choose (task 00050): the ones a radar of the player sees.
 *
 * A body the player only knows the position of is not in the list: the server marks it `known`.
 *
 * @param objects The bodies of the system.
 * @returns The bodies the player may choose.
 */
export function visibleObjects(
  objects: readonly { id: number; state: string; orbit: number | null; name: string | null }[],
): ObjectPlace[] {
  return objects
    .filter((object) => object.state === 'open')
    .map((object) => ({ id: object.id, orbit: object.orbit, name: object.name }));
}

/**
 * The bodies of a system the player may choose as a planet (task 00050): the planets of it, the
 * gas giants of it and the asteroid field of it, when it has one. The star of the system and the
 * nebulas are not places to fly to.
 *
 * @param objects The bodies of the system.
 * @returns The bodies the player may choose as a planet.
 */
export function planetBodies(
  objects: readonly { id: number; state: string; orbit: number | null; name: string | null; objectClass: string }[],
): ObjectPlace[] {
  return visibleObjects(
    objects.filter((object) => object.objectClass === 'planet' || object.objectClass === 'gas_giant' || object.objectClass === 'asteroid_field'),
  );
}
