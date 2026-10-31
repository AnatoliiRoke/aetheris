import type { Building, Planet } from '../../api/planetApi';
import { useI18n } from '../../i18n/useI18n';
import { effectText, formatDuration, formatNumber, lookup, namer, reasonText, requirementText } from './planetText';

type Names = ReturnType<typeof namer>;

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
