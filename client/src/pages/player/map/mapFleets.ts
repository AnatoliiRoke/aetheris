/**
 * The geometry of the fleets that fly, as the map draws them (task 00059): the route of a flight,
 * the point of the route the time has reached and the place of that point in the units of the
 * level that is drawn.
 *
 * The client counts the place of a fleet exactly as the server counts it in `PlaceOnRoute`: the
 * share of the route the time has reached, and the point between the start and the goal. The two
 * must agree, so the formula lives here once and the server has the same one.
 */

import type { FleetInFlight, FleetStationed } from '../../../api/fleetApi';

/** The share of a route where a fleet belongs to the sector it left, after it to the sector it flies to. */
const midpointProgress = 0.5;

/** The level of the map that is drawn. */
export type FleetMapLevel = 'galaxy' | 'sector' | 'system';

/**
 * The share of the route a flight has covered at a moment of game time.
 *
 * @param startedAt The moment of game time the fleet left, in milliseconds since the epoch.
 * @param arrivesAt The moment of game time the fleet arrives, in milliseconds since the epoch.
 * @param now The moment of game time now, in milliseconds since the epoch.
 */
export function routeProgress(startedAt: number, arrivesAt: number, now: number): number {
  const total = (arrivesAt - startedAt) / 1000;

  if (total <= 0) {
    return 1;
  }

  return Math.min(Math.max((now - startedAt) / 1000 / total, 0), 1);
}

/**
 * The orbit a fleet under way is at inside one system: it keeps the system and takes the orbit it
 * has reached, because the distance inside a system is counted by the orbits. The rounding is the
 * same as the server does: away from zero.
 *
 * @param fromOrbit The orbit the fleet left.
 * @param toOrbit The orbit the fleet flies to.
 * @param progress The share of the route the fleet has covered.
 */
export function routeOrbit(fromOrbit: number, toOrbit: number, progress: number): number {
  const orbit = fromOrbit + (toOrbit - fromOrbit) * progress;

  // The server rounds away from zero, so the client does the same.
  return orbit < 0 ? -Math.round(-orbit) : Math.round(orbit);
}

/**
 * The place of a fleet under way in the axes of the galaxy: the point of the route the time has
 * reached.
 *
 * @param flight The flight of the fleet.
 * @param now The moment of game time now, in milliseconds since the epoch.
 */
export function flightPlace(
  flight: FleetInFlight['flight'],
  now: number,
): { x: number; y: number; z: number; orbit: number | null } {
  const progress = flightProgress(flight, now);

  // Inside one system the fleet is between two orbits: it keeps the system and takes the orbit it
  // has reached, because the distance inside a system is counted by the orbits.
  if (flight.fromSystemId !== null && flight.fromSystemId === flight.toSystemId) {
    return {
      x: flight.fromX,
      y: flight.fromY,
      z: flight.fromZ,
      orbit: routeOrbit(flight.fromOrbit ?? 0, flight.toOrbit ?? 0, progress),
    };
  }

  return {
    x: flight.fromX + (flight.toX - flight.fromX) * progress,
    y: flight.fromY + (flight.toY - flight.fromY) * progress,
    z: flight.fromZ + (flight.toZ - flight.fromZ) * progress,
    orbit: null,
  };
}

/**
 * Whether a fleet prepares a jump at a moment (task 00072): the fleet stands at the place it left
 * until the preparation is over, and then the jump takes no time.
 *
 * @param flight The flight of the fleet.
 * @param now The moment of game time now, in milliseconds since the epoch.
 */
export function inJumpPrep(flight: FleetInFlight['flight'], now: number): boolean {
  return flight.legKind === 'jump' && now < Date.parse(flight.prepEndsAt ?? flight.arrivesAt);
}

/**
 * The share of the route a flight has covered at a moment: a jump stays at its start while it is
 * prepared and is at its end after (task 00072).
 *
 * @param flight The flight of the fleet.
 * @param now The moment of game time now, in milliseconds since the epoch.
 */
function flightProgress(flight: FleetInFlight['flight'], now: number): number {
  if (flight.legKind === 'jump') {
    return inJumpPrep(flight, now) ? 0 : 1;
  }

  return routeProgress(Date.parse(flight.startedAt), Date.parse(flight.arrivesAt), now);
}

/**
 * Whether a flight is inside one system: the fleet flies between two orbits and not between two
 * places of the galaxy.
 *
 * @param flight The flight of the fleet.
 */
export function flightInsideSystem(flight: FleetInFlight['flight']): boolean {
  return flight.fromSystemId !== null && flight.fromSystemId === flight.toSystemId;
}

/**
 * The sector a fleet under way belongs to: the sector it left while it has not reached the middle
 * of its route and the sector it flies to after that.
 *
 * @param flight The flight of the fleet.
 * @param now The moment of game time now, in milliseconds since the epoch.
 */
