import { artImage, artSize, images } from '../../assets/images';
import { Glyph } from '../../components/Glyph';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { usePublicRaces } from './usePublicRaces';

/**
 * The races, each with its colour, glyph, mood and flagship (documentation/ui-art/01, section 4).
 * The races, their texts and pictures come from the data of the server (task 00022).
 */
export function Races() {
  const { t } = useI18n();
  const { name } = useTexts();
  const races = usePublicRaces();

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
        {races.status === 'loading' ? <p className="meta" role="status">{t('common.loading')}</p> : null}
        {races.status === 'error' ? <p className="meta" role="alert">{t('races.error')}</p> : null}
        {races.status === 'ready' ? (
          <div className="grid-cards">
            {races.data.map((race) => {
              const raceName = name(race.nameKey);
              const picture = artImage(race.imageKey);

              return (
                <article className={race.colorKey ? `card race-card race-card--${race.colorKey}` : 'card race-card'} key={race.id}>
                  {picture ? (
                    <img
                      className="card__media card__media--ship"
                      src={picture}
                      alt={t('races.shipAlt', { race: raceName })}
                      {...artSize}
                      loading="lazy"
                    />
                  ) : (
                    <div className="card__media card__media--ship card__media--placeholder" role="img" aria-label={t('races.shipAlt', { race: raceName })} />
                  )}
                  <div className="card__body">
                    <div className="race-card__title">
                      <Glyph colorKey={race.colorKey} />
                      <h3 className="h3">{raceName}</h3>
                    </div>
                    <p className="meta race-card__mood">
                      {t('races.moodLabel')} <strong>{name(race.moodKey)}</strong>
                    </p>
                    <p className="card__text">{name(race.descriptionKey)}</p>
                    <ul className="traits" aria-label={t('races.traitsLabel')}>
                      {race.traitKeys.map((key) => (
                        <li className="chip" key={key}>
                          {name(key)}
                        </li>
                      ))}
                    </ul>
                  </div>
                </article>
              );
            })}
          </div>
        ) : null}
      </div>
    </section>
  );
}
