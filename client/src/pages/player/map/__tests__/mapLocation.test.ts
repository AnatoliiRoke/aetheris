import { describe, expect, it } from 'vitest';
import { buildQuery, parseLocation, targetFromText, targetToText } from '../mapLocation';

describe('mapLocation', () => {
  it('writes and reads the place and the choice of the map in the address', () => {
    const location = {
      galaxy: false,
      sectorId: 3,
      systemId: 12,
      objectId: 40,
      mode: 'list' as const,
      target: { kind: 'object' as const, objectId: 40, systemId: 12, sectorId: null },
    };
    const query = buildQuery(location);

    expect(query).toBe('?sector=3&system=12&object=40&view=list&target=object%3A40%3A12%3A');
    expect(parseLocation(new URLSearchParams(query))).toEqual(location);
  });

  it('reads the three kinds of a target and refuses a text that is not one', () => {
    expect(targetFromText('system:12')).toEqual({ kind: 'system', systemId: 12 });
    expect(targetFromText('point:3:4.5:-2:0')).toEqual({ kind: 'point', sectorId: 3, x: 4.5, y: -2, z: 0 });
    expect(targetToText({ kind: 'point', sectorId: 3, x: 4.5, y: -2, z: 0 })).toBe('point:3:4.5:-2:0');
    expect(targetFromText('point:3:a:2:0')).toBeNull();
    expect(targetFromText('system:0')).toBeNull();
    expect(targetFromText('fleet:3')).toBeNull();
    expect(targetFromText(null)).toBeNull();
  });

  it('writes nothing for an empty place and takes the galaxy from its own mark', () => {
    expect(buildQuery({ galaxy: false, sectorId: null, systemId: null, objectId: null, mode: 'map', target: null })).toBe('');
    expect(parseLocation(new URLSearchParams('level=galaxy')).galaxy).toBe(true);
    expect(parseLocation(new URLSearchParams('sector=abc&system=-1')).sectorId).toBeNull();
  });
});
