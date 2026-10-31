import { useCallback, useState } from 'react';
import type { Planet, PlanetResource } from '../../api/planetApi';
import { Alert } from '../../components/Alert';
import { ResourceIcon } from '../../components/GameIcon';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { StorageTable, type StorageRow } from './StorageTable';
import { fillPercent, formatAmount, formatCoefficient, formatNumber, storedTonnes, unitWeight, type namer } from './planetText';
import { useCharacteristics } from './useCharacteristics';
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
    <span>{t('planet.storageAmount', { amount: formatAmount(resource.amount, language), capacity: formatAmount(resource.capacity, language) })}</span>
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
        <i className={resource.isFull ? 'f-energy' : 'f-store'} style={{ width: `${percent}%` }} />
      </span>
      <span className="num">{`${formatNumber(percent, language)}%`}</span>
    </span>
  );
}

/**
 * The raw resources of a body as the design draws them: a table with the name, the amount against
 * the capacity, the rate per hour, the fill, the weight of one unit and the stored tonnes,
 * grouped by the group of the resource. The produced resources have a table of their own.
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
          { key: `group-${group.id}`, heading: names.group(group.id) },
          ...members.map((resource) => ({
            key: resource.resourceTypeId,
            cells: [
              <ResourceName key="name" names={names} resource={resource} />,
              <AmountCell key="amount" resource={resource} />,
              resource.isFull ? t('storage.rateStopped') : resource.ratePerHour > 0 ? `+${formatNumber(resource.ratePerHour, language)}` : '—',
              <FillCell key="fill" label={names.resource(resource.resourceTypeId)} resource={resource} />,
              <TonnesCell key="weight" tonnes={unitWeight(resource)} />,
              <TonnesCell key="volume" tonnes={storedTonnes(resource)} />,
            ],
          })),
        ];
  });

  return (
    <section aria-labelledby="storage-resources" className="panel">
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
            { id: 'weight', header: t('storage.weight'), numeric: true },
            { id: 'volume', header: t('storage.volume'), numeric: true },
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
 * with the amount against the capacity, the fill, the weight of one unit and the stored tonnes.
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
            { id: 'weight', header: t('storage.weight'), numeric: true },
            { id: 'volume', header: t('storage.volume'), numeric: true },
          ]}
          label={t('storage.production')}
          rows={members.map((resource) => ({
            key: resource.resourceTypeId,
            cells: [
              <ResourceName key="name" names={names} resource={resource} />,
              <AmountCell key="amount" resource={resource} />,
              <FillCell key="fill" label={names.resource(resource.resourceTypeId)} resource={resource} />,
              <TonnesCell key="weight" tonnes={unitWeight(resource)} />,
              <TonnesCell key="volume" tonnes={storedTonnes(resource)} />,
            ],
          }))}
        />
      )}
    </section>
  );
}

/**
 * The weight or the stored tonnes of a resource, with the unit of the weight.
 */
function TonnesCell({ tonnes }: { tonnes: number }) {
  const { t, language } = useI18n();

  return (
    <span>
      {formatCoefficient(tonnes, language)} <span className="meta">{t('storage.weightUnit')}</span>
    </span>
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
 * The parts on a body: the name, the group, the quantity and the weight of every stack, and how
 * many parts of its kind lie on the body against the room of the kind, from the stock of the
 * production module. The characteristics of a part show under its row, when the player opens
 * them.
 *
 * @param stock The state of the stock read.
 */
export function ModulesBlock({ stock: state }: { stock: StockState }) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const characteristics = useCharacteristics();
  const [opened, setOpened] = useState<ReadonlySet<string>>(() => new Set());
  const { stock, loaded, failed } = state;
  const kinds = new Map((stock?.storage.kinds ?? []).map((kind) => [kind.partTypeId, kind]));

  const toggle = useCallback((key: string) => {
    setOpened((current) => {
      const next = new Set(current);

      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }

      return next;
    });
  }, []);

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
        <>
          <p className="meta num">{t('production.stockCapacity', { capacity: formatAmount(stock.storage.capacity, language) })}</p>
          <StorageTable
            columns={[
              { id: 'name', header: t('storage.partName') },
              { id: 'type', header: t('storage.partType') },
              { id: 'quantity', header: t('storage.quantity'), numeric: true },
              { id: 'weight', header: t('storage.weight'), numeric: true },
              { id: 'kind', header: t('storage.kindTotal'), numeric: true },
            ]}
            label={t('storage.modules')}
            rows={stock.stacks.map((stack) => {
              const kind = kinds.get(stack.partTypeId);
              const key = `${stack.partTypeId}-${stack.markLevel}-${stack.hasRaceBonus}-${stack.conditionPercent}`;
              const rows = characteristics.rows(stack.partTypeId);
              const weight = characteristics.weight(stack.partTypeId);
              const isOpen = opened.has(key);

              return {
                key,
                cells: [
                  <span className="storage-part" key="name">
                    <span className="storage-part__name">{text(stack.nameKey) ?? stack.nameKey}</span>
                    {rows.length > 0 ? (
                      <button aria-expanded={isOpen} className="storage-toggle" key="toggle" type="button" onClick={() => toggle(key)}>
                        {t('storage.characteristics')}
                      </button>
                    ) : null}
                  </span>,
                  stack.partGroupNameKey ? text(stack.partGroupNameKey) ?? stack.partGroupNameKey : '—',
                  <span key="quantity">
                    {formatNumber(stack.quantity, language)} <span className="meta">{t('storage.unit')}</span>
                  </span>,
                  weight === null ? (
                    '—'
                  ) : (
                    <span key="weight">
                      {formatCoefficient(weight, language)} <span className="meta">{t('storage.weightUnit')}</span>
                    </span>
                  ),
                  kind ? (
                    <span className="storage-resource__value" key="kind">
                      {kind.isFull ? <span className="badge badge--warning">{t('planet.storageFullBadge')}</span> : null}
                      <span>{t('planet.storageAmount', { amount: formatAmount(kind.quantity, language), capacity: formatAmount(kind.capacity, language) })}</span>
                    </span>
                  ) : (
                    '—'
                  ),
                ],
                details: isOpen ? <CharacteristicList language={language} rows={rows} /> : undefined,
              };
            })}
          />
        </>
      )}
    </section>
  );
}
