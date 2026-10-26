import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { useSession } from '../../session/useSession';
import { PlayerLayout } from './PlayerLayout';

/**
 * The game sections; their screens arrive with later tasks. The server refuses them before
 * the e-mail confirmation (task 00006, decision 2), so the buttons say so.
 */
const sections: MessageKey[] = [
  'player.game.build',
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
      <section className="panel" aria-labelledby="sections-title">
        <h2 className="panel__title" id="sections-title">
          {t('player.game.sections')}
        </h2>
        <p className="lead">{t(confirmed ? 'player.game.soon' : 'player.game.locked')}</p>
        <div className="action-grid">
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
