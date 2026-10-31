import { useMemo, useState } from 'react';
import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { ResearchPlanetSelect } from './ResearchPlanetSelect';
import { ResearchQueue } from './ResearchQueue';
import { groupByBranch } from './researchTree';
import { TechnologyCard } from './TechnologyCard';
import { useResearch } from './useResearch';
import { useResearchCommands } from './useResearchCommands';
import { useResourceGroups } from './useResourceGroups';
import { useResourceNames } from './useResourceNames';
import { useWorldReference } from './useWorldReference';

/**
 * The tree of the technologies with the queue of the researches on the start screen, from the
 * real data (task 00012). The queue is one for the player; the research starts from the body
 * the player chooses, and the science centre of that body opens the technologies.
 *
 * The queue is the standard queue of the game (task 00020, section 8.1).
 */
export function ResearchPanel({ planetId }: { planetId: number }) {
  const { t } = useI18n();
  const { text } = useTexts();
  const [selectedPlanetId, setSelectedPlanetId] = useState(planetId);
  const state = useResearch(selectedPlanetId);
  const { tree, receivedAt, failed, reload } = state;
  const { busy, notice, enqueue, cancel } = useResearchCommands(state, planetId);
  const reference = useWorldReference();
  const resourceName = useResourceNames(reference, text);
  const resourceGroup = useResourceGroups(reference);

  const branches = useMemo(() => groupByBranch(tree), [tree]);

  return (
    <section className="panel" aria-labelledby="research-title">
      <div className="panel__head">
        <h2 className="panel__title" id="research-title">
          {t('player.game.research')}
        </h2>
      </div>
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      {!tree ? (
        <p className="lead" role="status">
          {failed ? t('research.loadFailed') : t('common.loading')}
        </p>
      ) : (
        <>
          <ResearchPlanetSelect disabled={busy} onChange={setSelectedPlanetId} tree={tree} />
          <p className="lead">{t('research.centerLevel', { level: tree.availableCenterLevel })}</p>
          <ResearchQueue
            busy={busy}
            onCancel={cancel}
            onFinished={() => void reload()}
            receivedAt={receivedAt}
            resourceName={resourceName}
            technologyName={(key) => text(key) ?? key}
            tree={tree}
          />
          {branches.map(({ branch, nameKey, technologies }) => (
            <div key={branch}>
              <h3 className="panel__title">{text(nameKey) ?? nameKey}</h3>
              {technologies.length === 0 ? (
                <span className="chip">{t('research.branchSoon')}</span>
              ) : (
                <ul className="building-grid">
                  {technologies.map((technology) => (
                    <TechnologyCard
                      key={technology.id}
                      branchName={text(nameKey) ?? nameKey}
                      deadline={null}
                      resourceGroupCode={resourceGroup}
                      technology={technology}
                      name={text(technology.nameKey) ?? technology.nameKey}
                      description={text(technology.descriptionKey) ?? technology.descriptionKey}
                      resourceName={resourceName}
                      timeMultiplier={tree.timeMultiplier}
                      unlockName={(key) => text(key) ?? key}
                      busy={busy || tree.selectedPlanetId === null}
                      onResearch={() => enqueue(technology, text(technology.nameKey) ?? technology.nameKey)}
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
