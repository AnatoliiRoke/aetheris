import { useState } from 'react';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { CivShell } from './CivShell';
import { PlaceholderBlock } from './PlaceholderBlock';
import { ScreenHead } from './ScreenHead';
import { ModulesBlock, ResourcesBlock } from './StorageBlocks';
import { Tabs, type TabItem } from './Tabs';
import { usePlanetScreen } from './usePlanetScreen';

/**
 * The tabs of the storage in their order. The identifiers are of the screen, not of the game.
 */
const tabIds = ['all', 'resources', 'production', 'modules', 'hulls', 'artifacts'] as const;
type TabId = (typeof tabIds)[number];

/**
 * The storage screen (task 00020): the tabs All, Resources, Production, Modules, Hulls and
 * Artifacts. The resources and the parts are the real data of the server; the produced
 * resources, the hulls and the artifacts have no data yet, so they are visual placeholders with
 * the word "Soon" and no made-up number.
 *
 * The frame of the screen is the common one of every screen of the game.
 */
export function StoragePage() {
  const { t } = useI18n();
  useDocumentTitle(t('meta.storage.title'));

  const { civilization, planet, failed, reload, names } = usePlanetScreen();
  const [active, setActive] = useState<TabId>('all');

  if (!civilization) {
    return null;
  }

  const tabs: TabItem[] = tabIds.map((id) => ({ id, label: t(`storage.tab.${id}` as never) }));
  const shows = (id: TabId) => active === 'all' || active === id;

  return (
    <CivShell active="storage">
      <div className="civ-main__in">
        <ScreenHead title={t('storage.title', { name: planet?.name ?? civilization.planetName ?? '' })} />
        <Tabs active={active} label={t('storage.tabs')} onChange={(id) => setActive(id as TabId)} tabs={tabs} />
        {failed && !planet ? (
          <div className="panel">
            <Alert kind="error">{t('planet.loadFailed')}</Alert>
            <button className="btn btn--secondary" type="button" onClick={() => void reload()}>
              {t('planet.retry')}
            </button>
          </div>
        ) : null}
        {shows('resources') ? (
          !planet || !names ? (
            failed ? null : (
              <p className="lead" role="status">
                {t('common.loading')}
              </p>
            )
          ) : (
            <ResourcesBlock names={names} planet={planet} />
          )
        ) : null}
        {shows('production') ? <PlaceholderBlock reason={t('storage.productionSoon')} title={t('storage.production')} /> : null}
        {shows('modules') ? <ModulesBlock planetId={civilization.planetId} /> : null}
        {shows('hulls') ? <PlaceholderBlock reason={t('storage.hullsSoon')} title={t('storage.hulls')} /> : null}
        {shows('artifacts') ? <PlaceholderBlock reason={t('storage.artifactsSoon')} title={t('storage.artifacts')} /> : null}
      </div>
    </CivShell>
  );
}
