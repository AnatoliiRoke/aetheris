import type { Building, Planet, QueueItem } from '../../api/planetApi';
import { Alert } from '../../components/Alert';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { effectText, formatDuration, formatNumber, lookup, namer, reasonText, requirementText, secondsLeft } from './planetText';

type Names = ReturnType<typeof namer>;

/**
 * The balance of the body and its storage by group, with the alarm of a full group.
 */
export function ResourcesPanel({ planet, names }: { planet: Planet; names: Names }) {
  const { t, language } = useI18n();
  const fullGroups = planet.storageGroups.filter((group) => group.isFull);

  return (
    <section className="panel" aria-labelledby="planet-resources">
      <h2 className="panel__title" id="planet-resources">
        {t('planet.resources')}
      </h2>
      {fullGroups.map((group) => (
        <Alert key={group.resourceGroupId} kind="info" title={t('planet.storageFull.title')}>
          {t('planet.storageFull.text', { group: names.group(group.resourceGroupId) })}
        </Alert>
      ))}
      <div className="planet-table-wrap">
        <table className="planet-table">
          <thead>
            <tr>
              <th scope="col">{t('planet.resource')}</th>
              <th scope="col">{t('planet.amount')}</th>
              <th scope="col">{t('planet.rate')}</th>
            </tr>
          </thead>
          <tbody>
            {planet.resources.map((resource) => (
              <tr key={resource.resourceTypeId}>
                <td>{names.resource(resource.resourceTypeId)}</td>
                <td className="num">{formatNumber(resource.amount, language)}</td>
                <td className="num">{resource.ratePerHour > 0 ? `+${formatNumber(resource.ratePerHour, language)}` : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h3 className="planet-subtitle">{t('planet.storage')}</h3>
      <ul className="storage-list">
        {planet.storageGroups.map((group) => (
          <li key={group.resourceGroupId} className="storage-row">
            <span className="storage-row__name">{names.group(group.resourceGroupId)}</span>
            <meter
              className="storage-row__meter"
              min={0}
              max={Math.max(group.capacity, group.stored, 1)}
              value={group.stored}
              aria-label={names.group(group.resourceGroupId)}
            />
            <span className="num">
              {t('planet.storageGroup', { stored: formatNumber(group.stored, language), capacity: formatNumber(group.capacity, language) })}
            </span>
            {group.isFull ? <span className="badge badge--warning">{t('planet.storageFullBadge')}</span> : null}
          </li>
        ))}
      </ul>
    </section>
  );
}

/**
 * The energy of the body now and once the queue is done.
 */
export function EnergyPanel({ planet }: { planet: Planet }) {
  const { t, language } = useI18n();
  const { energy } = planet;

  return (
    <section className="panel" aria-labelledby="planet-energy">
      <h2 className="panel__title" id="planet-energy">
        {t('planet.energy')}
      </h2>
      <dl className="facts">
        <dt>{t('planet.energyOutput')}</dt>
        <dd className="num">{formatNumber(energy.output, language)}</dd>
        <dt>{t('planet.energyConsumption')}</dt>
        <dd className="num">{formatNumber(energy.consumption, language)}</dd>
        <dt>{t('planet.energyBalance')}</dt>
        <dd className="num">{formatNumber(energy.balance, language)}</dd>
        <dt>{t('planet.energyQueued')}</dt>
        <dd className="num">{formatNumber(energy.queuedBalance, language)}</dd>
      </dl>
    </section>
  );
}

/**
 * The construction queue: what is built, the timer, what waits and the cancel.
 */
export function QueuePanel({
  planet,
  names,
  receivedAt,
  now,
  busy,
  onCancel,
}: {
  planet: Planet;
  names: Names;
  receivedAt: number;
  now: number;
  busy: boolean;
  onCancel: (item: QueueItem) => void;
}) {
  const { t, language } = useI18n();
  const { items, slotCount } = planet.queue;

  return (
    <section className="panel" aria-labelledby="planet-queue">
      <div className="panel__head">
        <h2 className="panel__title" id="planet-queue">
          {t('planet.queue')}
        </h2>
        <span className="meta">{t('planet.queueSlots', { used: items.length, total: slotCount })}</span>
      </div>
      {items.length === 0 ? <p className="lead">{t('planet.queueEmpty')}</p> : null}
      <ol className="queue-list">
        {items.map((item) => {
          const name = names.building(item.buildingTypeId);

          return (
            <li key={item.id} className="queue-item">
              <div className="queue-item__body">
                <strong>{t('planet.queueItem', { name, level: item.targetLevel })}</strong>
                <span className="meta">
                  {item.finishesAt ? t('planet.queueUnderWay') : t('planet.queueWaiting')}
                  {' · '}
                  <QueueTime planet={planet} item={item} receivedAt={receivedAt} now={now} language={language} />
                </span>
                <span className="meta">{names.amounts(item.costs)}</span>
              </div>
              <button
                className="btn btn--secondary"
                type="button"
                disabled={busy}
                aria-label={t('planet.queueCancelLabel', { name, level: item.targetLevel })}
                title={t('planet.queueCancelLabel', { name, level: item.targetLevel })}
                onClick={() => onCancel(item)}
              >
                {t('planet.queueCancel')}
              </button>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

function QueueTime({ planet, item, receivedAt, now, language }: { planet: Planet; item: QueueItem; receivedAt: number; now: number; language: Language }) {
  const { t } = useI18n();

  if (!item.finishesAt) {
    return <span className="num">{formatDuration(language, item.durationSeconds / (planet.timeMultiplier || 1))}</span>;
  }

  const left = secondsLeft(planet, item.finishesAt, receivedAt, now);

  return left > 0 ? (
    <span className="num" role="timer">
      {t('planet.timeLeft', { time: formatDuration(language, left) })}
    </span>
  ) : (
    <span>{t('planet.finishing')}</span>
  );
}

/**
 * A building with its level, the next level and the button that queues it. The button is
 * inactive with the reasons in its hint when the server would refuse; the server checks
 * everything again.
 */
export function BuildingCard({
  building,
  names,
  planet,
  busy,
  onEnqueue,
}: {
  building: Building;
  names: Names;
  planet: Planet;
  busy: boolean;
  onEnqueue: (building: Building) => void;
}) {
  const { t, language } = useI18n();
  const { next } = building;
  const name = names.building(building.buildingTypeId);
  const reasons = [
    ...next.unmetRequirements.map((requirement) => requirementText(language, requirement, names.entities)),
    ...next.reasonCodes.filter((code) => code !== 'planets_requirement_unmet').map((code) => reasonText(language, code)),
  ];

  if (next.missingResources.length > 0) {
    reasons.push(t('planet.missing', { list: names.amounts(next.missingResources) }));
  }

  const hint = reasons.join('. ');
  const reasonsId = `building-${building.buildingTypeId}-reasons`;

  return (
    <li className="building-card">
      <div className="building-card__head">
        <h3 className="building-card__name">{name}</h3>
        <span className="badge">{building.level > 0 ? t('planet.level', { level: building.level }) : t('planet.notBuilt')}</span>
      </div>
      <p className="building-card__text">{lookup(language, building.descriptionKey, '', undefined, names.entities)}</p>
      {building.queuedLevel > building.level ? <p className="meta">{t('planet.queuedTo', { level: building.queuedLevel })}</p> : null}
      {building.effects.length > 0 ? (
        <ul className="effect-list">
          {building.effects.map((effect, index) => (
            <li key={index}>{effectText(language, effect, names)}</li>
          ))}
        </ul>
      ) : null}
      {next.durationSeconds !== null ? (
        <div className="building-card__next">
          <span className="meta">{t('planet.nextLevel', { level: next.level })}</span>
          <dl className="facts facts--compact">
            <dt>{t('planet.cost')}</dt>
            <dd>{names.amounts(next.costs)}</dd>
            <dt>{t('planet.duration')}</dt>
            <dd className="num">{formatDuration(language, next.durationSeconds / (planet.timeMultiplier || 1))}</dd>
          </dl>
          <span className="meta">{t('planet.energyAfter', { value: formatNumber(next.energyBalanceAfter, language) })}</span>
        </div>
      ) : null}
      <button
        className="btn btn--secondary"
        type="button"
        disabled={busy || !next.canEnqueue}
        title={next.canEnqueue ? undefined : hint}
        aria-describedby={next.canEnqueue ? undefined : reasonsId}
        onClick={() => onEnqueue(building)}
      >
        {building.queuedLevel > 0 ? t('planet.upgrade') : t('planet.build')}
      </button>
      {next.canEnqueue ? null : (
        <ul className="reason-list" id={reasonsId}>
          {reasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      )}
    </li>
  );
}
