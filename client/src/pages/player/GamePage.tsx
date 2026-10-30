import { useMemo } from 'react';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { formatDuration, formatNumber, namer, secondsLeft } from './planetText';
import { PlayerLayout } from './PlayerLayout';
import { ProcessesPanel } from './ProcessesPanel';
import { ProductionPanel } from './ProductionPanel';
import { ResearchPanel } from './ResearchPanel';
import { TerraformingPanel } from './TerraformingPanel';
import { useEconomy } from './useEconomy';
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
  const { name } = useTexts();
  const { session } = useSession();
  useDocumentTitle(t('meta.game.title'));

  if (session.status !== 'signed-in') {
    return null;
  }

  const { me } = session;
  const confirmed = me.emailConfirmed;

  return (
    <PlayerLayout headerExtra={me.civilization ? <Credits planetId={me.civilization.planetId} /> : null}>
      <h1 className="player__title">{t('player.game.title', { nickname: me.nickname })}</h1>
      {me.civilization ? (
        <section className="panel" aria-labelledby="home-title">
          <h2 className="panel__title" id="home-title">
            {t('player.game.home')}
          </h2>
          <dl className="facts">
            <dt>{t('player.game.race')}</dt>
            <dd>{name(me.civilization.raceNameKey)}</dd>
            <dt>{t('player.game.planet')}</dt>
            <dd>{me.civilization.planetName}</dd>
          </dl>
        </section>
      ) : null}
      {me.civilization && confirmed ? <QueueSummary planetId={me.civilization.planetId} /> : null}
      {me.civilization && confirmed ? <ResearchPanel planetId={me.civilization.planetId} /> : null}
      {me.civilization && confirmed ? <ProductionPanel planetId={me.civilization.planetId} /> : null}
      {me.civilization && confirmed ? <ProcessesPanel planetId={me.civilization.planetId} /> : null}
      {me.civilization && confirmed ? <TerraformingPanel /> : null}
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
  const { text } = useTexts();
  const names = useMemo(() => (planet ? namer(planet, language, text) : null), [planet, language, text]);
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

/**
 * The balances of the currencies of the civilization in the header of the game: the amount in
 * the short form with the income per hour, and the full values in the hint (task 00015).
 *
 * The names of the currencies come from the server with the texts of the game data, so the
 * client knows no currency by name.
 */
function Credits({ planetId }: { planetId: number }) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const { balances } = useEconomy(null);
  const { planet } = usePlanet(planetId);
  const names = useMemo(
    () => new Map((planet?.resourceTypes ?? []).map((resource) => [resource.id, resource.nameKey])),
    [planet],
  );

  if (balances.length === 0) {
    return (
      <span className="badge" title={t('economy.credits')}>
        —
      </span>
    );
  }

  return (
    <div className="player__nav" aria-label={t('economy.credits')}>
      {balances.map((balance) => {
        const nameKey = names.get(balance.resourceTypeId);
        const name = (nameKey ? text(nameKey) : undefined) ?? t('economy.currency', { id: balance.resourceTypeId });
        const amount = shortValue(balance.amount, language);
        const incomeValue = shortValue(balance.incomePerHour, language);
        const income = incomeValue === '—' ? '—' : `+${incomeValue}${t('economy.perHourShort')}`;
        const title = `${name}: ${fullValue(balance.amount, language)} (${t('economy.income')}: ${balance.incomePerHour > 0 ? '+' : ''}${fullValue(balance.incomePerHour, language)})`;

        return (
          <span key={balance.resourceTypeId} className="badge" title={title}>
            <span>{name}</span> <span className="num">{amount}</span> <span className="meta num">({income})</span>
          </span>
        );
      })}
    </div>
  );
}

/**
 * A number of the server in the short form of the language; the full value stays in the hint.
 */
function shortValue(value: number, language: Language): string {
  if (!Number.isFinite(value) || value === 0) {
    return '—';
  }

  return new Intl.NumberFormat(language === 'uk' ? 'uk-UA' : 'en-GB', {
    notation: 'compact',
    maximumFractionDigits: 1,
  }).format(value);
}

/**
 * A number of the server in the full form of the language; a value without data is a
 * placeholder, never a made-up number (task 00010).
 */
function fullValue(value: number, language: Language): string {
  if (!Number.isFinite(value) || value === 0) {
    return '—';
  }

  return formatNumber(value, language);
}
