import { ApiError, clientErrorCodes } from '../../api/http';
import { siteConfig } from '../../config/site';
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
    case 'registration_limit_reached':
      return { title: 'error.registrationLimit.title', text: 'error.registrationLimit.text' };
    case 'captcha_required':
      return { title: 'error.captcha.title', text: 'error.captcha.text' };
    case 'onboarding_required':
      return { title: 'error.onboarding.title', text: 'error.onboarding.text' };
    case 'confirmation_too_soon':
      return { title: 'error.confirmationTooSoon.title', text: 'error.confirmationTooSoon.text' };
    case 'nickname_change_too_soon':
      return { title: 'error.nicknameTooSoon.title', text: 'error.nicknameTooSoon.text' };
    case 'world_civilization_exists':
      return { title: 'error.civilizationExists.title', text: 'error.civilizationExists.text' };
    case 'world_no_start_planet':
      return { title: 'error.noStartPlanet.title', text: 'error.noStartPlanet.text' };
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
  'password_no_digit',
  'password_no_uppercase',
  'password_too_common',
  'password_contains_email',
  'password_mismatch',
  'wrong_password',
]);

/**
 * The field rule codes of the account and planet forms (task 00006), translated as `error.<code>`.
 */
const fieldRuleCodes = new Set([
  ...passwordRuleCodes,
  'email_domain_blocked',
  'nickname_too_short',
  'nickname_too_long',
  'nickname_invalid_characters',
  'nickname_restricted',
  'nickname_taken',
  'planet_name_too_short',
  'planet_name_too_long',
  'planet_name_invalid_characters',
  'planet_name_restricted',
]);

/**
 * Reads the field rules that the server refused, as localization keys.
 *
 * @param field The name of the field in the server's answer.
 */
export function fieldRuleErrors(error: unknown, field: string): MessageKey[] {
  if (!(error instanceof ApiError) || error.code !== 'validation_failed') {
    return [];
  }

  const codes = error.details[field];

  return Array.isArray(codes)
    ? codes.filter((code): code is string => typeof code === 'string' && fieldRuleCodes.has(code)).map((code) => `error.${code}` as MessageKey)
    : [];
}

/**
 * Reads the password rules that the server refused, as localization keys.
 *
 * @param field The name of the password field in the server's answer.
 */
export function passwordRuleErrors(error: unknown, field: string): MessageKey[] {
  return fieldRuleErrors(error, field).filter((key) => passwordRuleCodes.has(key.slice('error.'.length)));
}

/**
 * The password rules the client checks before sending; the server checks them again.
 */
export function passwordProblems(password: string): MessageKey[] {
  const problems: MessageKey[] = [];

  if (password.length < siteConfig.passwordMinLength) {
    problems.push('error.passwordShort');
  }

  if (!/\p{Nd}/u.test(password)) {
    problems.push('error.password_no_digit');
  }

  if (!/\p{Lu}/u.test(password)) {
    problems.push('error.password_no_uppercase');
  }

  return problems;
}

const dashCode = '-'.codePointAt(0);

/**
 * Checks that a text has only the characters of a definition in the format of the server:
 * characters listed as they are, two characters joined by `-` are a range, a `-` at the start
 * or at the end is the dash itself.
 */
export function hasOnlyAllowedCharacters(text: string, definition: string): boolean {
  const characters = Array.from(definition, (character) => character.codePointAt(0) ?? 0);
  const ranges: Array<[number, number]> = [];

  for (let i = 0; i < characters.length; i++) {
    const lastIndex = i + 2;

    if (lastIndex < characters.length && characters[i + 1] === dashCode) {
      ranges.push([characters[i], characters[lastIndex]]);
      i = lastIndex;
    } else {
      ranges.push([characters[i], characters[i]]);
    }
  }

  const symbols = Array.from(text, (character) => character.codePointAt(0) ?? 0);

  return symbols.length > 0 && symbols.every((code) => ranges.some(([first, last]) => code >= first && code <= last));
}

/**
 * The nickname rules the client checks before sending; the server checks them again and
 * also the restricted words and the uniqueness.
 */
export function nicknameProblems(nickname: string): MessageKey[] {
  const value = nickname.trim();

  if (value.length < siteConfig.nicknameMinLength) {
    return ['error.nickname_too_short'];
  }

  if (value.length > siteConfig.nicknameMaxLength) {
    return ['error.nickname_too_long'];
  }

  return hasOnlyAllowedCharacters(value, siteConfig.nicknameCharacters) ? [] : ['error.nickname_invalid_characters'];
}

/**
 * The rules of the name of the home planet, checked before sending.
 */
export function planetNameProblems(name: string): MessageKey[] {
  const value = name.trim();

  if (value.length < siteConfig.planetNameMinLength) {
    return ['error.planet_name_too_short'];
  }

  if (value.length > siteConfig.planetNameMaxLength) {
    return ['error.planet_name_too_long'];
  }

  return hasOnlyAllowedCharacters(value, siteConfig.planetNameCharacters) ? [] : ['error.planet_name_invalid_characters'];
}

/**
 * A simple shape check of an e-mail address; the server checks it again.
 */
export function isEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}
