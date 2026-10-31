import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { BuildingCard } from './BuildingCard';
import { BuildingQueue } from './BuildingQueue';
import { CivShell } from './CivShell';
import { ScreenHead } from './ScreenHead';
import { groupBuildings } from './buildingCategories';
import { lookup } from './planetText';
import { usePlanetScreen } from './usePlanetScreen';

/**
 * The construction screen (task 00020): the queue of the buildings on the whole width and the
 * tiles of the buildings grouped by the category the data gives them, each with the cost of the next level, the
 * time and the reasons it cannot be queued yet. The server checks every rule again.
 *
 * The frame of the screen and the queue are the common ones of every screen of the game.
 */
export function BuildPage() {
  const { t, language } = useI18n();
  useDocumentTitle(t('meta.build.title'));

  const { civilization, planet, receivedAt, failed, reload, names, busy, notice, onEnqueue, onCancel } = usePlanetScreen();

  if (!civilization) {
    return null;
  }

  return (
    <CivShell active="planet">
      <div className="civ-main__in">
        <ScreenHead
          actions={
            <Link className="btn btn--secondary" to={routes.planet}>
              {t('screen.backToPlanet')}
            </Link>
          }
          subtitle={planet?.coordinates ? `${t('civ.sector')} ${planet.coordinates.sectorId} · ${t('civ.system')} ${planet.coordinates.systemId}` : undefined}
          title={t('build.title', { name: planet?.name ?? civilization.planetName ?? '' })}
        />
        {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
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
            <BuildingQueue
              busy={busy}
              emptyText={t('planet.queueEmpty')}
              footnote={t('queue.cancelNote')}
              names={names}
              onCancel={onCancel}
              onFinished={() => void reload()}
              planet={planet}
              receivedAt={receivedAt}
            />
            {planet.buildings.length === 0 ? (
              <div className="civ-empty">
                <p>{t('build.empty')}</p>
              </div>
            ) : (
              groupBuildings(planet).map(({ key, category, buildings }) => (
                <section aria-labelledby={`build-${key}`} className="panel" key={key}>
                  <h2 className="panel__title" id={`build-${key}`}>
                    {category ? lookup(language, category.nameKey, category.code, undefined, names.entities) : t('planet.category.other')}
                  </h2>
                  <ul className="building-grid">
                    {buildings.map((building) => (
                      <BuildingCard building={building} busy={busy} key={building.buildingTypeId} names={names} onEnqueue={onEnqueue} planet={planet} />
                    ))}
                  </ul>
                </section>
              ))
            )}
          </>
        )}
      </div>
    </CivShell>
  );
}
