import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, fakePlanetApi, renderPage, samplePlanet, signedInMe } from '../../../test/renderPage';
import { setRenderProbe } from '../renderProbe';

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const secondsToMilliseconds = 1000;

/**
 * The source files of the screens of the player, without the tests.
 */
const sources = Object.entries(import.meta.glob<string>('../*.{ts,tsx}', { query: '?raw', import: 'default', eager: true })).map(([path, text]) => ({
  name: path.replace('../', ''),
  text: text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, ''),
}));

/**
 * The renders of the components that report them, by name.
 */
let renders: Map<string, number>;

const count = (name: string) => renders.get(name) ?? 0;

beforeEach(() => {
  renders = new Map();
  setRenderProbe((name) => renders.set(name, count(name) + 1));
});

afterEach(() => {
  setRenderProbe(null);
  vi.useRealTimers();
});

/**
 * Waits until the frame and the screen have everything they read.
 */
async function loaded(container: HTMLElement) {
  await waitFor(() => expect(container.querySelector('.civ-topbar .civ-counter--credits:not(.is-off)')).not.toBeNull());
  await waitFor(() => expect(container.querySelector('.civ-topbar .civ-counter--energy:not(.is-off)')).not.toBeNull());
  await waitFor(() => expect(container.querySelector('.civ-topbar .nick .glyph--race-1')).not.toBeNull());
  await waitFor(() => expect(container.querySelectorAll('.civ-bottombar .civ-thumb')).toHaveLength(1));
  await screen.findByRole('region', { name: 'Черга будівництва' });
}

describe('the blocks of the frame are drawn on their own (task 00020, section 7.8)', () => {
  it('does not draw the header, the menu and the bottom bar again on the tick of a timer', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });

    const { container } = renderPage('/planet', { auth: signedIn() });

    await loaded(container);
    expect(screen.getAllByRole('timer').length).toBeGreaterThan(0);

    const before = { header: count('CivHeader'), footer: count('CivFooter'), menu: count('CivMenu'), shell: count('CivShell'), item: count('QueueItem') };

    for (let tick = 0; tick < 5; tick += 1) {
      act(() => {
        vi.advanceTimersByTime(secondsToMilliseconds);
      });
    }

    // Only the counter of the queue is drawn again, five times; the page and the frame are not.
    expect(count('QueueItem') - before.item).toBeGreaterThanOrEqual(5);
    expect(count('CivHeader')).toBe(before.header);
    expect(count('CivFooter')).toBe(before.footer);
    expect(count('CivMenu')).toBe(before.menu);
    expect(count('CivShell')).toBe(before.shell);
  });

  it('does not draw the header and the bottom bar again when the content of the page changes', async () => {
    const user = userEvent.setup();
    const { container } = renderPage('/planet/storage', { auth: signedIn() });

    await screen.findByRole('tablist', { name: 'Розділи складу' });
    await waitFor(() => expect(container.querySelector('.civ-topbar .civ-counter--energy:not(.is-off)')).not.toBeNull());
    await waitFor(() => expect(container.querySelectorAll('.civ-bottombar .civ-thumb')).toHaveLength(1));

    const before = { header: count('CivHeader'), footer: count('CivFooter'), menu: count('CivMenu') };

    for (const name of ['Ресурси', 'Продукція', 'Усе']) {
      await user.click(screen.getByRole('tab', { name }));
    }

    expect(count('CivHeader')).toBe(before.header);
    expect(count('CivFooter')).toBe(before.footer);
    expect(count('CivMenu')).toBe(before.menu);
  });

  it('draws the header again only when its own data changed', async () => {
    const planets = fakePlanetApi({ planet: vi.fn().mockResolvedValue(samplePlanet()) });
    const { container } = renderPage('/planet', { auth: signedIn(), planets });

    await loaded(container);

    const comeBack = () => {
      Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' });
      document.dispatchEvent(new Event('visibilitychange'));
    };
    const calls = () => (planets.planet as ReturnType<typeof vi.fn>).mock.calls.length;
    const callsBefore = calls();
    const headerBefore = count('CivHeader');
    const footerBefore = count('CivFooter');

    // The tab comes back and the server says the same: the header and the bottom bar are not drawn again.
    await act(async () => comeBack());
    await waitFor(() => expect(calls()).toBeGreaterThan(callsBefore));
    expect(count('CivHeader')).toBe(headerBefore);
    expect(count('CivFooter')).toBe(footerBefore);

    // The server says another energy: the header is drawn again, once, and shows it.
    (planets.planet as ReturnType<typeof vi.fn>).mockResolvedValue(samplePlanet({ energy: { output: 99, consumption: 5, balance: 94, queuedBalance: 94 } }));

    await act(async () => comeBack());
    await waitFor(() => expect(container.querySelector('.civ-topbar .civ-counter--energy')).toHaveTextContent('94'));
    expect(count('CivHeader')).toBe(headerBefore + 1);
    expect(count('CivFooter')).toBe(footerBefore);
  });
});

