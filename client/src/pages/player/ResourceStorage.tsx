import type { Planet, PlanetResource } from '../../api/planetApi';
import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { formatAmount, type namer } from './planetText';

type Names = ReturnType<typeof namer>;

const fullPercent = 100;

/**
 * The alarm of a full storage: the resources that are full and whose production stands still.
 * Nothing is drawn while no resource is full.
 *
 * @param planet The body.
 * @param names The names of the resources and the groups.
 */
export function FullResourcesAlert({ planet, names }: { planet: Planet; names: Names }) {
  const { t } = useI18n();
  const full = planet.resources.filter((resource) => resource.isFull);

  if (full.length === 0) {
    return null;
  }

  return <Alert kind="info">{t('planet.storageFull.text', { resources: full.map((resource) => names.resource(resource.resourceTypeId)).join(', ') })}</Alert>;
}

/**
 * The storage of a body by resource: under the name of every group a row for every resource of
 * the group with the amount against the capacity of the resource and a bar. Every resource has
 * the capacity of its own, which the server sends, so a full resource shows its badge while the
 * others of the group stay open.
 *
 * @param planet The body.
 * @param names The names of the resources and the groups.
 */
export function ResourceStorage({ planet, names }: { planet: Planet; names: Names }) {
  const groups = planet.resourceGroups
    .map((group) => ({ group, resources: planet.resources.filter((resource) => resource.resourceGroupId === group.id) }))
    .filter((entry) => entry.resources.length > 0);

  return (
    <div className="storage-groups">
      {groups.map(({ group, resources }) => (
        <div className="storage-group" key={group.id}>
          <h3 className="storage-group__title">{names.group(group.id)}</h3>
          <ul aria-label={names.group(group.id)} className="storage-resources">
            {resources.map((resource) => (
              <ResourceStorageRow key={resource.resourceTypeId} names={names} resource={resource} />
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

/**
 * A row of the storage: the name, the bar, and the amount against the capacity of the resource
 * with the badge of a full resource.
 */
function ResourceStorageRow({ resource, names }: { resource: PlanetResource; names: Names }) {
  const { t, language } = useI18n();
  const name = names.resource(resource.resourceTypeId);
  const percent = resource.capacity > 0 ? Math.min(fullPercent, Math.round((resource.amount / resource.capacity) * fullPercent)) : 0;

  return (
    <li className="storage-resource">
      <span className="storage-resource__name">{name}</span>
      <div aria-label={name} aria-valuemax={fullPercent} aria-valuemin={0} aria-valuenow={percent} className="track" role="progressbar">
        <i className={resource.isFull ? 'f-energy' : 'f-store'} style={{ width: `${percent}%` }} />
      </div>
      <span className="storage-resource__value num">
        {resource.isFull ? <span className="badge badge--warning">{t('planet.storageFullBadge')}</span> : null}
        <span>{t('planet.storageAmount', { amount: formatAmount(resource.amount, language), capacity: formatAmount(resource.capacity, language) })}</span>
      </span>
    </li>
  );
}
