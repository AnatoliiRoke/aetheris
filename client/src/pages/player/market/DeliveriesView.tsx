import { useEffect, useRef } from 'react';
import { GameIcon } from '../../../components/GameIcon';
import type { MarketDeliveries, MarketDelivery } from '../../../api/marketApi';
import { useI18n } from '../../../i18n/useI18n';
import { formatClock, formatInteger } from './marketFormat';
import type { PlanetChoice } from './marketPlanets';
import { ListSkeleton, ReadError } from './MarketStates';
import { useIsPhone } from './useIsPhone';
import { useTick } from './useGameClock';
import type { ItemView } from './useMarketItems';

const arrived = 'arrived';
const minimumPercent = 2;
const percentOfWhole = 100;

/**
 * The tab "Deliveries": the goods that travel to the player and the goods of the sold lots, each with
 * a bar, a timer and a chip of the state. The timer counts here and nowhere else, so the rest of the
 * page does not draw again every second; when a delivery reaches its time the server is asked once.
 *
 * @param deliveries The deliveries, or `null` while they are read.
 * @param failed Whether the read failed.
 * @param nowMs The game time now, or `null` before the overview arrives.
 * @param timeMultiplier How many game seconds pass in a real second.
 * @param planets The bodies of the player, for the names of the ends of the route.
 * @param itemView How an item looks.
 * @param onRetry What to do when the player asks to read again.
 * @param onArrive What to do once when a delivery has reached its time.
 */
export function DeliveriesView({
  deliveries,
  failed,
  nowMs,
  timeMultiplier,
  planets,
  itemView,
  onRetry,
  onArrive,
}: {
  deliveries: MarketDeliveries | null;
  failed: boolean;
  nowMs: () => number | null;
  timeMultiplier: number;
  planets: PlanetChoice[];
  itemView: (resourceTypeId: number | null, partTypeId: number | null) => ItemView;
  onRetry: () => void;
  onArrive: () => void;
}) {
  const { t, language } = useI18n();
  const phone = useIsPhone();
  const travelling = [...(deliveries?.incoming ?? []), ...(deliveries?.outgoing ?? [])].some((delivery) => delivery.state !== arrived);

  useTick(travelling);

  const asked = useRef<ReadonlySet<number>>(new Set());
  const now = nowMs();

  useEffect(() => {
    if (now === null) {
      return;
    }

    for (const delivery of [...(deliveries?.incoming ?? []), ...(deliveries?.outgoing ?? [])]) {
      if (delivery.state !== arrived && Date.parse(delivery.arrivesAt) <= now && !asked.current.has(delivery.id)) {
        asked.current = new Set([...asked.current, delivery.id]);
        onArrive();
      }
    }
  }, [deliveries, now, onArrive]);

  if (deliveries === null) {
    return failed ? <ReadError onRetry={onRetry} /> : <ListSkeleton />;
  }

  if (deliveries.incoming.length === 0 && deliveries.outgoing.length === 0) {
    return (
      <section className="mk-panel">
        <p className="mk-panel__empty">{t('market.noDel')}</p>
      </section>
    );
  }

  const planetName = (planetId: number) => planets.find((planet) => planet.planetId === planetId)?.name ?? String(planetId);
  const row = (delivery: MarketDelivery, incoming: boolean) => {
    const done = delivery.state === arrived;
    const totalMs = Math.max(1, Date.parse(delivery.arrivesAt) - Date.parse(delivery.startedAt));
    const leftMs = Math.max(0, Date.parse(delivery.arrivesAt) - (now ?? Date.parse(delivery.startedAt)));
    const percent = done ? percentOfWhole : Math.max(minimumPercent, Math.round((1 - leftMs / totalMs) * percentOfWhole));
    const view = itemView(delivery.resourceTypeId, delivery.partTypeId);
    const route = incoming
      ? `${delivery.counterparty.nickname} → ${planetName(delivery.destinationPlanetId)}`
      : `${planetName(delivery.sourcePlanetId)} → ${delivery.counterparty.nickname}`;
    const left = done || leftMs === 0 ? '—' : `${formatClock(leftMs / 1000 / timeMultiplier)} ${t('market.left')}`;

    return (
      <div className={`mk-delivery${phone ? ' mk-delivery--phone' : ''}`} key={delivery.id}>
        <div className="mk-delivery__what">
          <GameIcon className={view.iconClass} name={view.iconName} size={24} />
          <span className="mk-delivery__text">
            <span className="mk-delivery__name">
              {view.name} × {formatInteger(delivery.quantity, language)}
            </span>
            <span className="mk-delivery__route">{route}</span>
          </span>
        </div>
        <div className="mk-delivery__bar">
          <span aria-label={view.name} aria-valuemax={percentOfWhole} aria-valuemin={0} aria-valuenow={percent} className="mk-meter" role="progressbar">
            <span className={`mk-meter__bar${done ? ' is-done' : ''}`} style={{ width: `${percent}%` }} />
          </span>
          <span className="mk-delivery__left">{left}</span>
        </div>
        <div className="mk-delivery__state">
          <span className={`mk-state${done ? ' mk-state--done' : ''}`}>
            <GameIcon name={done ? 'check' : 'clock'} size={13} />
            {done ? t('market.delivered') : t('market.transit')}
          </span>
        </div>
      </div>
    );
  };

  return (
    <>
      {(
        [
          ['market.incoming', deliveries.incoming, true],
          ['market.outgoing', deliveries.outgoing, false],
        ] as const
      ).map(([title, list, incoming]) => (
        <section aria-label={t(title)} className="mk-panel" key={title}>
          <h2 className="mk-panel__title">
            {t(title)} <span className="mk-panel__count">· {list.length}</span>
          </h2>
          {list.map((delivery) => row(delivery, incoming))}
          {list.length === 0 ? <p className="mk-panel__empty">{t('market.noDel')}</p> : null}
        </section>
      ))}
    </>
  );
}
