import { useCallback } from 'react';
import { format, type MessageParams } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { configDescriptionsUk } from './configUk';
import { adminEn } from './en';
import { adminUk, type AdminMessageKey } from './uk';
import { worldNamesEn, worldNamesUk } from './worldNames';

const dictionaries = { uk: adminUk, en: adminEn } as const;
const worldNames = { uk: worldNamesUk, en: worldNamesEn } as const;

/**
 * The descriptions of the parameters by language; English uses the description of the seed.
 */
const configDescriptions: Record<'uk' | 'en', Record<string, string>> = { uk: configDescriptionsUk, en: {} };

/**
 * The texts of the admin panel (task 00007). They live in the admin module, apart from the
 * texts of the site, and follow the language chosen on the site.
 */
export function useAdminText(): {
  t: (key: AdminMessageKey, params?: MessageParams) => string;
  language: 'uk' | 'en';
} {
  const { language } = useI18n();
  const t = useCallback((key: AdminMessageKey, params?: MessageParams) => format(dictionaries[language][key], params), [language]);

  return { t, language };
}

/**
 * Formats a moment of the server for the tables.
 */
export function formatMoment(value: string | null | undefined, language: 'uk' | 'en'): string {
  if (!value) {
    return '—';
  }

  return new Date(value).toLocaleString(language === 'uk' ? 'uk-UA' : 'en-GB');
}

/**
 * The name of a reference row of the world (a resource, an object type, a size class) by its
 * localisation key from the database (task 00009, decision 9); a key without a text shows the
 * code.
 */
export function useWorldName(): (nameKey: string | null | undefined, code: string | null | undefined) => string {
  const { language } = useI18n();

  return useCallback((nameKey, code) => (nameKey ? worldNames[language][nameKey] : undefined) ?? code ?? '—', [language]);
}

/**
 * The description of a configuration parameter in the language of the panel, by the key
 * `config.<parameter key>.description` (task 00009, decision 13); the description of the seed
 * is the fallback.
 */
export function useConfigDescription(): (key: string, fallback: string) => string {
  const { language } = useI18n();

  return useCallback((key, fallback) => configDescriptions[language][`config.${key}.description`] ?? fallback, [language]);
}

/**
 * Formats a number for the panel with the decimal separator of the language.
 */
export function formatNumber(value: number, language: 'uk' | 'en', fractionDigits: number): string {
  return new Intl.NumberFormat(language === 'uk' ? 'uk-UA' : 'en-GB', { minimumFractionDigits: 0, maximumFractionDigits: fractionDigits }).format(value);
}
