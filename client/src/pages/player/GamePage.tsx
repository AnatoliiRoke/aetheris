import { useMemo } from 'react';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { formatDuration, namer, secondsLeft } from './planetText';
import { PlayerLayout } from './PlayerLayout';
import { useNow, usePlanet } from './usePlanet';

/**
 * The game sections whose screens arrive with later tasks. The server refuses them before
 * the e-mail confirmation (task 00006, decision 2), so the buttons say so. The construction
 * opens the planet (task 00011).
 */
const sections: MessageKey[] = [
  'player.game.research',
  'player.game.market',
  'player.game.chat',
  'player.game.flights',
  'player.game.clans',
];

/**
 * The start screen of a signed-in player after the first step: the civilization and the
 * entrances to the game sections.
 */
export function GamePage() {
  const { t } = useI18n();
  const { session } = useSession();
  useDocumentTitle(t('meta.game.title'));

  if (session.status !== 'signed-in') {
    return null;
  }

  const { me } = session;
  const confirmed = me.emailConfirmed;

  return (
    <PlayerLayout>
      <h1 className="player__title">{t('player.game.title', { nickname: me.nickname })}</h1>
      {me.civilization ? (
        <section className="panel" aria-labelledby="home-title">
          <h2 className="panel__title" id="home-title">
            {t('player.game.home')}
          </h2>
          <dl className="facts">
            <dt>{t('player.game.race')}</dt>
            <dd>{t(`races.${me.civilization.raceCode}.name` as MessageKey)}</dd>
            <dt>{t('player.game.planet')}</dt>
            <dd>{me.civilization.planetName}</dd>
          </dl>
        </section>
      ) : null}
      {me.civilization && confirmed ? <QueueSummary planetId={me.civilization.planetId} /> : null}
      <section className="panel" aria-labelledby="sections-title">
        <h2 className="panel__title" id="sections-title">
          {t('player.game.sections')}
        </h2>
        <p className="lead">{t(confirmed ? 'player.game.soon' : 'player.game.locked')}</p>
        <div className="action-grid">
          {confirmed ? (
            <Link className="btn btn--primary btn--lg" to={routes.planet}>
              {t('player.game.build')}
            </Link>
          ) : (
            <button className="btn btn--secondary btn--lg" type="button" disabled title={t('player.game.locked')}>
              {t('player.game.build')}
            </button>
          )}
          {sections.map((key) => (
            <button key={key} className="btn btn--secondary btn--lg" type="button" disabled title={t(confirmed ? 'player.game.soon' : 'player.game.locked')}>
              {t(key)}
            </button>
          ))}
        </div>
      </section>
    </PlayerLayout>
  );
}

/**
 * The construction queue of the home planet on the start screen, from the real data.
 */
function QueueSummary({ planetId }: { planetId: number }) {
  const { t, language } = useI18n();
  const { planet, receivedAt } = usePlanet(planetId);
  const now = useNow();
  const names = useMemo(() => (planet ? namer(planet, language) : null), [planet, language]);
  const items = planet?.queue.items ?? [];

  return (
    <section className="panel" aria-labelledby="queue-title">
      <div className="panel__head">
        <h2 className="panel__title" id="queue-title">
          {t('player.game.queue')}
        </h2>
        <Link className="text-btn" to={routes.planet}>
          {t('player.game.openPlanet')}
        </Link>
      </div>
      {!planet || !names ? (
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      ) : items.length === 0 ? (
        <p className="lead">{t('player.game.queueEmpty')}</p>
      ) : (
        <ol className="queue-list">
          {items.map((item) => (
            <li key={item.id} className="queue-item">
              <div className="queue-item__body">
                <strong>{t('planet.queueItem', { name: names.building(item.buildingTypeId), level: item.targetLevel })}</strong>
                <span className="meta num">
                  {item.finishesAt
                    ? t('planet.timeLeft', { time: formatDuration(language, secondsLeft(planet, item.finishesAt, receivedAt, now)) })
                    : t('planet.queueWaiting')}
                </span>
              </div>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
