import { render, type RenderResult } from '@testing-library/react';
import { vi } from 'vitest';
import { AppProviders, Pages } from '../App';
import type { AuthApi, Me } from '../api/authApi';
import type { Api } from '../api/context';
import type { ContentApi } from '../api/contentApi';
import type { GameApi } from '../api/gameApi';
import { mockContentApi } from '../api/mockContentApi';

/**
 * An account API whose every method is a spy; a test overrides what it needs.
 */
export function fakeAuthApi(overrides: Partial<AuthApi> = {}): AuthApi {
  return {
    register: vi.fn().mockResolvedValue(undefined),
    resendConfirmation: vi.fn().mockResolvedValue(undefined),
    confirmEmail: vi.fn().mockResolvedValue(undefined),
    login: vi.fn().mockResolvedValue({ deletionCancelled: false }),
    logout: vi.fn().mockResolvedValue(undefined),
    forgotPassword: vi.fn().mockResolvedValue(undefined),
    resetPassword: vi.fn().mockResolvedValue(undefined),
    me: vi.fn().mockResolvedValue(null),
    providers: vi.fn().mockResolvedValue({ google: false }),
    googleSignInUrl: vi.fn((returnUrl: string) => `/api/v1/auth/google?returnUrl=${returnUrl}`),
    resendMyConfirmation: vi.fn().mockResolvedValue(undefined),
    changePassword: vi.fn().mockResolvedValue(undefined),
    changeNickname: vi.fn().mockResolvedValue({ nickname: 'Pilot', nextChangeAt: '2026-11-25T00:00:00Z' }),
    signOutEverywhere: vi.fn().mockResolvedValue(undefined),
    deleteAccount: vi.fn().mockResolvedValue({ deletionDueAt: '2026-11-02T00:00:00Z' }),
    ...overrides,
  };
}

/**
 * A world API whose every method is a spy; a test overrides what it needs.
 */
export function fakeGameApi(overrides: Partial<GameApi> = {}): GameApi {
  return {
    races: vi.fn().mockResolvedValue([
      { id: 1, code: 'terran', nameKey: 'races.terran.name' },
      { id: 2, code: 'kvarr', nameKey: 'races.kvarr.name' },
    ]),
    foundCivilization: vi.fn().mockResolvedValue({
      raceId: 1,
      raceCode: 'terran',
      planetId: 10,
      planetName: 'Nova',
      systemId: 3,
      sectorId: 1,
    }),
    ...overrides,
  };
}

/**
 * A signed-in account for the tests.
 */
export function signedInMe(overrides: Partial<Me> = {}): Me {
  return {
    userId: 'user-1',
    nickname: 'Pilot',
    email: 'pilot@example.test',
    emailConfirmed: true,
    nicknameChangedAt: null,
    civilization: { raceId: 1, raceCode: 'terran', planetId: 10, planetName: 'Nova', systemId: 3, sectorId: 1 },
    ...overrides,
  };
}

/**
 * Renders the site at an address with fake server interfaces.
 */
export function renderPage(
  path: string,
  options: { auth?: AuthApi; content?: ContentApi; game?: GameApi } = {},
): RenderResult & { api: Api } {
  window.history.replaceState(null, '', path);

  const api: Api = {
    auth: options.auth ?? fakeAuthApi(),
    content: options.content ?? mockContentApi,
    game: options.game ?? fakeGameApi(),
  };
  const result = render(
    <AppProviders api={api}>
      <Pages />
    </AppProviders>,
  );

  return { ...result, api };
}
