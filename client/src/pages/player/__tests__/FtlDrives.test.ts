import { describe, expect, it } from 'vitest';
import type { FleetInFlight, ShipModule } from '../../../api/fleetApi';
import { flightPlace, inJumpPrep } from '../map/mapFleets';
import { ftlSlotsWithoutNormalDrive, isNormalDrive, needsNormalDrive } from '../shipyard/shipyardParts';

/**
 * A drive of the catalog for the tests (task 00072).
 */
function drive(id: number, engineKind: string): ShipModule {
  return {
    id,
    code: `drive_${id}`,
    nameKey: '',
    descriptionKey: '',
    imageKey: null,
    partGroupCode: 'pg_1',
    partGroupNameKey: '',
    role: 'universal',
    component: 'drive',
    compatibleHullIds: [],
    raceIds: [],
    energyUse: 0,
    power: 0,
    crew: 0,
    drivePush: 100,
    damage: 0,
    damageKind: null,
    shieldCapacity: 0,
    weight: 1,
    markLevels: [1],
    isAmmunition: false,
    isCapsule: false,
    engineKind,
  };
}

/**
 * A jump of a fleet for the tests (task 00072).
 */
function jump(startedAt: string, arrivesAt: string): FleetInFlight['flight'] {
  return {
    orderId: 1,
    startedAt,
    arrivesAt,
    distance: 500,
    speed: 0,
    fromSectorId: 1,
    fromSystemId: 1,
    fromOrbit: 12,
    fromX: 10,
    fromY: 20,
    fromZ: 0,
    toSectorId: 2,
    toSystemId: 2,
    toObjectId: null,
    toPlanetId: null,
    toOrbit: 12,
    toX: 110,
    toY: 220,
    toZ: 0,
    legKind: 'jump',
    prepEndsAt: arrivesAt,
  };
}

describe('the drives that fly between the systems (task 00072)', () => {
  const byId = new Map([drive(1, 'normal'), drive(2, 'gravity'), drive(5, 'hyper'), drive(6, 'jump')].map((module) => [module.id, module]));

  it('warns about a project with a hyperdrive and no ordinary drive, and names the slots of the drive', () => {
    expect(ftlSlotsWithoutNormalDrive([{ slotIndex: 3, partTypeId: 5 }, { slotIndex: 1, partTypeId: 6 }], byId)).toEqual([1, 3]);
  });

  it('does not warn when the project has an ordinary or a gravitational drive', () => {
    expect(ftlSlotsWithoutNormalDrive([{ slotIndex: 0, partTypeId: 5 }, { slotIndex: 1, partTypeId: 1 }], byId)).toEqual([]);
    expect(ftlSlotsWithoutNormalDrive([{ slotIndex: 0, partTypeId: 6 }, { slotIndex: 1, partTypeId: 2 }], byId)).toEqual([]);
  });

  it('counts only the ordinary class as the push inside a system', () => {
    expect(isNormalDrive(drive(1, 'normal'))).toBe(true);
    expect(isNormalDrive(drive(2, 'gravity'))).toBe(true);
    expect(isNormalDrive(drive(5, 'hyper'))).toBe(false);
  });

  it('reads the warning of the server', () => {
    expect(needsNormalDrive({ warnings: [{ code: 'fleet_ftl_needs_normal_drive', slotIndexes: [0] }] })).toBe(true);
    expect(needsNormalDrive({ warnings: [] })).toBe(false);
  });

  it('keeps a fleet that prepares a jump where it left and puts it at the end after the jump', () => {
    const flight = jump('2026-11-13T10:00:00Z', '2026-11-13T10:15:00Z');
    const during = Date.parse('2026-11-13T10:10:00Z');
    const after = Date.parse('2026-11-13T10:16:00Z');

    expect(inJumpPrep(flight, during)).toBe(true);
    expect(flightPlace(flight, during)).toMatchObject({ x: 10, y: 20 });
    expect(inJumpPrep(flight, after)).toBe(false);
    expect(flightPlace(flight, after)).toMatchObject({ x: 110, y: 220 });
  });
});