export function flightSector(flight: FleetInFlight['flight'], now: number): number {
  const progress = flightProgress(flight, now);

  return progress < midpointProgress ? flight.fromSectorId : flight.toSectorId;
}

/**
 * The place of a fleet that stands at a place, as the level that is drawn shows it. A fleet that
 * stands on an orbit of a system is drawn between the orbits, and a fleet that stands in open space
 * is drawn by the axes of the galaxy.
 *
 * @param fleet The fleet that stands.
 * @param level The level of the map that is drawn.
 * @param sectorId The sector that is drawn, or `null` on the level of the galaxy.
 * @param sectorPlace The place of the sector that is drawn, or `null` on the level of the galaxy.
 * @param systemId The system that is drawn, or `null` outside a system.
 * @param orbitPlace The place of an orbit of the system that is drawn.
 * @param orbitCount How many orbits the system that is drawn has.
 */
export function stationedPlace(
  fleet: FleetStationed,
  level: FleetMapLevel,
  sectorId: number | null,
  sectorPlace: { x: number; y: number } | null,
  systemId: number | null,
  orbitPlace: (orbit: number, orbitCount: number) => { x: number; y: number },
  orbitCount: number,
): { x: number; y: number } | null {
  // Inside one system the fleet stands on an orbit, and the map of the system draws it there.
  if (fleet.systemId !== null && fleet.orbit !== null) {
    if (level !== 'system' || systemId !== fleet.systemId) {
      return null;
    }

    return orbitPlace(fleet.orbit, orbitCount);
  }

  if (level === 'galaxy') {
    return null;
  }

  // On the level of a sector the place of the fleet is counted from the origin of the sector.
  if (level === 'sector') {
    if (sectorId === null || sectorPlace === null) {
      return null;
    }

    return { x: fleet.x - sectorPlace.x, y: fleet.y - sectorPlace.y };
  }

  return null;
}

/**
 * The route of a fleet as the level that is drawn shows it: the two ends of the line and the point
 * of the fleet on it. A route that is not in the level that is drawn has no place.
 *
 * @param fleet The fleet that is under way.
 * @param now The moment of game time now, in milliseconds since the epoch.
 * @param level The level of the map that is drawn.
 * @param sectorId The sector that is drawn, or `null` on the level of the galaxy.
 * @param sectorPlace The place of the sector that is drawn, or `null` on the level of the galaxy.
 * @param systemId The system that is drawn, or `null` outside a system.
 * @param orbitPlace The place of an orbit of the system that is drawn.
 * @param orbitCount How many orbits the system that is drawn has.
 */
export function fleetRoute(
  fleet: FleetInFlight,
  now: number,
  level: FleetMapLevel,
  sectorId: number | null,
  sectorPlace: { x: number; y: number } | null,
  systemId: number | null,
  orbitPlace: (orbit: number, orbitCount: number) => { x: number; y: number },
  orbitCount: number,
): { from: { x: number; y: number }; to: { x: number; y: number }; mark: { x: number; y: number } } | null {
  const flight = fleet.flight;

  // Inside one system the route is between two orbits, and only the level of the system shows it.
  if (flightInsideSystem(flight)) {
    if (level !== 'system' || systemId !== flight.fromSystemId) {
      return null;
    }

    const from = orbitPlace(flight.fromOrbit ?? 1, orbitCount);
    const to = orbitPlace(flight.toOrbit ?? 1, orbitCount);
    const place = flightPlace(flight, now);

    return { from, to, mark: orbitPlace(place.orbit ?? 1, orbitCount) };
  }

  // Between systems and in open space the route is between two places of the galaxy. On the level
  // of the galaxy the ends of the route are the places of the sectors it joins.
  if (level === 'galaxy') {
    if (!fleet.fromSector || !fleet.toSector) {
      return null;
    }

    const from = { x: fleet.fromSector.x, y: fleet.fromSector.y };
    const to = { x: fleet.toSector.x, y: fleet.toSector.y };
    const place = flightPlace(flight, now);

    return { from, to, mark: { x: place.x, y: place.y } };
  }

  // On the level of a sector the route is drawn in the axes of that sector: the places of the
  // galaxy are counted from the origin of the sector.
  if (level === 'sector') {
    if (sectorId === null || sectorPlace === null) {
      return null;
    }

    const from = { x: flight.fromX - sectorPlace.x, y: flight.fromY - sectorPlace.y };
    const to = { x: flight.toX - sectorPlace.x, y: flight.toY - sectorPlace.y };
    const place = flightPlace(flight, now);

    return { from, to, mark: { x: place.x - sectorPlace.x, y: place.y - sectorPlace.y } };
  }

  return null;
}
