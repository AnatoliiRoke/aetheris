import { useMemo, useState } from 'react';
import { ApiError } from '../../api/http';
import { technologyStatuses, type ResearchQueueItem, type ResearchTree, type Technology } from '../../api/researchApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { formatDuration, reasonText } from './planetText';
import { useNow } from './usePlanet';
import { useResearch } from './useResearch';

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['research_', 'planets_'];

/**
 * The tree of the technologies with the queue of the researches on the start screen, from the
 * real data (task 00012). The queue is one for the player; the research starts from the body
 * the player chooses, and the science centre of that body opens the technologies.
 */
export function ResearchPanel({ planetId }: { planetId: number }) {
  const { t, language } = useI18n();
  const { research } = useApi();
  const { text } = useTexts();
  const [selectedPlanetId, setSelectedPlanetId] = useState(planetId);
  const { tree, receivedAt, failed, reload, apply } = useResearch(selectedPlanetId);
  const [notice, setNotice] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const now = useNow();

  const branches = useMemo(() => groupByBranch(tree), [tree]);

  const run = async (action: () => Promise<ResearchTree>, success: string) => {
    setBusy(true);
    setNotice(null);

    try {
      apply(await action());
      setNotice({ kind: 'success', text: success });
    } catch (error) {
      const code = error instanceof ApiError ? error.code : 'internal_error';
      setNotice({
        kind: 'error',
        text: reasonPrefixes.some((prefix) => code.startsWith(prefix)) ? reasonText(language, code) : t('error.generic.text'),
      });
      await reload();
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="panel" aria-labelledby="research-title">
      <div className="panel__head">
        <h2 className="panel__title" id="research-title">
          {t('player.game.research')}
        </h2>
        {tree ? <span className="meta">{t('research.queueSlots', { used: tree.queueUsedSlots, total: tree.queueSlots })}</span> : null}
      </div>
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      {!tree ? (
        <p className="lead" role="status">
          {failed ? t('research.loadFailed') : t('common.loading')}
        </p>
      ) : (
        <>
          <label className="field">
            <span className="field__label">{t('research.planet')}</span>
            <select
              className="input"
              value={tree.selectedPlanetId ?? ''}
              disabled={busy}
              onChange={(event) => setSelectedPlanetId(Number(event.target.value))}
            >
              {tree.planets.map((planet) => (
                <option key={planet.planetId} value={planet.planetId}>
                  {planet.isAvailable
                    ? t('research.planetOption', { name: planet.name ?? planet.planetId, level: planet.researchCenterLevel })
                    : t('research.planetUnavailable', { name: planet.name ?? planet.planetId, reason: reasonText(language, planet.reasonCode ?? '') })}
                </option>
              ))}
            </select>
          </label>
          <p className="lead">{t('research.centerLevel', { level: tree.availableCenterLevel })}</p>
          <ResearchQueue
            items={tree.queue}
            nameOf={(item) => text(item.technologyNameKey) ?? item.technologyNameKey}
            language={language}
            gameNow={tree.gameNow}
            multiplier={tree.timeMultiplier}
            receivedAt={receivedAt}
            now={now}
            busy={busy}
            onCancel={(item) => void run(() => research.cancel(item.id), t('research.cancelled'))}
          />
          {branches.map(({ branch, nameKey, technologies }) => (
            <div key={branch}>
              <h3 className="panel__title">{text(nameKey) ?? nameKey}</h3>
              {technologies.length === 0 ? (
                <span className="chip">{t('research.branchSoon')}</span>
              ) : (
                <ul className="queue-list">
                  {technologies.map((technology) => (
                    <TechnologyRow
                      key={technology.id}
                      technology={technology}
                      name={text(technology.nameKey) ?? technology.nameKey}
                      description={text(technology.descriptionKey) ?? technology.descriptionKey}
                      unlockName={(key) => text(key) ?? key}
                      busy={busy || tree.selectedPlanetId === null}
                      onResearch={() =>
                        void run(
                          () => research.enqueue(tree.selectedPlanetId ?? planetId, technology.id, technology.learnedLevel + 1),
                          t('research.queued', { name: text(technology.nameKey) ?? technology.nameKey, level: technology.learnedLevel + 1 }),
                        )
                      }
                    />
                  ))}
                </ul>
              )}
            </div>
          ))}
        </>
      )}
    </section>
  );
}

/**
 * The branches of the tree in the order of the server, each with its technologies; a branch
 * without technologies is kept, so the player sees it is coming.
 */
function groupByBranch(tree: ResearchTree | null) {
  if (!tree) {
    return [];
  }

  return tree.branches.map((branch) => ({
    ...branch,
    technologies: tree.technologies
      .filter((technology) => technology.branch === branch.branch)
      .sort((first, second) => first.sortOrder - second.sortOrder),
  }));
}

/**
 * The records of the queue of the researches.
 */
function ResearchQueue({
  items,
  nameOf,
  language,
  gameNow,
  multiplier,
  receivedAt,
  now,
  busy,
  onCancel,
}: {
  items: ResearchQueueItem[];
  nameOf: (item: ResearchQueueItem) => string;
  language: Language;
  gameNow: string;
  multiplier: number;
  receivedAt: number;
  now: number;
  busy: boolean;
  onCancel: (item: ResearchQueueItem) => void;
}) {
  const { t } = useI18n();

  if (items.length === 0) {
    return <p className="lead">{t('research.queueEmpty')}</p>;
  }

  return (
    <ol className="queue-list">
      {items.map((item) => (
        <li key={item.id} className="queue-item">
          <div className="queue-item__body">
            <strong>{t('research.queueItem', { name: nameOf(item), level: item.targetLevel })}</strong>
            <span className="meta">
              {item.finishesAt ? t('research.underWay') : t('research.waiting')}
              {item.finishesAt
                ? ` · ${formatDuration(language, Math.max(0, secondsLeft(item.finishesAt, gameNow, multiplier, receivedAt, now)))}`
                : ''}
            </span>
          </div>
          <button className="btn btn--secondary" type="button" disabled={busy} onClick={() => onCancel(item)}>
            {t('research.cancel')}
          </button>
        </li>
      ))}
    </ol>
  );
}

/**
 * The real seconds left until a moment of game time, from the answer of the server and the
 * moment it was received.
 */
function secondsLeft(finishesAt: string, gameNow: string, multiplier: number, receivedAt: number, now: number): number {
  const millisecondsPerSecond = 1000;
  const gameSeconds = (Date.parse(finishesAt) - Date.parse(gameNow)) / millisecondsPerSecond;
  const speed = multiplier > 0 ? multiplier : 1;

  return gameSeconds / speed - (now - receivedAt) / millisecondsPerSecond;
}

/**
 * One technology of the tree with its state, what its next level opens and the button that
 * starts its research.
 */
function TechnologyRow({
  technology,
  name,
  description,
  unlockName,
  busy,
  onResearch,
}: {
  technology: Technology;
  name: string;
  description: string;
  unlockName: (key: string) => string;
  busy: boolean;
  onResearch: () => void;
}) {
  const { t, language } = useI18n();

  const status = technology.status;
  const learned = technology.learnedLevel;
  const max = technology.maxLevel;
  const next = technology.levels.find((level) => level.level === learned + 1);
  const opens = (next?.effects ?? []).map((effect) => effect.unlockNameKey).filter((key): key is string => Boolean(key));

  return (
    <li className="queue-item">
      <div className="queue-item__body">
        <strong>{name}</strong>
        <span className="meta">{description}</span>
        <span className="meta">{t('research.level', { learned, max })}</span>
        {opens.length > 0 ? <span className="meta">{t('research.opens', { names: opens.map(unlockName).join(', ') })}</span> : null}
        {status === technologyStatuses.unavailable && technology.reasonCode === 'research_center_level_unmet' ? (
          <span className="meta">{t('research.centerNeeded', { level: technology.researchCenterLevel })}</span>
        ) : null}
        {status === technologyStatuses.unavailable && technology.reasonCode && technology.reasonCode !== 'research_center_level_unmet' ? (
          <span className="meta">{reasonText(language, technology.reasonCode)}</span>
        ) : null}
      </div>
      {status === technologyStatuses.available ? (
        <button className="btn btn--secondary" type="button" disabled={busy} onClick={onResearch}>
          {t('research.start')}
        </button>
      ) : (
        <span className="chip">{t(`research.status.${status}` as never)}</span>
      )}
    </li>
  );
}
