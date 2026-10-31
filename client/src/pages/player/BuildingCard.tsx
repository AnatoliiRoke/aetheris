import type { Building, Planet } from '../../api/planetApi';
import { ResourceIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { effectText, formatAmount, formatDuration, lookup, namer, reasonText, requirementText } from './planetText';

type Names = ReturnType<typeof namer>;

/**
 * The type of a requirement that asks for a level of another building; the server names it.
 */
const buildingLevelRequirement = 'building_level';

/**
 * A building as the design draws it: the name and the level, the description, the effects of the
 * level built, the resources of the next level one by one with what is short, the time, the
 * energy once it is done, and the button that queues it. The button is inactive with the reasons
 * in its hint when the server would refuse; the server checks everything again.
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
  const missing = new Map(next.missingResources.map((entry) => [entry.resourceTypeId, entry.amount]));
  // A building that is not built and has a requirement out of reach is closed: the card shows no
  // cost and says what it opens with, instead of what to prepare for.
  const isClosed = building.level === 0 && next.unmetRequirements.some((requirement) => requirement.isClosed);
  const reasons = [
    ...next.unmetRequirements.map((requirement) =>
      isClosed && requirement.isClosed && requirement.requirementType === buildingLevelRequirement
        ? t('build.opensWith', { name: lookup(language, requirement.targetNameKey, requirement.targetNameKey ?? String(requirement.targetId), undefined, names.entities), level: requirement.targetLevel })
        : requirementText(language, requirement, names.entities),
    ),
    ...next.reasonCodes.filter((code) => code !== 'planets_requirement_unmet').map((code) => reasonText(language, code)),
  ];
  const hint = [...reasons, ...(next.missingResources.length > 0 && !isClosed ? [t('planet.missing', { list: names.amounts(next.missingResources) })] : [])].join('. ');
  const reasonsId = `building-${building.buildingTypeId}-reasons`;
  const isTopLevel = building.level >= building.maxLevel;
  const energyShort = next.energyBalanceAfter < 0;

  return (
    <li className="building-card">
      <div className="building-card__head">
        <h3 className="building-card__name">{name}</h3>
        <span className="badge">{building.level > 0 ? t('planet.level', { level: building.level }) : isClosed ? t('build.closed') : t('planet.notBuilt')}</span>
      </div>
      <p className="building-card__text">{lookup(language, building.descriptionKey, '', undefined, names.entities)}</p>
      {building.queuedLevel > building.level ? <p className="meta">{t('planet.queuedTo', { level: building.queuedLevel })}</p> : null}
      <div className="building-card__block">
        <span className="civ-label">{t('build.effects')}</span>
        {building.effects.length > 0 ? (
          <ul className="effect-list">
            {building.effects.map((effect, index) => (
              <li key={index}>{effectText(language, effect, names)}</li>
            ))}
          </ul>
        ) : (
          <span className="meta">{t('build.noEffects')}</span>
        )}
      </div>
      {next.durationSeconds !== null && !isClosed ? (
        <div className="building-card__next">
          <span className="meta">{t('planet.nextLevel', { level: next.level })}</span>
          <span className="civ-label">{t('build.requiredResources')}</span>
          <ul aria-label={t('build.requiredResources')} className="cost-list">
            {next.costs.map((cost) => {
              const short = missing.get(cost.resourceTypeId);

              return (
                <li className={short === undefined ? 'cost-list__row' : 'cost-list__row cost-list__row--short'} key={cost.resourceTypeId}>
                  <ResourceIcon groupCode={names.resourceGroupCode(cost.resourceTypeId)} />
                  <span className="cost-list__name">{names.resource(cost.resourceTypeId)}</span>
                  <span className="num">{formatAmount(cost.amount, language)}</span>
                  {short === undefined ? null : <span className="cost-list__missing">{t('build.missingAmount', { amount: formatAmount(short, language) })}</span>}
                </li>
              );
            })}
          </ul>
          <dl className="facts facts--compact">
            <dt>{t('build.duration')}</dt>
            <dd className="num">{formatDuration(language, next.durationSeconds / (planet.timeMultiplier || 1))}</dd>
            <dt>{t('build.energyAfter')}</dt>
            <dd className={energyShort ? 'num cost-list__missing' : 'num'}>{t('build.energyPair', { output: formatAmount(next.energyOutputAfter, language), consumption: formatAmount(next.energyConsumptionAfter, language) })}</dd>
          </dl>
        </div>
      ) : isTopLevel ? (
        <p className="meta">{t('build.topLevel')}</p>
      ) : null}
      <button
        className="btn btn--secondary"
        type="button"
        disabled={busy || !next.canEnqueue}
        title={next.canEnqueue ? undefined : hint}
        aria-describedby={next.canEnqueue || reasons.length === 0 ? undefined : reasonsId}
        onClick={() => onEnqueue(building)}
      >
        {building.queuedLevel > 0 ? t('planet.upgrade') : t('planet.build')}
      </button>
      {next.canEnqueue || reasons.length === 0 ? null : (
        <ul className="reason-list" id={reasonsId}>
          {reasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      )}
    </li>
  );
}
