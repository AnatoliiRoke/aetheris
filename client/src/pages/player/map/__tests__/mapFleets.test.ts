import { describe, expect, it } from 'vitest';
import type { FleetInFlight, FleetStationed } from '../../../../api/fleetApi';
import { fleetRoute, flightInsideSystem, flightPlace, flightSector, routeOrbit, routeProgress, stationedPlace } from '../mapFleets';

/** The moment of game time of the start of a flight, in milliseconds since the epoch. */
const started = Date.parse('2026-11-11T10:00:00Z');

/** A flight of ten minutes between two places of the galaxy. */
function flight(overrides: Partial<FleetInFlight['flight']> = {}): FleetInFlight['flight'] {
  return {
    orderId: 1,
    startedAt: new Date(started).toISOString(),
    arrivesAt: new Date(started + 600_000).toISOString(),
    distance: 10,
    speed: 60,
    fromSectorId: 3,
    fromSystemId: null,
    fromOrbit: null,
    fromX: 0,
    fromY: 0,
    fromZ: 0,
    toSectorId: 7,
    toSystemId: null,
    toObjectId: null,
    toPlanetId: null,
    toOrbit: null,
    toX: 100,
    toY: 200,
    toZ: 0,
    ...overrides,
  };
}

describe('mapFleets', () => {
  it('counts the share of the route the time has reached', () => {
    expect(routeProgress(started, started + 600_000, started)).toBe(0);
    expect(routeProgress(started, started + 600_000, started + 300_000)).toBeCloseTo(0.5, 8);
    expect(routeProgress(started, started + 600_000, started + 600_000)).toBe(1);
  });

  it('keeps the share of the route between zero and one', () => {
    expect(routeProgress(started, started + 600_000, started - 100_000)).toBe(0);
    expect(routeProgress(started, started + 600_000, started + 900_000)).toBe(1);
  });

  it('takes the whole route when the time of the flight is over', () => {
    expect(routeProgress(started, started, started)).toBe(1);
    expect(routeProgress(started, started - 1, started)).toBe(1);
  });

  it('rounds the orbit away from zero, as the server does', () => {
    expect(routeOrbit(1, 4, 0.5)).toBe(3);
    expect(routeOrbit(4, 1, 0.5)).toBe(3);
    expect(routeOrbit(1, 2, 0.5)).toBe(2);
  });

  it('puts the fleet at the point of the route the time has reached', () => {
    const place = flightPlace(flight(), started + 300_000);

    expect(place.x).toBeCloseTo(50, 8);
    expect(place.y).toBeCloseTo(100, 8);
    expect(place.z).toBeCloseTo(0, 8);
    expect(place.orbit).toBeNull();
  });

  it('keeps the fleet on the orbit it has reached inside one system', () => {
    const inside = flight({ fromSystemId: 4, toSystemId: 4, fromOrbit: 1, toOrbit: 5 });

    expect(flightInsideSystem(inside)).toBe(true);
    expect(flightPlace(inside, started).orbit).toBe(1);
    expect(flightPlace(inside, started + 600_000).orbit).toBe(5);
    expect(flightPlace(inside, started + 300_000).orbit).toBe(3);
  });

  it('tells the sector the fleet belongs to: the one it left before the middle, the one it flies to after it', () => {
    expect(flightSector(flight(), started)).toBe(3);
    expect(flightSector(flight(), started + 200_000)).toBe(3);
    expect(flightSector(flight(), started + 400_000)).toBe(7);
  });

  it('draws the route of a fleet between the places of the galaxy', () => {
    const fleet: FleetInFlight = {
      fleetId: 5,
      name: 'Перший',
      flight: flight(),
      fromSector: { sectorId: 3, x: 10, y: 20, z: 0 },
      toSector: { sectorId: 7, x: 110, y: 220, z: 0 },
    };

    const route = fleetRoute(fleet, started + 300_000, 'galaxy', null, null, null, () => ({ x: 0, y: 0 }), 1);

    expect(route).not.toBeNull();
    expect(route!.from).toEqual({ x: 10, y: 20 });
    expect(route!.to).toEqual({ x: 110, y: 220 });
    expect(route!.mark.x).toBeCloseTo(50, 8);
    expect(route!.mark.y).toBeCloseTo(100, 8);
  });

  it('draws the route of a fleet in the axes of the sector that is drawn', () => {
    const fleet: FleetInFlight = {
      fleetId: 5,
      name: 'Перший',
      flight: flight(),
      fromSector: { sectorId: 3, x: 1000, y: 2000, z: 0 },
      toSector: { sectorId: 7, x: 1100, y: 2200, z: 0 },
    };

    const route = fleetRoute(fleet, started + 300_000, 'sector', 3, { x: 1000, y: 2000 }, null, () => ({ x: 0, y: 0 }), 1);

    expect(route).not.toBeNull();
    expect(route!.from).toEqual({ x: -1000, y: -2000 });
    expect(route!.to).toEqual({ x: -900, y: -1800 });
    expect(route!.mark.x).toBeCloseTo(-950, 8);
  });

  it('draws a flight inside one system only on the level of that system', () => {
    const fleet: FleetInFlight = {
      fleetId: 5,
      name: 'Перший',
      flight: flight({ fromSystemId: 4, toSystemId: 4, fromOrbit: 1, toOrbit: 5 }),
      fromSector: { sectorId: 3, x: 0, y: 0, z: 0 },
      toSector: { sectorId: 3, x: 0, y: 0, z: 0 },
    };

    const orbits = (orbit: number) => ({ x: orbit * 10, y: orbit * 10 });
    const route = fleetRoute(fleet, started + 300_000, 'system', 3, { x: 0, y: 0 }, 4, orbits, 5);

    expect(route).not.toBeNull();
    expect(route!.from).toEqual({ x: 10, y: 10 });
    expect(route!.to).toEqual({ x: 50, y: 50 });
    expect(route!.mark).toEqual({ x: 30, y: 30 });
    expect(fleetRoute(fleet, started, 'galaxy', null, null, null, orbits, 5)).toBeNull();
  });

  it('draws a fleet that stands on an orbit of the system that is drawn', () => {
    const fleet: FleetStationed = {
      fleetId: 6,
      name: 'Другий',
      sectorId: 3,
      systemId: 4,
      orbit: 2,
      x: 100,
      y: 100,
      z: 0,
    };

    const orbits = (orbit: number) => ({ x: orbit * 10, y: orbit * 10 });

    expect(stationedPlace(fleet, 'system', 3, { x: 0, y: 0 }, 4, orbits, 5)).toEqual({ x: 20, y: 20 });
    expect(stationedPlace(fleet, 'system', 3, { x: 0, y: 0 }, 9, orbits, 5)).toBeNull();
    expect(stationedPlace(fleet, 'galaxy', null, null, null, orbits, 5)).toBeNull();
  });

  it('draws a fleet that stands in open space in the axes of the sector that is drawn', () => {
    const fleet: FleetStationed = {
      fleetId: 7,
      name: 'Третій',
      sectorId: 3,
      systemId: null,
      orbit: null,
      x: 1100,
      y: 2200,
      z: 0,
    };

    expect(stationedPlace(fleet, 'sector', 3, { x: 1000, y: 2000 }, null, () => ({ x: 0, y: 0 }), 1)).toEqual({ x: 100, y: 200 });
  });
});
