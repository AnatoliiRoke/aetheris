import { useCallback } from 'react';
import { useApi } from '../../api/useApi';
import { Glyph } from '../../components/Glyph';
import { useI18n } from '../../i18n/useI18n';
import { raceNameKey } from './raceKeys';
import { useAsync } from './useAsync';

/**
 * The places with their own colour in the table.
 */
const HIGHLIGHTED_PLACES = 3;

/**
 * Two letters of a player name for the avatar.
 */
const AVATAR_LETTERS = 2;

/**
 * The top 10 by development, the only ranking of stage 1 (production/01).
 */
export function Ranking() {
  const { t, language } = useI18n();
  const { content } = useApi();
  const loadPlayers = useCallback(() => content.getTopPlayers(), [content]);
  const state = useAsync(loadPlayers);
  const numbers = new Intl.NumberFormat(language === 'uk' ? 'uk-UA' : 'en-GB');

  return (
    <section className="section" id="ranking" aria-labelledby="ranking-title">
      <div className="container">
        <div className="section-head section-head--row">
          <div className="section-head section-head--flat">
            <span className="eyebrow">{t('nav.ranking')}</span>
            <h2 className="h2" id="ranking-title">
              {t('ranking.title')}
            </h2>
          </div>
          <span className="meta">{t('ranking.updated')}</span>
        </div>

        {state.status === 'loading' ? <p className="meta" role="status">{t('common.loading')}</p> : null}
        {state.status === 'error' ? <p className="meta" role="alert">{t('ranking.error')}</p> : null}
        {state.status === 'ready' && state.data.length === 0 ? <p className="meta">{t('ranking.empty')}</p> : null}

        {state.status === 'ready' && state.data.length > 0 ? (
          <div className="table-wrap">
            <table className="rank-table">
              <caption className="visually-hidden">{t('ranking.caption')}</caption>
              <thead>
                <tr>
                  <th scope="col">{t('ranking.place')}</th>
                  <th scope="col">{t('ranking.player')}</th>
                  <th scope="col">{t('ranking.race')}</th>
                  <th scope="col">{t('ranking.score')}</th>
                </tr>
              </thead>
              <tbody>
                {state.data.map((entry) => (
                  <tr key={entry.place}>
                    <td>
                      <span className={entry.place <= HIGHLIGHTED_PLACES ? `place place--${entry.place}` : 'place'}>{entry.place}</span>
                    </td>
                    <td>
                      <span className="player">
                        <span className="avatar" aria-hidden="true">
                          {entry.playerName.slice(0, AVATAR_LETTERS).toUpperCase()}
                        </span>
                        {entry.playerName}
                      </span>
                    </td>
                    <td>
                      <span className="race">
                        <Glyph race={entry.race} small />
                        {t(raceNameKey(entry.race))}
                      </span>
                    </td>
                    <td className="score">{numbers.format(entry.score)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}

        <div className="rank-actions">
          <a className="btn btn--secondary btn--lg" href="#ranking-full">
            <span>{t('ranking.full')}</span> <span aria-hidden="true">→</span>
          </a>
        </div>
      </div>
    </section>
  );
}