describe('the frame reads the server once (task 00020, section 7.8)', () => {
  it('asks once for the body, the economy, the bodies, the reference tables and the races, and not again between the screens', async () => {
    const user = userEvent.setup();
    const { container, api } = renderPage('/planet', { auth: signedIn() });

    await loaded(container);

    const reads = () => ({
      planet: (api.planets.planet as ReturnType<typeof vi.fn>).mock.calls.length,
      overview: (api.economy.overview as ReturnType<typeof vi.fn>).mock.calls.length,
      bodies: (api.world.planets as ReturnType<typeof vi.fn>).mock.calls.length,
      reference: (api.world.reference as ReturnType<typeof vi.fn>).mock.calls.length,
      races: (api.game.races as ReturnType<typeof vi.fn>).mock.calls.length,
    });

    // The header, the menu, the bottom bar and the page share one answer of each read.
    expect(reads()).toEqual({ planet: 1, overview: 1, bodies: 1, reference: 1, races: 1 });

    const header = container.querySelector('.civ-topbar');
    const footer = container.querySelector('.civ-bottombar');
    const shellRendersBefore = count('CivShell');

    // From the planet to the construction and to the storage: the frame stays, nothing is read again.
    await user.click(within(container.querySelector<HTMLElement>('.civ-side')!).getByRole('link', { name: 'Склад' }));
    await screen.findByRole('tablist', { name: 'Розділи складу' });

    expect(window.location.pathname).toBe('/planet/storage');
    expect(container.querySelector('.civ-topbar')).toBe(header);
    expect(container.querySelector('.civ-bottombar')).toBe(footer);
    expect(reads()).toEqual({ planet: 1, overview: 1, bodies: 1, reference: 1, races: 1 });
    expect(count('CivShell')).toBeLessThanOrEqual(shellRendersBefore + 1);
  });

  it('keeps the frame mounted from the planet to the construction', async () => {
    const { container, api } = renderPage('/planet', { auth: signedIn() });

    await loaded(container);

    const header = container.querySelector('.civ-topbar');
    const planetReads = (api.planets.planet as ReturnType<typeof vi.fn>).mock.calls.length;

    window.history.pushState(null, '', '/planet/build');
    await act(async () => {
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    await screen.findByRole('heading', { name: /Будівництво/ });

    expect(container.querySelector('.civ-topbar')).toBe(header);
    expect((api.planets.planet as ReturnType<typeof vi.fn>).mock.calls.length).toBe(planetReads);
  });
});

describe('the second tick belongs to the counter only (task 00020, section 7.8)', () => {
  it('has the one interval of the screens in the counter of the queue, and none in the frame or in a page', () => {
    const withInterval = sources.filter((file) => /setInterval\(/.test(file.text)).map((file) => file.name);

    expect(withInterval).toEqual(['QueueItem.tsx']);
  });

  it('reads the server on no timer in the frame and in the shared state', () => {
    const frame = sources.filter((file) => /^(Civ|Shell|shell|useShell)/.test(file.name));

    expect(frame.length).toBeGreaterThan(8);
    expect(frame.filter((file) => /setInterval|setTimeout/.test(file.text)).map((file) => file.name)).toEqual([]);
  });
});
