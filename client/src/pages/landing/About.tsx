import { artSize, images } from '../../assets/images';
import type { MessageKey } from '../../i18n/uk';
import { useI18n } from '../../i18n/useI18n';

type AboutCard = keyof typeof images.about;

const cards: AboutCard[] = ['planets', 'constructor', 'market', 'battle'];

/**
 * The four pillars of the game.
 */
export function About() {
  const { t } = useI18n();

  return (
    <section className="section" id="about" aria-labelledby="about-title">
      <div className="container">
        <div className="section-head">
          <span className="eyebrow">{t('nav.about')}</span>
          <h2 className="h2" id="about-title">
            {t('about.title')}
          </h2>
          <p className="lead">{t('about.lead')}</p>
        </div>
        <div className="grid-cards">
          {cards.map((card) => (
            <article className="card" key={card}>
              <img className="card__media" src={images.about[card]} alt="" {...artSize} loading="lazy" />
              <div className="card__body">
                <h3 className="h3">{t(`about.${card}.title` as MessageKey)}</h3>
                <p className="card__text">{t(`about.${card}.text` as MessageKey)}</p>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
