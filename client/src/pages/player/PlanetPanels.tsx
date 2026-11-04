import type { Planet, PlanetResource } from '../../api/planetApi';
import { ResourceIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { formatNumber, formatSigned, namer } from './planetText';
import { FullResourcesAlert, ResourceStorage } from './ResourceStorage';

type Names = ReturnType<typeof namer>;

/**
 * The growth of the body: what every resource brings per hour, or a dash for a resource that is
 * not mined here. The resources are in blocks by their group, so the player sees the kinds of the
 * resources apart. A full resource raises the alarm.
 */
export function ResourcesPanel({ planet, names }: { planet: Planet; names: Names }) {
  const { t } = useI18n();
  // The balances (energy, population and the like) have their own blocks on the screen; the server
  // marks them, so the client knows none of them by its code.
  const notMinedIds = new Set(planet.resourceTypes.filter((resource) => resource.isBalance === true).map((resource) => resource.id));
  const mined = planet.resources.filter((resource) => !notMinedIds.has(resource.resourceTypeId));
  const groupIds = new Set(planet.resourceGroups.map((group) => group.id));
  // The resources in blocks by their group: the name of the group is the heading of the block. A
  // resource without a known group goes to the last block, so no resource of the body is lost.
  const others = mined.filter((resource) => resource.resourceGroupId === null || !groupIds.has(resource.resourceGroupId));
  const groups = [
    ...planet.resourceGroups.map((group) => ({
      key: String(group.id),
      title: names.group(group.id),
      resources: mined.filter((resource) => resource.resourceGroupId === group.id),
    })),
    { key: 'other', title: t('planet.resourcesOther'), resources: others },
  ].filter((entry) => entry.resources.length > 0);

  return (
    <section className="panel" aria-labelledby="planet-resources">
      <div className="panel__head">
        <h2 className="panel__title" id="planet-resources">
          {t('planet.resources')}
        </h2>
        <span className="meta">{t('planet.resourcesSubtitle')}</span>
      </div>
      <FullResourcesAlert names={names} planet={planet} />
      <div className="planet-table-wrap">
        {groups.map(({ key, title, resources }) => (
          <div className="planet-resources__group" key={key}>
            <h3 className="planet-resources__title">{title}</h3>
            <table className="planet-table">
              <thead>
                <tr role="row">
                  <th scope="col">{t('planet.resource')}</th>
                  <th scope="col">{t('planet.rate')}</th>
                </tr>
              </thead>
              <tbody>
                {resources.map((resource) => (
                  <tr key={resource.resourceTypeId} role="row">
                    <td>
                      <span className="resource-name">
                        <ResourceIcon groupCode={names.resourceGroupCode(resource.resourceTypeId)} />
                        <span>{names.resource(resource.resourceTypeId)}</span>
                      </span>
                    </td>
                    <td className="num">
                      <ResourceRate resource={resource} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
      </div>
    </section>
  );
}

/**
 * The hourly figure of one mined resource: its production, or a dash for a resource that is not
 * mined here.
 */
function ResourceRate({ resource }: { resource: PlanetResource }) {
  const { t, language } = useI18n();

  if (resource.ratePerHour > 0) {
    return (
      <>
        <span className="rate-plus">+{formatNumber(resource.ratePerHour, language)}</span>
        <span className="rate-unit">{t('planet.rate')}</span>
      </>
    );
  }

  return (
    <>
      <span className="rate-dash">—</span>
      <span>{t('planet.notMined')}</span>
    </>
  );
}

/**
 * What the storage of the body holds: under the name of every group a row for every resource
 * with the amount against the capacity of the resource, the bar and the badge of a full resource.
 */
export function StoragePanel({ planet, names }: { planet: Planet; names: Names }) {
  const { t } = useI18n();

  return (
    <section className="panel" aria-labelledby="planet-storage">
      <h2 className="panel__title" id="planet-storage">
        {t('planet.storage')}
      </h2>
      <ResourceStorage names={names} planet={planet} />
    </section>
  );
}

/**
 * The energy of the body now and once the queue is done.
 */
export function EnergyPanel({ planet }: { planet: Planet }) {
  const { t, language } = useI18n();
  const { energy } = planet;

  return (
    <section className="panel" aria-labelledby="planet-energy">
      <h2 className="panel__title" id="planet-energy">
        {t('planet.energy')}
      </h2>
      <dl className="facts">
        <dt>{t('planet.energyOutput')}</dt>
        <dd className="num">{formatNumber(energy.output, language)}</dd>
        <dt>{t('planet.energyConsumption')}</dt>
        <dd className="num">{formatNumber(energy.consumption, language)}</dd>
        <dt>{t('planet.energyBalance')}</dt>
        <dd className="num">{formatSigned(energy.balance, language)}</dd>
        <dt>{t('planet.energyQueued')}</dt>
        <dd className="num">{formatSigned(energy.queuedBalance, language)}</dd>
      </dl>
      <p className="meta">{t('planet.energyNote')}</p>
    </section>
  );
}
