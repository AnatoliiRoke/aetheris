import { createContext } from 'react';
import { en } from './en';
import { uk, type MessageKey, type Messages } from './uk';

/**
 * The supported languages (documentation/ui-art/04-lokalizatsiia.md).
 */
export type Language = 'uk' | 'en';

/**
 * The texts of every language.
 */
export const dictionaries: Record<Language, Messages> = { uk, en };

/**
 * The parameters of a text, put in place of `{name}`.
 */
export type MessageParams = Record<string, string | number>;

/**
 * What the components get from the language provider.
 */
export interface I18nValue {
  language: Language;
  setLanguage: (language: Language) => void;
  t: (key: MessageKey, params?: MessageParams) => string;
}

/**
 * Fills the parameters of a text.
 */
export function format(text: string, params?: MessageParams): string {
  if (!params) {
    return text;
  }

  return text.replace(/\{(\w+)\}/g, (match, name: string) =>
    Object.hasOwn(params, name) ? String(params[name]) : match,
  );
}

export const I18nContext = createContext<I18nValue | null>(null);
