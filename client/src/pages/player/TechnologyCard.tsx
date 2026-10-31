import { technologyStatuses, type Technology } from '../../api/researchApi';
import { useI18n } from '../../i18n/useI18n';
import { formatCoefficient, formatDuration, formatNumber, lookup, reasonText } from './planetText';

/**
 * A technology of the tree with its state, what its next level gives, its cost and time, and the
 * button that starts its research. The button is inactive with the reason beside it when the
 * server would refuse; the server checks everything again (task 00020).
 *
 * @param technology The technology.
 * @param name The name of the technology.
 * @param description The description of the technology.
 * @param resourceName The name of a resource by its identifier.
 * @param unlockName The name of a subject the technology opens, by its localisation key.
 * @param timeMultiplier How many game seconds pass in a real second.
 * @param busy Whether a command is running.
 * @param onResearch What to do when the player starts the research.
 */
export function TechnologyCard({
  technology,
  name,
  description,
  resourceName,
  unlockName,
  timeMultiplier,
  busy,
  onResearch,
}: {
  technology: Technology;
  name: string;
  description: string;
  resourceName: (id: number) => string;
  unlockName: (key: string) => string;
  timeMultiplier: number;
  busy: boolean;
  onResearch: () => void;
}) {
  const { t, language } = useI18n();
  const status = technology.status;
  const next = technology.levels.find((level) => level.level === technology.learnedLevel + 1);
  const opens = (next?.effects ?? []).map((effect) => effect.unlockNameKey).filter((key): key is string => Boolean(key));
  const gains = (next?.effects ?? []).filter((effect) => !effect.unlockNameKey);
  const multiplier = timeMultiplier > 0 ? timeMultiplier : 1;
  const reasons: string[] = [];

  if (status === technologyStatuses.unavailable && technology.reasonCode === 'research_center_level_unmet') {
    reasons.push(t('research.centerNeeded', { level: technology.researchCenterLevel }));
  } else if (status === technologyStatuses.unavailable && technology.reasonCode) {
    reasons.push(reasonText(language, technology.reasonCode));
  }

  return (
    <li className="tech-card">
      <div className="building-card__head">
        <h3 className="building-card__name">{name}</h3>
        <span className="chip">{t(`research.status.${status}` as never)}</span>
      </div>
      <p className="building-card__text">{description}</p>
      <span className="meta">{t('research.level', { learned: technology.learnedLevel, max: technology.maxLevel })}</span>
      {opens.length > 0 ? <span className="meta">{t('research.opens', { names: opens.map(unlockName).join(', ') })}</span> : null}
      {gains.length > 0 ? (
        <ul className="effect-list">
          {gains.map((effect, index) => (
            <li key={index}>
              {lookup(language, `research.effect.${effect.effectType}`, t('research.effect.generic', { type: effect.effectType, value: formatCoefficient(effect.value, language) }), {
                value: formatCoefficient(effect.value, language),
              })}
            </li>
          ))}
        </ul>
      ) : null}
      {next ? (
        <div className="building-card__next">
          <span className="meta">{t('research.nextLevel', { level: next.level })}</span>
          <dl className="facts facts--compact">
            <dt>{t('planet.cost')}</dt>
            <dd>{next.costs.map((cost) => `${resourceName(cost.resourceTypeId)} ${formatNumber(cost.amount, language)}`).join(', ')}</dd>
            <dt>{t('planet.duration')}</dt>
            <dd className="num">{formatDuration(language, next.durationSeconds / multiplier)}</dd>
          </dl>
        </div>
      ) : null}
      {reasons.length > 0 ? (
        <ul className="reason-list">
          {reasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      ) : null}
      {status === technologyStatuses.available ? (
        <button className="btn btn--secondary" disabled={busy} type="button" onClick={onResearch}>
          {t('research.start')}
        </button>
      ) : (
        <button className="btn btn--secondary" disabled title={reasons.join('. ') || t(`research.status.${status}` as never)} type="button">
          {t('research.start')}
        </button>
      )}
    </li>
  );
}
