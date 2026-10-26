import { createContext, useContext } from 'react';
import { ApiError } from '../api/http';
import type { AdminApi, AdminMe } from './adminApi';
import type { AdminMessageKey } from './i18n/uk';

/**
 * What the screens of the admin panel get: the endpoints, the administrator and the sign-out.
 */
export interface AdminContextValue {
  api: AdminApi;
  me: AdminMe;
  /** Whether a role of the administrator grants an action code. */
  can: (actionCode: string) => boolean;
  /** Handles an error of a request: an ended session leads back to the sign-in. */
  onError: (error: unknown) => void;
}

export const AdminContext = createContext<AdminContextValue | null>(null);

/**
 * The context of the admin panel.
 */
export function useAdmin(): AdminContextValue {
  const value = useContext(AdminContext);

  if (!value) {
    throw new Error('useAdmin must be used inside the admin panel.');
  }

  return value;
}

/**
 * The texts of the error codes of the server the admin panel knows.
 */
const errorKeys: Record<string, AdminMessageKey> = {
  invalid_credentials: 'admin.error.invalidCredentials',
  second_factor_required: 'admin.error.secondFactorRequired',
  account_locked: 'admin.error.accountLocked',
  rate_limited: 'admin.error.rateLimited',
  session_expired: 'admin.error.sessionExpired',
  not_authenticated: 'admin.error.sessionExpired',
  access_denied: 'admin.error.accessDenied',
  action_code_missing: 'admin.error.accessDenied',
  read_only_parameter: 'admin.error.readOnly',
  validation_failed: 'admin.error.validation',
  not_found: 'admin.error.notFound',
  world_generation_running: 'admin.error.generationRunning',
  world_generation_too_large: 'admin.error.generationTooLarge',
  world_rule_set_unknown: 'admin.error.ruleSetUnknown',
  network_error: 'admin.error.network',
};

/**
 * The text of an error of a request.
 */
export function errorKey(error: unknown): AdminMessageKey {
  if (error instanceof ApiError) {
    return errorKeys[error.code] ?? 'admin.error.unknown';
  }

  return 'admin.error.unknown';
}

/**
 * The codes of the fields of a validation error, by the field name in lower case, for example
 * `{ value: ['out_of_bounds'] }`.
 */
export function fieldErrors(error: unknown): Record<string, string[]> {
  if (!(error instanceof ApiError) || error.code !== 'validation_failed') {
    return {};
  }

  return Object.fromEntries(
    Object.entries(error.details).map(([field, codes]) => [field.toLowerCase(), Array.isArray(codes) ? codes.map(String) : []]),
  );
}
