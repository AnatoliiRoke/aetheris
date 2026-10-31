import { useMemo, useState } from 'react';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { BuildingQueue } from './BuildingQueue';
import { EnergyPanel, ResourcesPanel, StoragePanel } from './PlanetPanels';
import { PlanetCard } from './PlanetCard';
import { PopulationPanel } from './PopulationPanel';
import { ProcessesPanel } from './ProcessesPanel';
import { ProcessQueue } from './ProcessQueue';
import { ProductionPanel } from './ProductionPanel';
import { ProductionQueue } from './ProductionQueue';
import { ResearchQueue } from './ResearchQueue';
import { ScreenHead } from './ScreenHead';
import { useProcessOrders, useProductionOrders } from './useActiveOrders';
import { useResearch } from './useResearch';
import { useResearchCommands } from './useResearchCommands';
import { useResourceGroups } from './useResourceGroups';
import { useResourceNames } from './useResourceNames';
import { usePlanetScreen } from './usePlanetScreen';
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
        subtitle={planet?.coordinates ? `${typeName} · ${t('civ.sector')} ${planet.coordinates.sectorId} · ${t('civ.system')} ${planet.coordinates.systemId}` : undefined}
        title={t('planet.title', { name: planet?.name ?? civilization.planetName ?? '' })}
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
                  onCancel={research.cancel}
                  onFinished={() => void researchState.reload()}
                  receivedAt={researchState.receivedAt}
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
