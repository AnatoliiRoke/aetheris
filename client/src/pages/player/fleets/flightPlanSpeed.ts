import type { Fleet, ShipDesign } from '../../../api/fleetApi';

/**
 * The speed and the time of the flight of a fleet (task 00050).
 *
 * The speed of a fleet is the speed of its slowest ship: the fleet waits for the last one. The
 * speed of a ship comes from the push of its drives and the mass it carries, so the fleet flies at
 * two speeds: with an empty hold and with a full one.
 */

/**
 * The reference point of the formula of the speed (task 00050): a ship of 1000 tonnes with 100 kN of
 * push flies at 45 light-years per hour.
 */
const REFERENCE_PUSH_KILONEWTONS = 100;

/** The mass of the reference ship, in tonnes. */
const REFERENCE_MASS_TONNES = 1000;

/** The speed of the reference ship, in light-years per hour. */
const REFERENCE_SPEED_LIGHT_YEARS_PER_HOUR = 45;

/**
 * The speed of a ship, in light-years per hour (task 00050).
 *
 * The speed grows with the square root of the push per tonne, so a heavier ship is slower, but not
 * in proportion to its mass.
 *
 * @param pushKilonewtons The push of the drives of the ship, in kilonewtons.
 * @param massTonnes The mass of the ship with its cargo, in tonnes.
 * @returns The speed of the ship, in light-years per hour.
 */
export function shipSpeed(pushKilonewtons: number, massTonnes: number): number {
  if (pushKilonewtons <= 0 || massTonnes <= 0) {
    return 0;
  }

  const reference = REFERENCE_PUSH_KILONEWTONS / REFERENCE_MASS_TONNES;

  return REFERENCE_SPEED_LIGHT_YEARS_PER_HOUR * Math.sqrt(pushKilonewtons / massTonnes / reference);
}

/**
 * The two times of the flight of a fleet, in minutes (task 00050).
 *
 * @param distance The distance to the target, in light-years.
 * @param emptySpeed The speed of the fleet with an empty hold, in light-years per hour.
 * @param fullSpeed The speed of the fleet with a full hold, in light-years per hour.
 * @returns The time with an empty hold and the time with a full hold, in minutes.
 */
export function flightMinutes(distance: number, emptySpeed: number, fullSpeed: number): { empty: number; full: number } {
  const toMinutes = (speed: number): number => (speed <= 0 ? 0 : (distance / speed) * 60);

  return { empty: toMinutes(emptySpeed), full: toMinutes(fullSpeed) };
}

/**
 * The speed of a fleet, in light-years per hour (task 00050): the speed of its slowest ship, with an
 * empty hold and with a full one.
 *
 * @param fleet The fleet.
 * @param designs The projects of the ships of the fleet, by their identifiers.
 * @returns The speed of the fleet with an empty hold and with a full one.
 */
export function fleetSpeeds(fleet: Fleet, designs: readonly ShipDesign[]): { empty: number; full: number } {
  const byId = new Map(designs.map((design) => [design.id, design]));

  let empty = 0;
  let full = 0;

  for (const ship of fleet.ships) {
    const design = byId.get(ship.designId);

    if (design === undefined) {
      continue;
    }

    const push = design.summary.drivePush;

    // The empty speed of the ship: the push of the drives over the mass of the hull.
    const emptySpeed = shipSpeed(push, design.summary.weight);

    // The full speed of the ship: the same ship with its hold full.
    const fullSpeed = shipSpeed(push, design.summary.weight + design.summary.holdCapacity);

    // The fleet flies at the speed of its slowest ship, so the slowest one wins both times.
    empty = empty === 0 ? emptySpeed : Math.min(empty, emptySpeed);
    full = full === 0 ? fullSpeed : Math.min(full, fullSpeed);
  }

  return { empty, full };
}
