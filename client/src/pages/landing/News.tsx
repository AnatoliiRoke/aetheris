import { useCallback } from 'react';
import { useApi } from '../../api/useApi';
import { artSize, newsImages } from '../../assets/images';
import { useI18n } from '../../i18n/useI18n';
import { useAsync } from './useAsync';

/**
 * The three latest news items.
 */
export function News() {
  const { t, language } = useI18n();
  const { content } = useApi();
  const loadNews = useCallback(() => content.getLatestNews(language), [content, language]);
  const state = useAsync(loadNews);
  const dates = new Intl.DateTimeFormat(language === 'uk' ? 'uk-UA' : 'en-GB', { dateStyle: 'short', timeZone: 'UTC' });

  return (
    <section className="section" id="news" aria-labelledby="news-title">
      <div className="container">
        <div className="section-head">
          <span className="eyebrow">{t('nav.news')}</span>
          <h2 className="h2" id="news-title">
            {t('news.title')}
          </h2>
        </div>

        {state.status === 'loading' ? <p className="meta" role="status">{t('common.loading')}</p> : null}
        {state.status === 'error' ? <p className="meta" role="alert">{t('news.error')}</p> : null}

        {state.status === 'ready' ? (
          <div className="grid-cards grid-cards--news">
            {state.data.map((item) => (
              <article className="card news-card" key={item.id}>
                <img className="card__media card__media--wide" src={newsImages[item.image]} alt="" {...artSize} loading="lazy" />
                <div className="card__body">
                  <time dateTime={item.publishedOn}>{dates.format(new Date(item.publishedOn))}</time>
                  <h3 className="h3">{item.title}</h3>
                  <p className="card__text">{item.excerpt}</p>
                  <a className="read-more" href={`#news-${item.id}`}>
                    <span>{t('news.readMore')}</span>
                    <span className="visually-hidden">: {item.title}</span> <span aria-hidden="true">→</span>
                  </a>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </div>
    </section>
  );
}
