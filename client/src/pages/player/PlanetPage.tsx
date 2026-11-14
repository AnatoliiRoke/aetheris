import { useState, useMemo, type FormEvent } from 'react';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { GameIcon } from '../../components/GameIcon';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { siteConfig } from '../../config/site';
import type { MessageKey } from '../../i18n/uk';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { fieldRuleErrors, planetNameProblems } from '../auth/authErrors';
import { BuildingQueue } from './BuildingQueue';
import { FinancePanel } from './FinancePanel';
import { EnergyPanel, ResourcesPanel, StoragePanel } from './PlanetPanels';
import { PlanetCard } from './PlanetCard';
import { PopulationPanel } from './PopulationPanel';
import { ProcessesPanel } from './ProcessesPanel';
import { ProcessQueue } from './ProcessQueue';
import { ProductionPanel } from './ProductionPanel';
import { ProductionQueue } from './ProductionQueue';
import { ResearchQueue } from './ResearchQueue';
import { galaxyPlace } from './planetText';
import { ScreenHead } from './ScreenHead';
import { useProcessOrders, useProductionOrders } from './useActiveOrders';
import { useResearch } from './useResearch';
import { useResearchCommands } from './useResearchCommands';
import { useResourceGroups } from './useResourceGroups';
import { useResourceCodes, useResourceNames } from './useResourceNames';
import { usePlanetScreen } from './usePlanetScreen';
import { useShellActions } from './useShell';
import { useWorldReference } from './useWorldReference';

/**
 * The planet of the player (tasks 00011 and 00020): the card of the body, the resources and the
 * storage, the energy, the population and the two queues, the construction and the research.
 * The page only shows the rules; the server checks them on every command.
 *
 * The frame of the screen is the common one of every screen of the game; the queues are the
 * standard queue panels, the same as on the screens of the construction and the research. Under
 * the research queue stand the production queue and the queue of the processes: every batch of
 * every factory and every order of every building of the body (task 00020, section 7.5).
 */
