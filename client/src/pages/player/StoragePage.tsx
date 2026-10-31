import { useMemo, useState } from 'react';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { routes } from '../../router/context';
import { useI18n } from '../../i18n/useI18n';
import { PlaceholderBlock } from './PlaceholderBlock';
import { ScreenHead } from './ScreenHead';
import { ModulesBlock, ProductionBlock, ResourcesBlock, StorageAlarm } from './StorageBlocks';
import { Tabs, type TabItem } from './Tabs';
import { usePlanetScreen } from './usePlanetScreen';
import { useStock } from './useStock';

/**
 * The tabs of the storage in their order. The identifiers are of the screen, not of the game.
 */
const tabIds = ['all', 'resources', 'production', 'modules', 'hulls', 'artifacts'] as const;
type TabId = (typeof tabIds)[number];

/**
 * The storage screen (task 00020): the tabs All, Resources, Production, Modules, Hulls and
 * Artifacts. The resources, the produced resources and the parts are the real data of the
 * server; the hulls and the artifacts have no data yet, so they are visual placeholders with the
 * word "Soon" and no made-up number.
 *
 * The frame of the screen is the common one of every screen of the game.
 */
export function StoragePage() {
  const { t } = useI18n();
  useDocumentTitle(t('meta.storage.title'));

  const { civilization, planetId, planet, failed, reload, names } = usePlanetScreen();
  const [active, setActive] = useState<TabId>('all');
  // One read of the stock serves the produced resources and the parts.
  const stock = useStock(planetId);
  const producedIds = useMemo(() => new Set<number>(stock.stock?.producedResourceTypeIds ?? []), [stock.stock]);

  if (!civilization || planetId === null) {
    return null;
  }

  const tabs: TabItem[] = tabIds.map((id) => ({ id, label: t(`storage.tab.${id}` as never) }));
  const shows = (id: TabId) => active === 'all' || active === id;

  return (
    <div className="civ-main__in">
      <ScreenHead
        breadcrumbs={[{ label: t('civ.nav.planet'), to: routes.planet }, { label: planet?.name ?? civilization.planetName ?? t('civ.untitled') }]}
        subtitle={
          planet?.coordinates
            ? `${t('civ.sector')} ${planet.coordinates.sectorId} · ${t('civ.system')} ${planet.coordinates.systemId}${planet.coordinates.orbit === null ? '' : ` · ${t('civ.orbit')} ${planet.coordinates.orbit}`}`
            : undefined
        }
        title={t('storage.title', { name: planet?.name ?? civilization.planetName ?? '' })}
      />
      {planet && names ? <StorageAlarm names={names} planet={planet} /> : null}
      <Tabs active={active} label={t('storage.tabs')} onChange={(id) => setActive(id as TabId)} tabs={tabs} />
      {failed && !planet ? (
        <div className="panel">
          <Alert kind="error">{t('planet.loadFailed')}</Alert>
          <button className="btn btn--secondary" type="button" onClick={() => void reload()}>
            {t('planet.retry')}
          </button>
        </div>
      ) : null}
      <div className="storage-grid">
        {shows('resources') ? (
          !planet || !names ? (
            failed ? null : (
              <p className="lead" role="status">
                {t('common.loading')}
              </p>
            )
          ) : (
            <ResourcesBlock names={names} planet={planet} producedIds={producedIds} />
          )
        ) : null}
        {shows('production') && planet && names ? <ProductionBlock names={names} planet={planet} stock={stock} /> : null}
        {shows('modules') ? <ModulesBlock stock={stock} /> : null}
        {shows('hulls') ? <PlaceholderBlock reason={t('storage.hullsSoon')} title={t('storage.hullsHeading')} /> : null}
        {shows('artifacts') ? <PlaceholderBlock reason={t('storage.artifactsSoon')} title={t('storage.artifacts')} /> : null}
      </div>
    </div>
  );
}
