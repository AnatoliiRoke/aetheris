import { memo } from 'react';
import { technologyStatuses, type Technology } from '../../api/researchApi';
import { GameIcon, ResourceIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { formatAmount, formatDuration, formatSigned, lookup, reasonText } from './planetText';
import { ResearchProgress } from './QueueItem';
import { reportRender } from './renderProbe';

/**
 * The multiplier that changes nothing: a technology that is not learned yet gives no more and no
 * less than the body has, so its first level is measured against it.
 */
const neutralMultiplier = 1;
const percentFactor = 100;

/**
 * A technology of the tree as the design draws it: the name and the state, the description, the
 * effect now and per level, the cost of the next level one resource at a time with what is
 * short, the time, the reasons it is closed and the button that starts its research. The button
 * is inactive with the reason beside it when the server would refuse; the server checks
 * everything again (task 00020). A technology the player cannot reach yet is a dimmed card with
 * the name and the branch only.
 *
 * @param technology The technology.
 * @param name The name of the technology.
 * @param description The description of the technology.
 * @param branchName The name of the branch of the technology.
 * @param resourceName The name of a resource by its identifier.
 * @param resourceGroupCode The code of the group of a resource, for its icon.
 * @param unlockName The name of a subject the technology opens, by its localisation key.
 * @param timeMultiplier How many game seconds pass in a real second.
 * @param deadline The moment the research under way ends on the clock of the browser, or `null`.
 * @param busy Whether a command is running.
 * @param onResearch What to do when the player starts the research; it must be the same function between the draws, so a card whose data did not change is not drawn again.
 */
export const TechnologyCard = memo(function TechnologyCard({
  technology,
  name,
  description,
  branchName,
  resourceName,
  resourceGroupCode,
  unlockName,
  timeMultiplier,
  deadline,
  busy,
  onResearch,
}: {
  technology: Technology;
  name: string;
  description: string;
  branchName: string;
  resourceName: (id: number) => string;
  resourceGroupCode: (id: number) => string | null;
  unlockName: (key: string) => string;
  timeMultiplier: number;
  deadline: number | null;
  busy: boolean;
  onResearch: (technology: Technology) => void;
}) {
  reportRender('TechnologyCard');

  const { t, language } = useI18n();
  const status = technology.status;

  if (status === technologyStatuses.unknown) {
    return (
      <li className="tech-card tech-card--unknown">
        <div className="tech-card__head">
          <span aria-hidden="true" className="tech-card__mark">
            <GameIcon name="research" size={20} />
          </span>
          <div className="tech-card__id">
            <h3 className="building-card__name">{name}</h3>
            <span className="meta">{branchName}</span>
          </div>
          <span className="chip tech-status tech-status--unknown">{t('research.status.unknown')}</span>
        </div>
        <p className="meta">{t('research.unknownText')}</p>
        <button className="btn btn--secondary" disabled title={t('research.unknownText')} type="button">
          {t('research.start')}
        </button>
      </li>
    );
  }

  const next = technology.levels.find((level) => level.level === technology.learnedLevel + 1);
  const opens = (next?.effects ?? []).map((effect) => effect.unlockNameKey).filter((key): key is string => Boolean(key));
  const nowEffects = new Map(technology.currentEffects.filter((effect) => !effect.unlockNameKey).map((effect) => [effect.effectType, effect.value]));
  const gains = (next?.effects ?? []).filter((effect) => !effect.unlockNameKey);
  const shownEffects = gains.length > 0 ? gains : technology.currentEffects.filter((effect) => !effect.unlockNameKey);
  const multiplier = timeMultiplier > 0 ? timeMultiplier : 1;
  const missing = new Map((next?.missingResources ?? []).map((entry) => [entry.resourceTypeId, entry.amount]));
  const reasons: string[] = [];

  if (status === technologyStatuses.unavailable && technology.reasonCode === 'research_center_level_unmet') {
    reasons.push(t('research.centerNeeded', { level: technology.researchCenterLevel }));
  } else if (status === technologyStatuses.unavailable && technology.reasonCode) {
    reasons.push(reasonText(language, technology.reasonCode));
  }

  const isShort = status === technologyStatuses.available && missing.size > 0;
  const isResearching = status === technologyStatuses.researching;
  const disabledReason = isShort ? t('research.notEnough') : reasons.join('. ') || t(`research.status.${status}` as never);
  const showsLearned = technology.learnedLevel > 0 && !isResearching && status !== technologyStatuses.queued;
  const statusText = showsLearned ? t('research.learnedLevel', { level: technology.learnedLevel }) : t(`research.status.${status}` as never);
  const statusClass = showsLearned ? technologyStatuses.learned : status;
  const percent = (multiplierValue: number) => `${formatSigned((multiplierValue - neutralMultiplier) * percentFactor, language)}%`;
  const plateLabel = technology.learnedLevel > 0 ? t('research.nextLevel', { level: next?.level ?? technology.learnedLevel + 1 }) : t('planet.level', { level: next?.level ?? technology.learnedLevel + 1 });

  return (
    <li className="tech-card">
      <div className="tech-card__head">
        <span aria-hidden="true" className="tech-card__mark">
          <GameIcon name="research" size={20} />
        </span>
        <div className="tech-card__id">
          <h3 className="building-card__name">{name}</h3>
        </div>
        <span className={`chip tech-status tech-status--${statusClass}`}>{statusText}</span>
      </div>
      <p className="building-card__text">{description}</p>
      {shownEffects.length > 0 ? (
        <div className="building-card__block">
          <span className="civ-label">{t('research.effectLabel')}</span>
          <ul className="effect-list">
            {shownEffects.map((effect) => {
              const current = nowEffects.get(effect.effectType);
              const step = next ? effect.value - (current ?? neutralMultiplier) : null;
              const value = current === undefined
                ? step === null
                  ? t('research.noEffectYet')
                  : t('research.effectPerLevel', { value: `${formatSigned(step * percentFactor, language)}%` })
                : percent(current);

              return (
                <li className="effect-row" key={effect.effectType}>
                  <span>{lookup(language, `research.effectName.${effect.effectType}`, effect.effectType)}</span>
                  <strong className="num">{value}</strong>
                </li>
              );
            })}
          </ul>
        </div>
      ) : null}
      {opens.length > 0 ? <span className="meta">{t('research.opens', { names: opens.map(unlockName).join(', ') })}</span> : null}
      {isResearching && deadline !== null ? (
        <div className="building-card__next">
          <span className="meta">{t('planet.level', { level: next?.level ?? technology.learnedLevel + 1 })}</span>
          <ResearchProgress deadline={deadline} durationSeconds={(next?.durationSeconds ?? 0) / multiplier} label={t('research.timeLeftLabel')} />
        </div>
      ) : next ? (
        <div className="building-card__next">
          <span className="meta">{plateLabel}</span>
          <ul aria-label={t('build.requiredResources')} className="cost-list">
            {next.costs.map((cost) => {
              const short = missing.get(cost.resourceTypeId);

              return (
                <li className={short === undefined ? 'cost-list__row' : 'cost-list__row cost-list__row--short'} key={cost.resourceTypeId}>
                  <ResourceIcon groupCode={resourceGroupCode(cost.resourceTypeId)} />
                  <span className="cost-list__name">{resourceName(cost.resourceTypeId)}</span>
                  <span className="num">{formatAmount(cost.amount, language)}</span>
                  {short === undefined ? <span className="cost-list__missing" /> : <span className="cost-list__missing">{t('build.missingAmount', { amount: formatAmount(short, language) })}</span>}
                </li>
              );
            })}
          </ul>
          <div className="tech-card__time">
            <span>{t('research.duration')}</span>
            <span className="num">{formatDuration(language, next.durationSeconds / multiplier)}</span>
          </div>
        </div>
      ) : null}
      {reasons.length > 0 ? (
        <ul className="reason-list">
          {reasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      ) : null}
      {status === technologyStatuses.available && !isShort ? (
        <button className="btn btn--secondary" disabled={busy} type="button" onClick={() => onResearch(technology)}>
          {t('research.start')}
        </button>
      ) : (
        <button className="btn btn--secondary" disabled title={isResearching ? t('research.researchingNow') : disabledReason} type="button">
          {isResearching ? t('research.researchingNow') : t('research.start')}
        </button>
      )}
    </li>
  );
});