export function PlanetPage() {
  const { t } = useI18n();
  const { text } = useTexts();
  useDocumentTitle(t('meta.planet.title'));

  const { civilization, planetId, planet, receivedAt, failed, reload, names, busy, notice, onCancel } = usePlanetScreen();
  const reference = useWorldReference();
  const researchState = useResearch(planetId);
  const research = useResearchCommands(researchState, planetId ?? 0);
  // The panels of the factory and of the processes read again after a command of the queues
  // above them, and the queues read again after a command of the panels.
  const [productionRevision, setProductionRevision] = useState(0);
  const [processesRevision, setProcessesRevision] = useState(0);
  const productionOrders = useProductionOrders(planetId, () => setProductionRevision((revision) => revision + 1));
  const processOrders = useProcessOrders(planetId, () => setProcessesRevision((revision) => revision + 1));
  const resourceName = useResourceNames(reference, text);
  const resourceCode = useResourceCodes(reference);
  const resourceGroup = useResourceGroups(reference);
  const objectType = useMemo(() => reference?.objectTypes.find((type) => type.id === planet?.typeId) ?? null, [reference, planet?.typeId]);

  if (!civilization || planetId === null) {
    return null;
  }

  const typeName = objectType ? text(objectType.nameKey) ?? objectType.nameKey : t('civ.unknownType');
  const chooseLabel = (route: string, label: string) => (
    <Link className="btn btn--secondary" to={route}>
      {label}
    </Link>
  );

  return (
    <div className="civ-main__in">
      <ScreenHead
        subtitle={planet?.coordinates ? galaxyPlace(t, planet.coordinates) : undefined}
        title={planet?.name ?? civilization.planetName ?? ''}
        titleControl={
          <PlanetHeading
            badge={planet ? typeName : undefined}
            isHome={civilization.planetId === planetId}
            name={planet?.name ?? civilization.planetName ?? ''}
            planetId={planetId}
            titlePrefix={t('planet.title', { name: '' }).trim()}
          />
        }
        titlePrefix={t('planet.title', { name: '' }).trim()}
      />
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      {research.notice ? <Alert kind={research.notice.kind}>{research.notice.text}</Alert> : null}
      {productionOrders.notice ? <Alert kind={productionOrders.notice.kind}>{productionOrders.notice.text}</Alert> : null}
      {processOrders.notice ? <Alert kind={processOrders.notice.kind}>{processOrders.notice.text}</Alert> : null}
      {failed && !planet ? (
        <div className="panel">
          <Alert kind="error">{t('planet.loadFailed')}</Alert>
          <button className="btn btn--secondary" type="button" onClick={() => void reload()}>
            {t('planet.retry')}
          </button>
        </div>
      ) : null}
      {!planet || !names ? (
        failed ? null : (
          <p className="lead" role="status">
            {t('common.loading')}
          </p>
        )
      ) : (
        <>
          <div className="planet-layout">
            <div className="planet-layout__main">
              <PlanetCard planet={planet} resourceGroup={resourceGroup} resourceName={resourceName} typeImageKey={objectType?.imageKey ?? null} typeName={typeName} />
              <ResourcesPanel names={names} planet={planet} />
              <StoragePanel names={names} planet={planet} />
              <div className="planet-grid">
                <EnergyPanel planet={planet} />
                <FinancePanel />
                <PopulationPanel planetId={planetId} />
              </div>
              <ProductionPanel
                onChanged={() => void productionOrders.reload()}
                planetId={planetId}
                revision={productionRevision}
              />
              <ProcessesPanel
                onChanged={() => void processOrders.reload()}
                planetId={planetId}
                revision={processesRevision}
              />
            </div>
            <aside aria-label={t('planet.queues')} className="planet-layout__aside">
              <BuildingQueue
                action={chooseLabel(routes.planetBuild, t('planet.chooseBuilding'))}
                busy={busy}
                names={names}
                onCancel={onCancel}
                onFinished={() => void reload()}
                planet={planet}
                receivedAt={receivedAt}
              />
              {researchState.tree ? (
                <ResearchQueue
                  action={chooseLabel(routes.planetResearch, t('planet.chooseResearch'))}
                  busy={research.busy}
                  fillClass="f-research"
                  onCancel={research.cancel}
                  onFinished={() => void researchState.reload()}
                  receivedAt={researchState.receivedAt}
                  resourceCode={resourceCode}
                  resourceName={resourceName}
                  technologyName={(key) => text(key) ?? key}
                  tree={researchState.tree}
                />
              ) : (
                <section aria-label={t('research.queueTitle')} className="panel">
                  <h2 className="panel__title">{t('research.queueTitle')}</h2>
                  <p className="lead" role="status">
                    {researchState.failed ? t('research.loadFailed') : t('common.loading')}
                  </p>
                </section>
              )}
              {productionOrders.orders ? (
                <ProductionQueue
                  busy={productionOrders.busy}
                  onCancel={(order) => void productionOrders.cancel(order)}
                  onFinished={() => void productionOrders.reload()}
                  orders={productionOrders.orders}
                  receivedAt={productionOrders.receivedAt}
                  timeMultiplier={planet.timeMultiplier}
                  tools={chooseLabel(routes.planetProduction, t('planet.chooseProduction'))}
                />
              ) : (
                <QueueLoading
                  failed={productionOrders.failed}
                  failedText={t('production.loadFailed')}
                  loadingText={t('production.loading')}
                  onRetry={() => void productionOrders.reload()}
                  title={t('production.queue')}
                />
              )}
              {processOrders.orders ? (
                <ProcessQueue
                  busy={processOrders.busy}
                  onCancel={(order) => void processOrders.cancel(order)}
                  onFinished={() => void processOrders.reload()}
                  orders={processOrders.orders}
                  receivedAt={processOrders.receivedAt}
                  timeMultiplier={planet.timeMultiplier}
                />
              ) : (
                <QueueLoading
                  failed={processOrders.failed}
                  failedText={t('processes.loadFailed')}
                  loadingText={t('processes.loading')}
                  onRetry={() => void processOrders.reload()}
                  title={t('processes.queue')}
                />
              )}
            </aside>
          </div>
        </>
      )}
    </div>
  );
}

