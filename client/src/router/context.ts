import { createContext } from 'react';

/**
 * The routes of the public site (task 00003) and of the signed-in player (task 00006).
 */
export const routes = {
  home: '/',
  login: '/login',
  register: '/register',
  forgotPassword: '/forgot-password',
  checkEmail: '/check-email',
  confirmEmail: '/confirm-email',
  resetPassword: '/reset-password',
  game: '/game',
  planet: '/planet',
  planetBuild: '/planet/build',
  planetProduction: '/planet/production',
  planetResearch: '/planet/research',
  planetStorage: '/planet/storage',
  map: '/map',
  market: '/market',
  shipyard: '/shipyard',
  fleets: '/fleets',
  civilization: '/civilization',
  account: '/account',
} as const;

/**
 * What the components get from the router.
 */
export interface RouterValue {
  path: string;
  query: URLSearchParams;
  navigate: (to: string, options?: { replace?: boolean }) => void;
}

export const RouterContext = createContext<RouterValue | null>(null);
