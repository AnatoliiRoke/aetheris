import { describe, expect, it, vi } from 'vitest';
import { createMapApi } from '../mapApi';
import type { HttpClient } from '../http';

const http = (answer: unknown) => ({ get: vi.fn().mockResolvedValue(answer) }) as unknown as HttpClient & { get: ReturnType<typeof vi.fn> };

describe('the map API', () => {
  it('asks the routes of the map with the body the distances are counted from, and turns the answers into camel case', async () => {
    const client = http({ sector_id: 1, sectors: [], sector_size: 1000, sees_only_own: false, systems: [{ travel_seconds: 5 }] });
    const api = createMapApi(client);

    expect(await api.galaxy()).toMatchObject({ sectorSize: 1000, seesOnlyOwn: false });
    await api.sector(3, 10);
    await api.sector(3, null);
    await api.system(12, 10);
    await api.search('Ly ra');
    await api.target(3, 1.5, 2, 0, 10);
    await api.target(3, 1.5, 2, 0, null);

    expect(client.get.mock.calls.map((call) => call[0])).toEqual([
      '/api/v1/world/map/galaxy',
      '/api/v1/world/map/sectors/3?from_planet_id=10',
      '/api/v1/world/map/sectors/3',
      '/api/v1/world/map/systems/12?from_planet_id=10',
      '/api/v1/world/map/search?q=Ly%20ra',
      '/api/v1/world/map/target?sector_id=3&x=1.5&y=2&z=0&from_planet_id=10',
      '/api/v1/world/map/target?sector_id=3&x=1.5&y=2&z=0',
    ]);
  });
});
