import { act, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakePlanetApi,
  fakeProcessesApi,
  fakeResearchApi,
  renderPage,
  samplePlanet,
  sampleResearchTree,
  signedInMe,
} from '../../../test/renderPage';

/**
 * Task 00020, section 7.4: the data of a page is read when the page opens, after a command, when
 * a record of a queue finishes and when the tab becomes visible again; never on a timer.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const secondMilliseconds = 1000;
const longTimeMilliseconds = 60 * 60 * secondMilliseconds;
const settleMilliseconds = 50;

const emptyPlanet = () => samplePlanet({ queue: { slotCount: 2, items: [] } });

const underWayPlanet = () => samplePlanet();

const underWayTree = () =>
  sampleResearchTree({
    queue: [
      {
        id: 9,
        technologyId: 1,
        technologyNameKey: 'research.technology.tech_1.name',
        targetLevel: 1,
        planetId: 10,
        position: 1,
        startsAt: '2026-10-26T12:00:00Z',
        finishesAt: '2026-10-26T12:00:30Z',
        costs: [{ resourceTypeId: 1, amount: 200 }],
      },
    ],
    queueUsedSlots: 1,
  });

/**
 * The number of calls of every fake interface method that is a spy.
 */
const totalCalls = (api: object): number =>
  Object.values(api).reduce(
    (sum: number, group: object) =>
      sum +
      Object.values(group).reduce((groupSum: number, method: unknown) => groupSum + (vi.isMockFunction(method) ? method.mock.calls.length : 0), 0),
    0,
  );

const settle = () => act(() => new Promise<void>((resolve) => window.setTimeout(resolve, settleMilliseconds)));

const setVisibility = (state: 'visible' | 'hidden') => {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(state);
  document.dispatchEvent(new Event('visibilitychange'));
};

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('the pages of the player have no terraforming (task 00020, section 7.4)', () => {
  it.each(['/planet', '/game'])('does not draw the terraforming panel on %s', async (path) => {
    const processes = fakeProcessesApi();
    renderPage(path, { auth: signedIn(), processes });

    const queues = await screen.findByRole('region', { name: 'Процеси' });
    await settle();

    expect(within(queues).queryByText(/Тераформ/)).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Тераформінг' })).not.toBeInTheDocument();
    expect(processes.terraforming).not.toHaveBeenCalled();
  });
});

describe('the data is read again on the events of the page (task 00020, section 7.4)', () => {
  it.each([
    ['/planet', 'construction'],
    ['/planet/build', 'construction'],
    ['/planet', 'research'],
    ['/planet/research', 'research'],
  ])('asks the server again once when a record of the %s queue of %s finishes', async (path, queue) => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    const planets = fakePlanetApi({ planet: vi.fn().mockResolvedValue(queue === 'construction' ? underWayPlanet() : emptyPlanet()) });
    const research = fakeResearchApi({ tree: vi.fn().mockResolvedValue(queue === 'research' ? underWayTree() : sampleResearchTree()) });
    renderPage(path, { auth: signedIn(), planets, research });

    await screen.findByRole('timer');
    await settle();
    const source = queue === 'construction' ? planets.planet : research.tree;
    const callsBefore = vi.mocked(source).mock.calls.length;

    await act(async () => {
      vi.advanceTimersByTime(31 * secondMilliseconds);
    });
    await settle();
    expect(vi.mocked(source).mock.calls.length).toBe(callsBefore + 1);

    // The record tells once: the next seconds do not ask again.
    await act(async () => {
      vi.advanceTimersByTime(10 * secondMilliseconds);
    });
    await settle();
    expect(vi.mocked(source).mock.calls.length).toBe(callsBefore + 1);
  });

  it('reads the data again when the tab becomes visible, and not when it becomes hidden', async () => {
    const planets = fakePlanetApi({ planet: vi.fn().mockResolvedValue(emptyPlanet()) });
    const research = fakeResearchApi();
    renderPage('/planet', { auth: signedIn(), planets, research });

    await screen.findByRole('region', { name: 'Процеси' });
    await settle();
    const planetCalls = vi.mocked(planets.planet).mock.calls.length;
    const researchCalls = vi.mocked(research.tree).mock.calls.length;

    act(() => setVisibility('hidden'));
    await settle();
    expect(vi.mocked(planets.planet).mock.calls.length).toBe(planetCalls);
    expect(vi.mocked(research.tree).mock.calls.length).toBe(researchCalls);

    act(() => setVisibility('visible'));
    await waitFor(() => expect(vi.mocked(planets.planet).mock.calls.length).toBeGreaterThan(planetCalls));
    await waitFor(() => expect(vi.mocked(research.tree).mock.calls.length).toBeGreaterThan(researchCalls));
  });

  it('never reads the server on a timer: a long time passes without a new request', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    const planets = fakePlanetApi({ planet: vi.fn().mockResolvedValue(emptyPlanet()) });
    const { api } = renderPage('/planet', { auth: signedIn(), planets });

    await screen.findByRole('region', { name: 'Процеси' });
    await settle();
    const callsBefore = totalCalls(api);

    expect(callsBefore).toBeGreaterThan(0);

    await act(async () => {
      vi.advanceTimersByTime(longTimeMilliseconds);
    });
    await settle();

    expect(totalCalls(api)).toBe(callsBefore);
  });
});
