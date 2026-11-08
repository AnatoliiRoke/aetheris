import type { FleetShip } from '../../../api/fleetApi';
import { useI18n } from '../../../i18n/useI18n';
import { PlanetAvatar } from './FleetBits';
import { groupByPlanet } from './fleetsParts';
import { ProjectList } from './ProjectLists';
import { useFleetsScene } from './useFleetsScene';

/**
 * The ships that are in no fleet, grouped by the body they stand on and, on every body, by the class of
 * the hull and the project with how many ships each has (task 00040, mock-up). The ships are not listed
 * one by one. Every body has its own button: it opens the window that asks which fleet of that body gets
 * how many ships of which project.
 *
 * @param ships The free ships.
 * @param busy Whether a command is under way.
 * @param planetsWithFleet The bodies that have a fleet, so the button of a body without one says why it waits.
 * @param onAdd Opens the window that adds ships of a body to a fleet of it.
 */
export function FreeShipsPanel({
  ships,
  busy,
  planetsWithFleet,
  onAdd,
}: {
  ships: readonly FleetShip[];
  busy: boolean;
  planetsWithFleet: ReadonlySet<number>;
  onAdd: (planetId: number) => void;
}) {
  const { t } = useI18n();
  const scene = useFleetsScene();

  return (
    <section aria-labelledby="fleets-free" className="fl-free">
      <h2 className="fl-free__title" id="fleets-free">
        {t('fleets.freeTitle')}
      </h2>
      <p className="fl-note">{t('fleets.freeNote')}</p>
      {ships.length === 0 ? (
        <p className="fl-note">{t('fleets.noFree')}</p>
      ) : (
        groupByPlanet(ships).map((group) => {
          const hasFleet = planetsWithFleet.has(group.planetId);

          return (
            <div className="fl-free__group" key={group.planetId}>
              <div className="fl-free__head">
                <span className="fl-free__who">
                  <PlanetAvatar planetId={group.planetId} size="md" />
                  <b>{scene.nameOf(group.planetId)}</b>
                  <span className="fl-note">{t('fleets.freeCount', { count: group.ships.length })}</span>
                </span>
                <button
                  className="btn btn--primary"
                  disabled={busy || !hasFleet}
                  onClick={() => onAdd(group.planetId)}
                  title={hasFleet ? undefined : t('fleets.noFleetOnPlanet')}
                  type="button"
                >
                  {t('fleets.addToFleet')}
                </button>
              </div>
              <ProjectList ships={group.ships} />
            </div>
          );
        })
      )}
    </section>
  );
}