/**
 * The place of a queue while its orders load, or when they could not be read: the heading of the
 * queue with a short word and, after a failure, the button that tries again.
 */
function QueueLoading({
  title,
  loadingText,
  failedText,
  failed,
  onRetry,
}: {
  title: string;
  loadingText: string;
  failedText: string;
  failed: boolean;
  onRetry: () => void;
}) {
  const { t } = useI18n();

  return (
    <section aria-label={title} className="panel">
      <h2 className="panel__title">{title}</h2>
      {failed ? (
        <>
          <Alert kind="error">{failedText}</Alert>
          <button className="btn btn--secondary" type="button" onClick={onRetry}>
            {t('planet.retry')}
          </button>
        </>
      ) : (
        <p className="lead" role="status">
          {loadingText}
        </p>
      )}
    </section>
  );
}

/**
 * The name of the body and the pencil that makes it editable. The server checks the name again.
 */
function PlanetHeading({
  planetId,
  name,
  isHome,
  badge,
  titlePrefix,
}: {
  planetId: number;
  name: string;
  isHome: boolean;
  badge?: string;
  titlePrefix: string;
}) {
  const { t } = useI18n();
  const { game } = useApi();
  const { refresh } = useSession();
  const { setPlanetName } = useShellActions();
  const [renaming, setRenaming] = useState(false);
  const [draft, setDraft] = useState(name);
  const [busy, setBusy] = useState(false);
  const [nameErrors, setNameErrors] = useState<MessageKey[]>([]);
  const limits = { min: siteConfig.planetNameMinLength, max: siteConfig.planetNameMaxLength };

  const close = () => {
    setRenaming(false);
    setNameErrors([]);
    setDraft(name);
  };

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();

    const problems = planetNameProblems(draft);

    setNameErrors(problems);

    if (problems.length > 0) {
      return;
    }

    const nextName = draft.trim();

    if (nextName === name) {
      close();
      return;
    }

    setBusy(true);

    try {
      const saved = await game.renamePlanet(planetId, nextName);

      setPlanetName(saved.planetId, saved.name);

      if (isHome) {
        await refresh();
      }

      setRenaming(false);
      setNameErrors([]);
    } catch (error) {
      const fields = fieldRuleErrors(error, 'planet_name');

      setNameErrors(fields.length > 0 ? fields : ['error.generic.text']);
    } finally {
      setBusy(false);
    }
  };

  if (!renaming) {
    return (
      <>
        <h1 aria-label={titlePrefix ? `${titlePrefix} ${name}` : name} className="player__title">
          {name}
        </h1>
        <button
          aria-label={t('planet.rename')}
          className="icon-btn"
          onClick={() => {
            setDraft(name);
            setNameErrors([]);
            setRenaming(true);
          }}
          title={t('planet.rename')}
          type="button"
        >
          <GameIcon name="pencil" />
        </button>
        {badge ? <span className="screen-head__badge">{badge}</span> : null}
      </>
    );
  }

  return (
    <>
      <form className="screen-head__rename" onSubmit={(event) => void onSubmit(event)}>
        <label className="visually-hidden" htmlFor="planet-rename">
          {t('planet.rename')}
        </label>
        <input
          aria-invalid={nameErrors.length > 0}
          autoFocus
          className="input"
          id="planet-rename"
          maxLength={siteConfig.planetNameMaxLength}
          onChange={(event) => {
            setDraft(event.target.value);
            setNameErrors([]);
          }}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              event.preventDefault();
              close();
            }
          }}
          value={draft}
        />
        <button className="btn btn--primary" disabled={busy} type="submit">
          {t('planet.renameSave')}
        </button>
        <button className="btn btn--secondary" disabled={busy} onClick={close} type="button">
          {t('planet.renameCancel')}
        </button>
        {nameErrors.length > 0 ? <p className="error-text">{nameErrors.map((key) => t(key, limits)).join(' ')}</p> : null}
      </form>
      {badge ? <span className="screen-head__badge">{badge}</span> : null}
    </>
  );
}
