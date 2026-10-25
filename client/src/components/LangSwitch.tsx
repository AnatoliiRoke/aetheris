import type { CSSProperties } from 'react';
import type { Language } from '../i18n/context';
import { useI18n } from '../i18n/useI18n';

const languages: { code: Language; label: string }[] = [
  { code: 'uk', label: 'UA' },
  { code: 'en', label: 'EN' },
];

/**
 * The UA / EN switch. The language names are not translated: a visitor must recognise
 * their own language whatever the current one is.
 */
export function LangSwitch({ style }: { style?: CSSProperties }) {
  const { language, setLanguage, t } = useI18n();

  return (
    <div className="lang-switch" role="group" aria-label={t('common.language')} style={style}>
      {languages.map(({ code, label }) => (
        <button key={code} type="button" lang={code} aria-pressed={language === code} onClick={() => setLanguage(code)}>
          {label}
        </button>
      ))}
    </div>
  );
}
