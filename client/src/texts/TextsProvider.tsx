import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useApi } from '../api/useApi';
import { serverConfig } from '../config/server';
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
 * the admin panel shows up without a new release and without a restart. A pack that failed to
 * load (the server was not there yet) is asked for again after a pause, so the names appear by
 * themselves when the server is up.
 */
export function TextsProvider({ children }: { children: ReactNode }) {
  const { texts } = useApi();
  const { language } = useI18n();
  const [packs, setPacks] = useState<Packs>({});
  const [round, setRound] = useState(0);
  const failures = useRef(0);

  const reload = useCallback(() => setRound((value) => value + 1), []);

  useEffect(() => {
    let current = true;
    let timer: ReturnType<typeof setTimeout> | null = null;
    const languages: Language[] = language === fallbackLanguage ? [language] : [language, fallbackLanguage];

    Promise.all(languages.map((wanted) => texts.pack(wanted).then((pack) => [wanted, pack.texts] as const))).then(
      (loaded) => {
        if (current) {
          failures.current = 0;
          // A pack that says the same keeps its old object, so a read on the return to the tab wakes
          // no reader of the texts when nothing was renamed.
          setPacks((previous) => {
            const changed = loaded.filter(([wanted, pack]) => JSON.stringify(previous[wanted]) !== JSON.stringify(pack));

            return changed.length === 0 ? previous : { ...previous, ...Object.fromEntries(changed) };
          });
        }
      },
      () => {
        // Without the server the texts that were loaded stay; the keys show where none were. The
        // read is repeated after a pause, the last pause repeating.
        if (current) {
          const pauses = serverConfig.retryDelaysMs;

          timer = setTimeout(() => setRound((value) => value + 1), pauses[Math.min(failures.current, pauses.length - 1)]);
          failures.current += 1;
        }
      },
    );

    return () => {
      current = false;

      if (timer) {
        clearTimeout(timer);
      }
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
