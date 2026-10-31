import { useMemo, useState } from 'react';
import { technologyStatuses } from '../../api/researchApi';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { CivShell } from './CivShell';
import { ResearchPlanetSelect } from './ResearchPlanetSelect';
import { ResearchQueue } from './ResearchQueue';
import { ScreenHead } from './ScreenHead';
import { TechnologyCard } from './TechnologyCard';
import { Tabs } from './Tabs';
import { allStatuses, filterTechnologies, groupByBranch } from './researchTree';
import { useResearch } from './useResearch';
import { useResearchCommands } from './useResearchCommands';
import { useResourceNames } from './useResourceNames';
import { useWorldReference } from './useWorldReference';

/**
 * The states a technology can be filtered by: every state a technology shows except the unknown
 * one, which the server never sends to a player.
 */
const filterStatuses = [
  technologyStatuses.available,
  technologyStatuses.queued,
  technologyStatuses.researching,
  technologyStatuses.learned,
  technologyStatuses.unavailable,
];

/**
 * The research screen (task 00020): the research queue, the tabs of the branches, the search and
 * the filter by state, and the tiles of the technologies with the effect, the cost, the time and
 * the reasons a technology is closed. The server checks every rule again.
 *
 * The frame of the screen and the queue are the common ones of every screen of the game.
 */
export function ResearchPage() {
  const { t } = useI18n();
  const { text } = useTexts();
  const { session } = useSession();
  useDocumentTitle(t('meta.research.title'));

  const civilization = session.status === 'signed-in' ? session.me.civilization : null;
  const [selectedPlanetId, setSelectedPlanetId] = useState<number | null>(null);
  const planetId = selectedPlanetId ?? civilization?.planetId ?? null;
  const state = useResearch(planetId);
  const { tree, receivedAt, failed, reload } = state;
  const { busy, notice, enqueue, cancel } = useResearchCommands(state, civilization?.planetId ?? 0);
  const resourceName = useResourceNames(useWorldReference(), text);
  const [branchId, setBranchId] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<string>(allStatuses);

  const branches = useMemo(() => groupByBranch(tree), [tree]);
  const activeBranch = branches.find((branch) => branch.branch === branchId) ?? branches[0] ?? null;
  const visible = useMemo(
    () =>
      activeBranch
        ? filterTechnologies(activeBranch.technologies, query, status, (technology) => `${text(technology.nameKey) ?? technology.nameKey} ${text(technology.descriptionKey) ?? ''}`)
        : [],
    [activeBranch, query, status, text],
  );

  if (!civilization) {
    return null;
  }

  return (
    <CivShell active="research">
      <div className="civ-main__in">
        <ScreenHead
          actions={
            <Link className="btn btn--secondary" to={routes.planet}>
              {t('screen.backToPlanet')}
            </Link>
          }
          subtitle={tree ? t('research.centerLevel', { level: tree.availableCenterLevel }) : undefined}
          title={t('research.title')}
        />
        {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
        {!tree ? (
          <div className="panel">
            {failed ? <Alert kind="error">{t('research.loadFailed')}</Alert> : null}
            {failed ? (
              <button className="btn btn--secondary" type="button" onClick={() => void reload()}>
                {t('planet.retry')}
              </button>
            ) : (
              <p className="lead" role="status">
                {t('common.loading')}
              </p>
            )}
          </div>
        ) : (
          <>
            <ResearchPlanetSelect disabled={busy} onChange={setSelectedPlanetId} tree={tree} />
            <ResearchQueue
              busy={busy}
              footnote={t('queue.cancelNote')}
              onCancel={cancel}
              onFinished={() => void reload()}
              receivedAt={receivedAt}
              resourceName={resourceName}
              technologyName={(key) => text(key) ?? key}
              tree={tree}
            />
            {activeBranch ? (
              <section aria-label={t('research.technologies')} className="panel">
                <Tabs
                  active={activeBranch.branch}
                  label={t('research.branches')}
                  onChange={setBranchId}
                  tabs={branches.map((branch) => ({ id: branch.branch, label: text(branch.nameKey) ?? branch.nameKey }))}
                />
                <div className="tech-tools">
                  <label className="field">
                    <span className="field__label">{t('research.search')}</span>
                    <input className="input" type="search" value={query} onChange={(event) => setQuery(event.target.value)} />
                  </label>
                  <label className="field">
                    <span className="field__label">{t('research.filter')}</span>
                    <select className="input" value={status} onChange={(event) => setStatus(event.target.value)}>
                      <option value={allStatuses}>{t('research.filterAll')}</option>
                      {filterStatuses.map((value) => (
                        <option key={value} value={value}>
                          {t(`research.status.${value}` as never)}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
                {activeBranch.technologies.length === 0 ? (
                  <div className="civ-empty">
                    <p>{t('research.branchEmpty')}</p>
                  </div>
                ) : visible.length === 0 ? (
                  <div className="civ-empty">
                    <p>{t('research.noMatches')}</p>
                  </div>
                ) : (
                  <ul className="building-grid">
                    {visible.map((technology) => (
                      <TechnologyCard
                        busy={busy || tree.selectedPlanetId === null}
                        description={text(technology.descriptionKey) ?? technology.descriptionKey}
                        key={technology.id}
                        name={text(technology.nameKey) ?? technology.nameKey}
                        onResearch={() => enqueue(technology, text(technology.nameKey) ?? technology.nameKey)}
                        resourceName={resourceName}
                        technology={technology}
                        timeMultiplier={tree.timeMultiplier}
                        unlockName={(key) => text(key) ?? key}
                      />
                    ))}
                  </ul>
                )}
              </section>
            ) : (
              <div className="civ-empty">
                <p>{t('research.noBranches')}</p>
              </div>
            )}
          </>
        )}
      </div>
    </CivShell>
  );
}
