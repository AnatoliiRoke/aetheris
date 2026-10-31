import type { Planet, PlanetResource } from '../../api/planetApi';
import { Alert } from '../../components/Alert';
import { ResourceIcon } from '../../components/GameIcon';
import { resourceIconClass } from '../../components/resourceIcon';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { StorageTable, type StorageRow } from './StorageTable';
import { fillPercent, formatAmount, formatCoefficient, formatNumber, type namer } from './planetText';
import type { CharacteristicRow } from './useCharacteristics.helpers';
import type { StockState } from './useStock';

type Names = ReturnType<typeof namer>;

const fullPercent = 100;

/**
 * The alarm of the storage: the resources that are full and whose production stands still, the
 * advice to build more room and the way to the construction. Nothing is drawn while no resource
 * is full.
 *
 * @param planet The body.
 * @param names The names of the resources and the groups.
 */
export function StorageAlarm({ planet, names }: { planet: Planet; names: Names }) {
  const { t } = useI18n();
  const full = planet.resources.filter((resource) => resource.isFull);

  if (full.length === 0) {
    return null;
  }

  return (
    <Alert kind="info">
      {t('storage.alarm', { resources: full.map((resource) => names.resource(resource.resourceTypeId)).join(', ') })}{' '}
      <Link to={routes.planetBuild}>{t('storage.alarmLink')}</Link>
    </Alert>
  );
}

/**
 * The name of a resource with its icon and, for a full resource, the badge.
 */
function ResourceName({ resource, names }: { resource: PlanetResource; names: Names }) {
  const { t } = useI18n();

  return (
    <span className="resource-name">
      <ResourceIcon groupCode={names.resourceGroupCode(resource.resourceTypeId)} />
      <span>{names.resource(resource.resourceTypeId)}</span>
      {resource.isFull ? <span className="badge badge--warning">{t('planet.storageFullBadge')}</span> : null}
    </span>
  );
}

/**
 * The amount of a resource against its capacity; a resource whose group has no storage shows the
 * amount alone with the note that there is no storage.
 */
function AmountCell({ resource }: { resource: PlanetResource }) {
  const { t, language } = useI18n();

  return resource.capacity > 0 ? (
    <span>
      <span className="storage-qty">{formatAmount(resource.amount, language)}</span>
      {' / '}
      <span className="storage-cap">{formatAmount(resource.capacity, language)}</span>
    </span>
  ) : (
    <span title={t('planet.storageNoCapacityHint')}>{t('planet.storageNoCapacity', { amount: formatAmount(resource.amount, language) })}</span>
  );
}

/**
 * How full the storage of a resource is: the bar and the percent of the two numbers the server
 * sends; a resource without a capacity has nothing to fill.
 */
function FillCell({ resource, label }: { resource: PlanetResource; label: string }) {
  const { language } = useI18n();

  if (resource.capacity <= 0) {
    return <span>—</span>;
  }

  const percent = fillPercent(resource);

  return (
    <span className="fill-cell">
      <span aria-label={label} aria-valuemax={fullPercent} aria-valuemin={0} aria-valuenow={percent} className="track" role="progressbar">
        <i className={resource.isFull ? 'f-stripe' : 'f-store'} style={{ width: `${percent}%` }} />
      </span>
      <span className="num">{`${formatNumber(percent, language)}%`}</span>
    </span>
  );
}

/**
 * The raw resources of a body as the design draws them: a table with the name, the amount against
 * the capacity, the rate per hour and the fill, grouped by the group of the resource. The
 * produced resources have a table of their own.
 *
 * @param planet The body.
 * @param names The names of the resources and the groups.
 * @param producedIds The resources some recipe makes, which belong to the production table.
 */
export function ResourcesBlock({ planet, names, producedIds }: { planet: Planet; names: Names; producedIds: ReadonlySet<number> }) {
  const { t, language } = useI18n();
  const raw = planet.resources.filter((resource) => !producedIds.has(resource.resourceTypeId));
  const rows: StorageRow[] = planet.resourceGroups.flatMap((group) => {
    const members = raw.filter((resource) => resource.resourceGroupId === group.id);

    return members.length === 0
      ? []
      : [
          {
            key: `group-${group.id}`,
            heading: (
              <span className="storage-group">
                <span className={`storage-dot ${resourceIconClass(group.code)}`} />
                {names.group(group.id)}
              </span>
            ),
          },
          ...members.map((resource) => ({
            key: resource.resourceTypeId,
            cells: [
              <ResourceName key="name" names={names} resource={resource} />,
              <AmountCell key="amount" resource={resource} />,
              resource.ratePerHour > 0 ? (
                <span className="storage-rate" key="rate">
                  +{formatNumber(resource.ratePerHour, language)}
                </span>
              ) : (
                <span className="storage-rate storage-rate--none" key="rate">
                  —
                </span>
              ),
              <FillCell key="fill" label={names.resource(resource.resourceTypeId)} resource={resource} />,
            ],
          })),
        ];
  });

  return (
    <section aria-labelledby="storage-resources" className="panel storage-grid__wide">
      <h2 className="panel__title" id="storage-resources">
        {t('storage.resources')}
      </h2>
      {rows.length === 0 ? (
        <p className="lead">{t('storage.resourcesEmpty')}</p>
      ) : (
        <StorageTable
          columns={[
            { id: 'name', header: t('storage.partName') },
            { id: 'amount', header: t('planet.amount'), numeric: true },
            { id: 'rate', header: t('planet.rate'), numeric: true },
            { id: 'fill', header: t('storage.fill'), numeric: true },
          ]}
          label={t('storage.resources')}
          rows={rows}
        />
      )}
    </section>
  );
}

