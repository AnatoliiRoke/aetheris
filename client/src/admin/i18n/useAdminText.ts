import { useCallback } from 'react';
import { format, type MessageParams } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { adminEn } from './en';
import { adminUk, type AdminMessageKey } from './uk';

const dictionaries = { uk: adminUk, en: adminEn } as const;

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
