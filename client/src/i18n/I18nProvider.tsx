import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { I18nContext, dictionaries, format, type I18nValue, type Language, type MessageParams } from './context';
import type { MessageKey } from './uk';

/**
 * The storage key of the chosen language.
 */
export const LANGUAGE_STORAGE_KEY = 'aetheris.lang';

function readStoredLanguage(): Language | null {
  try {
    const stored = window.localStorage.getItem(LANGUAGE_STORAGE_KEY);
    return stored === 'uk' || stored === 'en' ? stored : null;
  } catch {
    // Storage can be blocked, for example in a private window: the default applies.
    return null;
  }
}

function storeLanguage(language: Language): void {
  try {
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
  } catch {
    // The choice simply is not remembered.
  }
}

interface I18nProviderProps {
  children: ReactNode;
  initialLanguage?: Language;
}

/**
 * Holds the current language, remembers it and keeps `<html lang>` in step.
 */
export function I18nProvider({ children, initialLanguage }: I18nProviderProps) {
  const [language, setLanguageState] = useState<Language>(() => initialLanguage ?? readStoredLanguage() ?? 'uk');

  useEffect(() => {
    document.documentElement.lang = language;
  }, [language]);

  const setLanguage = useCallback((next: Language) => {
    storeLanguage(next);
    setLanguageState(next);
  }, []);

  const value = useMemo<I18nValue>(
    () => ({
      language,
      setLanguage,
      t: (key: MessageKey, params?: MessageParams) => format(dictionaries[language][key], params),
    }),
    [language, setLanguage],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}
