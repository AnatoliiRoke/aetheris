import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { Technology } from '../../api/researchApi';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { ResearchPlanetSelect } from './ResearchPlanetSelect';
import { ResearchQueue } from './ResearchQueue';
import { ScreenHead } from './ScreenHead';
import { TechnologyCard } from './TechnologyCard';
import { Tabs } from './Tabs';
import { allStatuses, filterTechnologies, groupByBranch, statusFilters } from './researchTree';
import { deadlineFor } from './planetText';
import { useResearch } from './useResearch';
import { useCurrentPlanetId } from './useShell';
import { useResearchCommands } from './useResearchCommands';
import { useResourceGroups } from './useResourceGroups';
import { useResourceNames } from './useResourceNames';
import { useWorldReference } from './useWorldReference';

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
  const currentPlanetId = useCurrentPlanetId();
  const planetId = selectedPlanetId ?? currentPlanetId;
  const state = useResearch(planetId);
  const { tree, receivedAt, failed, reload } = state;
  const { busy, notice, enqueue, cancel } = useResearchCommands(state, currentPlanetId ?? 0);
  const reference = useWorldReference();
  const resourceName = useResourceNames(reference, text);
  const resourceGroup = useResourceGroups(reference);
  const [branchId, setBranchId] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<string>(allStatuses);

  // The cards are drawn again only when their own data changes: the callbacks given to them keep
  // the same identity between the draws.
  const enqueueRef = useRef(enqueue);
  useEffect(() => {
    enqueueRef.current = enqueue;
  });
  const startResearch = useCallback((technology: Technology) => enqueueRef.current(technology, text(technology.nameKey) ?? technology.nameKey), [text]);
  const unlockName = useCallback((key: string) => text(key) ?? key, [text]);

  const branches = useMemo(() => groupByBranch(tree), [tree]);
  // The end of the research under way, by technology, on the clock of the browser.
  const deadlines = useMemo(
    () => new Map((tree?.queue ?? []).map((item) => [item.technologyId, deadlineFor(tree?.gameNow ?? '', tree?.timeMultiplier ?? 1, item.finishesAt, receivedAt)])),
    [tree, receivedAt],
  );
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
    <div className="civ-main__in">
      <ScreenHead
        actions={
          <Link className="btn btn--secondary" to={routes.planet}>
            {t('screen.backToPlanet')}
          </Link>
        }
        breadcrumbs={[{ label: t('civ.nav.research') }]}
        subtitle={tree ? `${t('research.centerLevelLine', { level: tree.availableCenterLevel })} · ${t('research.centerHint')}` : undefined}
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
          {tree.availableCenterLevel <= 0 ? <Alert kind="info">{t('research.centerMissing')}</Alert> : null}
          <ResearchPlanetSelect disabled={busy} onChange={setSelectedPlanetId} tree={tree} />
          <ResearchQueue
            busy={busy}
            footnote={t('research.queueNote')}
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
                <div aria-label={t('research.filter')} className="filter-chips" role="group">
                  {[{ id: allStatuses }, ...statusFilters].map((entry) => (
                    <button aria-pressed={status === entry.id} className="chip" key={entry.id} type="button" onClick={() => setStatus(entry.id)}>
                      {t(`research.filter.${entry.id}` as never)}
                    </button>
                  ))}
                </div>
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
                      branchName={text(activeBranch.nameKey) ?? activeBranch.nameKey}
                      deadline={deadlines.get(technology.id) ?? null}
                      name={text(technology.nameKey) ?? technology.nameKey}
                      onResearch={startResearch}
                      resourceGroupCode={resourceGroup}
                      resourceName={resourceName}
                      technology={technology}
                      timeMultiplier={tree.timeMultiplier}
                      unlockName={unlockName}
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
  );
}
