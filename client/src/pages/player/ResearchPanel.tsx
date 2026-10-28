import { useMemo } from 'react';
import { technologyStatuses, type ResearchQueueItem, type Technology } from '../../api/researchApi';
import { useApi } from '../../api/useApi';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { formatDuration } from './planetText';
import { useNow } from './usePlanet';
import { useResearch } from './useResearch';

/**
 * The tree of the technologies with the queue of the researches on the start screen, from the
 * real data (task 00012).
 */
export function ResearchPanel({ planetId }: { planetId: number }) {
  const { t, language } = useI18n();
  const { research } = useApi();
  const { text } = useTexts();
  const { tree, receivedAt, failed } = useResearch(planetId);
  const now = useNow();

  const technologies = useMemo(
    () => (tree ? tree.technologies.filter((technology) => technology.branch === 'economy') : []),
    [tree],
  );

  return (
    <section className="panel" aria-labelledby="research-title">
      <div className="panel__head">
        <h2 className="panel__title" id="research-title">
          {t('player.game.research')}
        </h2>
        {tree ? <span className="meta">{t('research.queueSlots', { used: tree.queueUsedSlots, total: tree.queueSlots })}</span> : null}
      </div>
      {!tree ? (
        <p className="lead" role="status">
          {failed ? t('research.loadFailed') : t('common.loading')}
        </p>
      ) : (
        <>
          <p className="lead">
            {t('research.centerLevel', { level: tree.availableCenterLevel })}
          </p>
          <ResearchQueue
            items={tree.queue}
            nameOf={(item) => text(item.technologyNameKey) ?? item.technologyNameKey}
            language={language}
            gameNow={tree.gameNow}
            multiplier={tree.timeMultiplier}
            receivedAt={receivedAt}
            now={now}
            onCancel={async (item) => {
              await research.cancel(item.id);
            }}
          />
          <ul className="queue-list">
            {technologies.map((technology) => (
              <TechnologyRow
                key={technology.id}
                technology={technology}
                name={text(technology.nameKey) ?? technology.nameKey}
                description={text(technology.descriptionKey) ?? technology.descriptionKey}
                onResearch={async () => {
                  await research.enqueue(planetId, technology.id, Math.min(technology.learnedLevel + 1, technology.maxLevel));
                }}
              />
            ))}
          </ul>
        </>
      )}
    </section>
  );
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
  onCancel,
}: {
  items: ResearchQueueItem[];
  nameOf: (item: ResearchQueueItem) => string;
  language: Language;
  gameNow: string;
  multiplier: number;
  receivedAt: number;
  now: number;
  onCancel: (item: ResearchQueueItem) => Promise<void>;
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
              {item.finishesAt ? ` · ${formatDuration(language, secondsLeft(item.finishesAt, gameNow, multiplier, receivedAt, now))}` : ''}
            </span>
          </div>
          <button className="btn btn--secondary" type="button" onClick={() => void onCancel(item)}>
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
 * One technology of the tree with its state and the button that starts its research.
 */
function TechnologyRow({
  technology,
  name,
  description,
  onResearch,
}: {
  technology: Technology;
  name: string;
  description: string;
  onResearch: () => Promise<void>;
}) {
  const { t } = useI18n();

  const status = technology.status;
  const learned = technology.learnedLevel;
  const max = technology.maxLevel;

  return (
    <li className="queue-item">
      <div className="queue-item__body">
        <strong>{name}</strong>
        <span className="meta">{description}</span>
        <span className="meta">{t('research.level', { learned, max })}</span>
        {status === technologyStatuses.unavailable ? (
          <span className="meta">
            {t('research.centerNeeded', { level: technology.researchCenterLevel })}
          </span>
        ) : null}
      </div>
      {status === technologyStatuses.available ? (
        <button className="btn btn--secondary" type="button" onClick={() => void onResearch()}>
          {t('research.start')}
        </button>
      ) : (
        <span className="chip">{t(`research.status.${status}` as never)}</span>
      )}
    </li>
  );
}
