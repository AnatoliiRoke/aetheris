import { artSize, images, shipImages } from '../../assets/images';
import { Glyph } from '../../components/Glyph';
import type { MessageKey } from '../../i18n/uk';
import { useI18n } from '../../i18n/useI18n';
import { raceNameKey, raceOrder } from './raceKeys';

/**
 * The four races, each with its colour, glyph, mood and flagship (documentation/ui-art/01, section 4).
 */
export function Races() {
  const { t } = useI18n();

  return (
    <section className="races" id="races" aria-labelledby="races-title">
      <img className="races__bg" src={images.racesBackground} alt="" {...artSize} loading="lazy" />
      <div className="container">
        <div className="section-head">
          <span className="eyebrow">{t('nav.races')}</span>
          <h2 className="h2" id="races-title">
            {t('races.title')}
          </h2>
          <p className="lead">{t('races.lead')}</p>
        </div>
        <div className="grid-cards">
          {raceOrder.map((race) => {
            const name = t(raceNameKey(race));
            const key = (suffix: string) => `races.${race}.${suffix}` as MessageKey;

            return (
              <article className={`card race-card race-card--${race}`} key={race}>
                <img
                  className="card__media card__media--ship"
                  src={shipImages[race]}
                  alt={t('races.shipAlt', { race: name })}
                  {...artSize}
                  loading="lazy"
                />
                <div className="card__body">
                  <div className="race-card__title">
                    <Glyph race={race} />
                    <h3 className="h3">{name}</h3>
                  </div>
                  <p className="meta race-card__mood">
                    {t('races.moodLabel')} <strong>{t(key('mood'))}</strong>
                  </p>
                  <p className="card__text">{t(key('text'))}</p>
                  <ul className="traits" aria-label={t('races.traitsLabel')}>
                    <li className="chip">{t(key('trait1'))}</li>
                    <li className="chip">{t(key('trait2'))}</li>
                  </ul>
                </div>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}
