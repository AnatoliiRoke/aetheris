import { render, type RenderResult } from '@testing-library/react';
import { vi } from 'vitest';
import { AppProviders, Pages } from '../App';
import type { AuthApi } from '../api/authApi';
import type { Api } from '../api/context';
import type { ContentApi } from '../api/contentApi';
import { mockContentApi } from '../api/mockContentApi';

/**
 * An account API whose every method is a spy; a test overrides what it needs.
 */
export function fakeAuthApi(overrides: Partial<AuthApi> = {}): AuthApi {
  return {
    register: vi.fn().mockResolvedValue(undefined),
    resendConfirmation: vi.fn().mockResolvedValue(undefined),
    confirmEmail: vi.fn().mockResolvedValue(undefined),
    login: vi.fn().mockResolvedValue(undefined),
    logout: vi.fn().mockResolvedValue(undefined),
    forgotPassword: vi.fn().mockResolvedValue(undefined),
    resetPassword: vi.fn().mockResolvedValue(undefined),
    me: vi.fn().mockResolvedValue(null),
    providers: vi.fn().mockResolvedValue({ google: false }),
    googleSignInUrl: vi.fn((returnUrl: string) => `/api/v1/auth/google?returnUrl=${returnUrl}`),
    ...overrides,
  };
}

/**
 * Renders the site at an address with fake server interfaces.
 */
export function renderPage(
  path: string,
  options: { auth?: AuthApi; content?: ContentApi } = {},
): RenderResult & { api: Api } {
  window.history.replaceState(null, '', path);

  const api: Api = { auth: options.auth ?? fakeAuthApi(), content: options.content ?? mockContentApi };
  const result = render(
    <AppProviders api={api}>
      <Pages />
    </AppProviders>,
  );

  return { ...result, api };
}
