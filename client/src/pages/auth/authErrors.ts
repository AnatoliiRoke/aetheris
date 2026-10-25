import { ApiError, clientErrorCodes } from '../../api/http';
import type { MessageKey } from '../../i18n/uk';

/**
 * The message of a failed request: a title and a text.
 */
export interface ErrorMessage {
  title: MessageKey;
  text: MessageKey;
}

const rateLimited: ErrorMessage = { title: 'error.rateLimited.title', text: 'error.rateLimited.text' };
const network: ErrorMessage = { title: 'error.network.title', text: 'error.network.text' };
const generic: ErrorMessage = { title: 'error.generic.title', text: 'error.generic.text' };

/**
 * Turns a server error code into the text shown to the player. The server never says
 * whether an address is registered, and neither does the client.
 */
export function describeError(error: unknown): ErrorMessage {
  const code = error instanceof ApiError ? error.code : clientErrorCodes.unknown;

  switch (code) {
    case 'invalid_credentials':
      return { title: 'error.signinFailed.title', text: 'error.signinFailed.text' };
    case 'account_locked':
      return { title: 'error.locked.title', text: 'error.locked.text' };
    case 'email_not_confirmed':
      return { title: 'error.notConfirmed.title', text: 'error.notConfirmed.text' };
    case 'invalid_token':
      return { title: 'error.invalidToken.title', text: 'error.invalidToken.text' };
    case 'rate_limited':
      return rateLimited;
    case clientErrorCodes.network:
      return network;
    default:
      return generic;
  }
}

/**
 * The password rule codes the server answers with (`ConfigPasswordValidator`).
 */
const passwordRuleCodes = new Set([
  'password_too_short',
  'password_too_long',
  'password_too_few_unique_chars',
  'password_too_common',
  'password_contains_email',
]);

/**
 * Reads the password rules that the server refused, as localization keys.
 *
 * @param field The name of the password field in the server's answer.
 */
export function passwordRuleErrors(error: unknown, field: string): MessageKey[] {
  if (!(error instanceof ApiError) || error.code !== 'validation_failed') {
    return [];
  }

  const codes = error.details[field];

  return Array.isArray(codes)
    ? codes.filter((code): code is string => typeof code === 'string' && passwordRuleCodes.has(code)).map((code) => `error.${code}` as MessageKey)
    : [];
}

/**
 * A simple shape check of an e-mail address; the server checks it again.
 */
export function isEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}
