import { ApiError } from '../../api/http';
import type { Language } from '../../i18n/context';
import { reasonText } from './planetText';

/**
 * A message above the content of a screen: what a command did or why the server refused it.
 */
export type Notice = { kind: 'error' | 'success'; text: string } | null;

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['research_', 'planets_'];

/**
 * The message for the player from the error of a command: a refusal of the server has its own
 * text, any other failure gets the general one.
 *
 * @param error The error of the command.
 * @param language The language of the player.
 * @param generic The general text for a failure the player cannot act on.
 * @param prefixes The prefixes of the codes that have their own text; the research ones by default.
 */
export function noticeFromError(error: unknown, language: Language, generic: string, prefixes: string[] = reasonPrefixes): Notice {
  const code = error instanceof ApiError ? error.code : 'internal_error';

  return { kind: 'error', text: prefixes.some((prefix) => code.startsWith(prefix)) ? reasonText(language, code) : generic };
}
