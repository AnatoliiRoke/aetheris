import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, renderPage, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const gameRoutes = [
  { path: '/game', current: 'Огляд' },
  { path: '/planet', current: 'Планета' },
  { path: '/planet/build', current: 'Будівництво' },
  { path: '/planet/research', current: 'Дослідження' },
  { path: '/planet/storage', current: 'Склад' },
];

/**
 * The menu without the mark of the active section, so two screens can be compared.
 */
const menuWithoutMark = (container: HTMLElement) => container.querySelector('.civ-side')!.outerHTML.replaceAll(' aria-current="page"', '');

describe('the common frame of the screens of the game (task 00020, section 8.1)', () => {
  it.each(gameRoutes)('puts $path in the one frame with the header, the menu and the bottom bar', async ({ path, current }) => {
    const { container } = renderPage(path, { auth: signedIn() });

    await waitFor(() => expect(container.querySelector('.civ-side')).not.toBeNull());

    // One header, one menu, one bottom bar: a page never draws its own copy of them.
    expect(screen.getAllByRole('banner')).toHaveLength(1);
    expect(container.querySelectorAll('.civ-topbar')).toHaveLength(1);
    expect(container.querySelectorAll('.civ-side')).toHaveLength(1);
    expect(container.querySelectorAll('.civ-bottombar')).toHaveLength(1);
    expect(container.querySelectorAll('.civ-tabbar')).toHaveLength(1);
    expect(container.querySelectorAll('.player__top')).toHaveLength(0);
    expect(container.querySelectorAll('header')).toHaveLength(1);
    expect(container.querySelectorAll('main')).toHaveLength(1);

    const menu = container.querySelector<HTMLElement>('.civ-side')!;
    expect(within(menu).getAllByRole('link', { current: 'page' })).toHaveLength(1);
    expect(within(menu).getByRole('link', { current: 'page' })).toHaveTextContent(current);
    expect(within(menu).getByRole('link', { name: 'Склад' })).toHaveAttribute('href', '/planet/storage');
  });

  it('keeps the menu and the header the same on every screen, only the active section changes', async () => {
    const menus: string[] = [];
    const headers: string[] = [];
    const bottoms: string[] = [];

    for (const { path } of gameRoutes) {
      const { container, unmount } = renderPage(path, { auth: signedIn() });

      await waitFor(() => expect(container.querySelector('.civ-side')).not.toBeNull());
      await waitFor(() => expect(container.querySelectorAll('.civ-bottombar .civ-thumb')).toHaveLength(1));
      // The header is complete when the balances, the energy and the sign of the race have arrived.
      await waitFor(() => expect(container.querySelector('.civ-topbar .civ-counter--credits:not(.is-off)')).not.toBeNull());
      await waitFor(() => expect(container.querySelector('.civ-topbar .civ-counter--energy:not(.is-off)')).not.toBeNull());
      await waitFor(() => expect(container.querySelector('.civ-topbar .nick .glyph--race-1')).not.toBeNull());

      menus.push(menuWithoutMark(container));
      headers.push(container.querySelector('.civ-topbar')!.outerHTML);
      bottoms.push(container.querySelector('.civ-bottombar')!.outerHTML);
      unmount();
    }

    expect(new Set(menus).size).toBe(1);
    expect(new Set(headers).size).toBe(1);
    expect(new Set(bottoms).size).toBe(1);
  });

  it('opens the screens from the menu without leaving the frame', async () => {
    const user = userEvent.setup();
    const { container } = renderPage('/planet', { auth: signedIn() });

    const menu = await waitFor(() => {
      const found = container.querySelector<HTMLElement>('.civ-side');

      expect(found).not.toBeNull();

      return found!;
    });

    await user.click(within(menu).getByRole('link', { name: 'Склад' }));
    expect(window.location.pathname).toBe('/planet/storage');
    expect(await screen.findByRole('tablist', { name: 'Розділи складу' })).toBeInTheDocument();
    expect(container.querySelectorAll('.civ-side')).toHaveLength(1);

    await user.click(within(container.querySelector<HTMLElement>('.civ-side')!).getByRole('link', { name: 'Дослідження' }));
    expect(window.location.pathname).toBe('/planet/research');
    expect(await screen.findByRole('region', { name: 'Черга досліджень' })).toBeInTheDocument();
    expect(container.querySelectorAll('.civ-side')).toHaveLength(1);
  });

  it('shows the screens in English with the same frame', async () => {
    const user = userEvent.setup();
    const { container } = renderPage('/planet/storage', { auth: signedIn() });

    await screen.findByRole('tablist', { name: 'Розділи складу' });
    await user.click(within(container.querySelector<HTMLElement>('.civ-topbar')!).getByRole('button', { name: 'EN' }));

    expect(await screen.findByRole('tablist', { name: 'Sections of the storage' })).toBeInTheDocument();
    expect(within(container.querySelector<HTMLElement>('.civ-side')!).getByRole('link', { name: 'Research' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Hulls' })).toBeInTheDocument();
  });
});
