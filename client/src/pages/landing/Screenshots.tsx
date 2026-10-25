import { useRef, useState, type KeyboardEvent } from 'react';
import { artSize, images } from '../../assets/images';
import type { MessageKey } from '../../i18n/uk';
import { useI18n } from '../../i18n/useI18n';

type Screen = keyof typeof images.screens;

const screens: Screen[] = ['overview', 'starmap', 'constructor', 'market'];

/**
 * The screenshot gallery: one large frame and the thumbnails. The arrow keys move between
 * the thumbnails.
 */
export function Screenshots() {
  const { t } = useI18n();
  const [selected, setSelected] = useState(0);
  const thumbRefs = useRef<(HTMLButtonElement | null)[]>([]);

  const title = (screen: Screen) => t(`screens.${screen}` as MessageKey);
  const current = screens[selected];

  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') {
      return;
    }

    event.preventDefault();
    const next = (index + (event.key === 'ArrowRight' ? 1 : -1) + screens.length) % screens.length;
    setSelected(next);
    thumbRefs.current[next]?.focus();
  };

  return (
    <section className="section" id="screens" aria-labelledby="screens-title">
      <div className="container gallery">
        <div className="section-head">
          <span className="eyebrow">{t('screens.eyebrow')}</span>
          <h2 className="h2" id="screens-title">
            {t('screens.title')}
          </h2>
        </div>
        <figure>
          <div className="gallery__stage">
            <img src={images.screens[current]} alt={t('screens.alt', { title: title(current) })} {...artSize} loading="lazy" />
          </div>
          <figcaption className="meta gallery__caption" aria-live="polite">
            {t('screens.caption', { index: selected + 1, total: screens.length, title: title(current) })}
          </figcaption>
        </figure>
        <div className="gallery__thumbs" role="group" aria-label={t('screens.choose')}>
          {screens.map((screen, index) => (
            <button
              key={screen}
              ref={(element) => {
                thumbRefs.current[index] = element;
              }}
              className="thumb"
              type="button"
              aria-pressed={index === selected}
              onClick={() => setSelected(index)}
              onKeyDown={(event) => onKeyDown(event, index)}
            >
              <img src={images.screens[screen]} alt="" {...artSize} loading="lazy" />
              <span className="thumb__label">
                <span>{title(screen)}</span>
                <span className="thumb__now">{t('screens.now')}</span>
              </span>
            </button>
          ))}
        </div>
      </div>
    </section>
  );
}
