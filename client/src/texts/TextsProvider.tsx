import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useApi } from '../api/useApi';
import { fallbackLanguage, type Language } from '../i18n/context';
import { useI18n } from '../i18n/useI18n';
import { TextsContext, type TextsValue } from './context';

type Packs = Partial<Record<Language, Record<string, string>>>;

/**
 * Keeps the texts of the game data in memory (task 00022, decision 2): the pack of the current
 * language and the pack of the fallback language. The names of resources, races or buildings
 * never live in the files of the client.
 *
 * The packs are asked for again whenever the page comes back into view, so a name changed in
 * the admin panel shows up without a new release and without a restart.
 */
export function TextsProvider({ children }: { children: ReactNode }) {
  const { texts } = useApi();
  const { language } = useI18n();
  const [packs, setPacks] = useState<Packs>({});
  const [round, setRound] = useState(0);

  const reload = useCallback(() => setRound((value) => value + 1), []);

  useEffect(() => {
    let current = true;
    const languages: Language[] = language === fallbackLanguage ? [language] : [language, fallbackLanguage];

    Promise.all(languages.map((wanted) => texts.pack(wanted).then((pack) => [wanted, pack.texts] as const))).then(
      (loaded) => {
        if (current) {
          setPacks((previous) => ({ ...previous, ...Object.fromEntries(loaded) }));
        }
      },
      () => {
        // Without the server the texts that were loaded stay; the keys show where none were.
      },
    );

    return () => {
      current = false;
    };
  }, [texts, language, round]);

  useEffect(() => {
    const refresh = () => {
      if (document.visibilityState === 'visible') {
        reload();
      }
    };

    document.addEventListener('visibilitychange', refresh);
    window.addEventListener('focus', refresh);

    return () => {
      document.removeEventListener('visibilitychange', refresh);
      window.removeEventListener('focus', refresh);
    };
  }, [reload]);

  const value = useMemo<TextsValue>(() => {
    const text = (key: string | null | undefined) =>
      key ? (packs[language]?.[key] ?? packs[fallbackLanguage]?.[key]) : undefined;

    return {
      text,
      name: (key, fallback) => text(key) ?? key ?? fallback ?? '',
      reload,
    };
  }, [packs, language, reload]);

  return <TextsContext.Provider value={value}>{children}</TextsContext.Provider>;
}
