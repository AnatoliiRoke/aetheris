import { useEffect, useRef } from 'react';
import { images } from '../../assets/images';
import { ArrowRightIcon } from '../../components/Icons';
import type { MessageKey } from '../../i18n/uk';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';

/**
 * How much slower than the page the star layer moves, for a soft parallax.
 */
const STAR_PARALLAX_FACTOR = 0.25;

const facts: { image: string; value: MessageKey; label: MessageKey }[] = [
  { image: images.planetTerra, value: 'hero.fact.universe.value', label: 'hero.fact.universe.label' },
  { image: images.planetGas, value: 'hero.fact.races.value', label: 'hero.fact.races.label' },
  { image: images.planetLava, value: 'hero.fact.cycle.value', label: 'hero.fact.cycle.label' },
];

/**
 * The first screen: the painted scene, the promise of the game and the main call to play.
 */
export function Hero() {
  const { t } = useI18n();
  const starsRef = useRef<HTMLImageElement | null>(null);

  // A light parallax of the star layer (task 00003, section 3.1). It is switched off for
  // people who asked the system for less motion.
  useEffect(() => {
    const stars = starsRef.current;
    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? true;

    if (!stars || reduceMotion) {
      return;
    }

    let frame = 0;
    const onScroll = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        stars.style.transform = `translate3d(0, ${window.scrollY * STAR_PARALLAX_FACTOR}px, 0)`;
      });
    };

    window.addEventListener('scroll', onScroll, { passive: true });

    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener('scroll', onScroll);
    };
  }, []);

  return (
    <section className="hero" id="top" aria-labelledby="hero-title">
      <img className="hero__bg" src={images.heroBackground} alt="" width={1200} height={896} fetchPriority="high" />
      <img ref={starsRef} className="hero__stars" src={images.heroStars} alt="" width={1200} height={896} />
      <div className="container hero__inner">
        <span className="badge">
          <span className="badge__dot" aria-hidden="true" />
          {t('hero.badge')}
        </span>
        <h1 className="hero__title" id="hero-title">
          {t('hero.title')}
        </h1>
        <p className="hero__text">{t('hero.text')}</p>
        <div className="hero__cta">
          <Link className="btn btn--primary btn--lg" to={routes.register}>
            <span>{t('common.playFree')}</span>
            <ArrowRightIcon className="btn__icon" />
          </Link>
          <Link className="btn btn--secondary btn--lg" to={routes.login}>
            {t('common.signIn')}
          </Link>
        </div>
        <p className="meta">{t('hero.meta')}</p>
        <ul className="facts">
          {facts.map((fact) => (
            <li className="fact" key={fact.value}>
              <img src={fact.image} alt="" width={48} height={48} />
              <span className="fact__text">
                <strong>{t(fact.value)}</strong>
                <span>{t(fact.label)}</span>
              </span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
