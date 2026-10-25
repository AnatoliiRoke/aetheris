import { useId, useState } from 'react';
import type { MessageKey } from '../../i18n/uk';
import { useI18n } from '../../i18n/useI18n';

const questions = ['free', 'start', 'battles', 'race', 'mobile', 'support'] as const;

/**
 * The frequently asked questions, as an accordion with one answer open at a time.
 */
export function Faq() {
  const { t } = useI18n();
  const baseId = useId();
  const [open, setOpen] = useState<number | null>(0);

  return (
    <section className="section section--last" id="help" aria-labelledby="faq-title">
      <div className="container faq">
        <div className="section-head">
          <span className="eyebrow">{t('nav.help')}</span>
          <h2 className="h2" id="faq-title">
            {t('faq.title')}
          </h2>
        </div>
        <div className="faq__list">
          {questions.map((question, index) => {
            const buttonId = `${baseId}-q${index}`;
            const panelId = `${baseId}-a${index}`;
            const expanded = open === index;

            return (
              <div className="faq__item" key={question}>
                <h3 className="faq__q">
                  <button
                    className="faq__btn"
                    type="button"
                    id={buttonId}
                    aria-expanded={expanded}
                    aria-controls={panelId}
                    onClick={() => setOpen(expanded ? null : index)}
                  >
                    {t(`faq.${question}.q` as MessageKey)}
                    <span className="faq__icon" aria-hidden="true" />
                  </button>
                </h3>
                <div className="faq__a" id={panelId} role="region" aria-labelledby={buttonId} hidden={!expanded}>
                  {t(`faq.${question}.a` as MessageKey)}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