/**
 * The produced resources of a body, the real data of the server: the resources some recipe makes,
 * with the amount against the capacity and the fill.
 * While the list of the produced resources loads the block says so, and without any it says
 * there is nothing yet.
 *
 * @param planet The body.
 * @param names The names of the resources and the groups.
 * @param stock The state of the stock read, which holds the list of the produced resources.
 */
export function ProductionBlock({ planet, names, stock }: { planet: Planet; names: Names; stock: StockState }) {
  const { t } = useI18n();
  const produced = new Set(stock.stock?.producedResourceTypeIds ?? []);
  const members = planet.resources.filter((resource) => produced.has(resource.resourceTypeId));

  return (
    <section aria-labelledby="storage-production" className="panel">
      <h2 className="panel__title" id="storage-production">
        {t('storage.production')}
      </h2>
      {!stock.loaded ? (
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      ) : members.length === 0 ? (
        <p className="lead">{t('storage.productionEmpty')}</p>
      ) : (
        <StorageTable
          columns={[
            { id: 'name', header: t('storage.partName') },
            { id: 'amount', header: t('planet.amount'), numeric: true },
            { id: 'fill', header: t('storage.fill'), numeric: true },
          ]}
          label={t('storage.production')}
          rows={members.map((resource) => ({
            key: resource.resourceTypeId,
            cells: [
              <ResourceName key="name" names={names} resource={resource} />,
              <AmountCell key="amount" resource={resource} />,
              <FillCell key="fill" label={names.resource(resource.resourceTypeId)} resource={resource} />,
            ],
          }))}
        />
      )}
    </section>
  );
}

/**
 * The characteristics of a part: the name of every characteristic of its group, its value and
 * its unit, and the value at each level when the part has one. A characteristic the part has no
 * value for shows a dash, never a made-up number. The plaques use the tokens of the game and
 * stay at three in a row.
 *
 * @param rows The rows of the characteristics of the part.
 * @param language The language.
 */
export function CharacteristicList({ rows, language }: { rows: CharacteristicRow[]; language: Language }) {
  const { t } = useI18n();

  return (
    <ul className="storage-chars">
      {rows.map((row) => (
        <li className="storage-char" key={row.characteristicId}>
          <span className="storage-char__name">{row.name}</span>
          <span className="storage-char__value num">
            {row.value === null ? (
              '—'
            ) : (
              <>
                {formatCoefficient(row.value, language)}
                {row.unit ? <span className="meta"> {row.unit}</span> : null}
              </>
            )}
          </span>
          {row.levels.map((step) => (
            <span className="storage-char__level num" key={step.level}>
              {t('storage.levelValue', { level: step.level })} {formatCoefficient(step.value, language)}
              {row.unit ? <span className="meta"> {row.unit}</span> : null}
            </span>
          ))}
        </li>
      ))}
    </ul>
  );
}

/**
 * The parts on a body, as the design draws them: the name, the type and the quantity.
 *
 * @param stock The state of the stock read.
 */
export function ModulesBlock({ stock: state }: { stock: StockState }) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const { stock, loaded, failed } = state;

  return (
    <section aria-labelledby="storage-modules" className="panel">
      <h2 className="panel__title" id="storage-modules">
        {t('storage.modulesHeading')}
      </h2>
      {failed ? <Alert kind="error">{t('storage.modulesFailed')}</Alert> : null}
      {!loaded ? (
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      ) : stock === null ? null : stock.stacks.length === 0 ? (
        <p className="lead">{t('storage.modulesEmpty')}</p>
      ) : (
        <StorageTable
          columns={[
            { id: 'name', header: t('storage.partName') },
            { id: 'type', header: t('storage.partType') },
            { id: 'quantity', header: t('storage.quantity'), numeric: true },
          ]}
          label={t('storage.modules')}
          rows={stock.stacks.map((stack) => ({
            key: `${stack.partTypeId}-${stack.markLevel}-${stack.hasRaceBonus}-${stack.conditionPercent}`,
            cells: [
              text(stack.nameKey) ?? stack.nameKey,
              stack.partGroupNameKey ? text(stack.partGroupNameKey) ?? stack.partGroupNameKey : '—',
              <span key="quantity">
                {formatNumber(stack.quantity, language)} <span className="meta">{t('storage.unit')}</span>
              </span>,
            ],
          }))}
        />
      )}
    </section>
  );
}
