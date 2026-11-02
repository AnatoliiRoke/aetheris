import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Api } from '../../../api/context';
import { ApiError } from '../../../api/http';
import { serverConfig } from '../../../config/server';
import { samplePlanet } from '../../../test/renderPage';
import { createShellStore } from '../shellStore';

const unreachable = () => new ApiError(0, 'network');

function storeOver(planet: ReturnType<typeof vi.fn>, overview: ReturnType<typeof vi.fn> = vi.fn()) {
  const api = { planets: { planet }, economy: { overview } } as unknown as Api;

  return createShellStore(api);
}

describe('the shared state heals itself when the server was not there (task 00020)', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('reads a planet again after the pause when the first read failed, and stops once it has the planet', async () => {
    const planet = vi.fn().mockRejectedValueOnce(unreachable()).mockRejectedValueOnce(unreachable()).mockResolvedValue(samplePlanet());
    const store = storeOver(planet);

    await store.actions.loadPlanet(10);

    expect(store.getState().failedPlanetIds).toEqual([10]);
    expect(planet).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(serverConfig.retryDelaysMs[0]);

    expect(planet).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(serverConfig.retryDelaysMs[1]);

    expect(planet).toHaveBeenCalledTimes(3);
    expect(store.getState().planets.has(10)).toBe(true);
    expect(store.getState().failedPlanetIds).toEqual([]);

    await vi.advanceTimersByTimeAsync(60_000);

    expect(planet).toHaveBeenCalledTimes(3);
    store.dispose();
  });

  it('gives up after the last pause and waits for an event of the page', async () => {
    const planet = vi.fn().mockRejectedValue(unreachable());
    const store = storeOver(planet);

    await store.actions.loadPlanet(10);
    await vi.advanceTimersByTimeAsync(serverConfig.retryDelaysMs.reduce((total, pause) => total + pause, 0) + 60_000);

    expect(planet).toHaveBeenCalledTimes(serverConfig.retryDelaysMs.length + 1);
    expect(store.getState().failedPlanetIds).toEqual([10]);
    store.dispose();
  });

  it('reads the economy again after a failure', async () => {
    const overview = vi.fn().mockRejectedValueOnce(unreachable()).mockResolvedValue({ balances: [] });
    const store = storeOver(vi.fn(), overview);

    await store.actions.loadEconomy();

    expect(store.getState().economyFailed).toBe(true);

    await vi.advanceTimersByTimeAsync(serverConfig.retryDelaysMs[0]);

    expect(store.getState().economyFailed).toBe(false);
    expect(store.getState().economyLoaded).toBe(true);
    store.dispose();
  });

  it('stops the repeated reads when the state is no longer in use', async () => {
    const planet = vi.fn().mockRejectedValue(unreachable());
    const store = storeOver(planet);

    await store.actions.loadPlanet(10);
    store.dispose();
    await vi.advanceTimersByTimeAsync(60_000);

    expect(planet).toHaveBeenCalledTimes(1);
  });
});
