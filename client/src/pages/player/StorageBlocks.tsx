import type { Planet } from '../../api/planetApi';
import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { ResourceStorage, FullResourcesAlert } from './ResourceStorage';
import { StorageTable } from './StorageTable';
import { formatAmount, formatNumber, type namer } from './planetText';
import { useStock } from './useStock';

/**
 * The resources of a body as the design draws them: under the name of every group a row for
 * every resource with the amount against the capacity of the resource, the bar and the badge of
 * a full resource, and the alarm of the resources that are full.
 *
 * @param planet The body.
 * @param names The names of the resources and the groups.
 */
export function ResourcesBlock({ planet, names }: { planet: Planet; names: ReturnType<typeof namer> }) {
  const { t } = useI18n();

  return (
    <section aria-labelledby="storage-resources" className="panel">
      <h2 className="panel__title" id="storage-resources">
        {t('storage.resources')}
      </h2>
      {planet.resources.length === 0 ? (
        <p className="lead">{t('storage.resourcesEmpty')}</p>
      ) : (
        <>
          <FullResourcesAlert names={names} planet={planet} />
          <ResourceStorage names={names} planet={planet} />
        </>
      )}
    </section>
  );
}

/**
 * The parts on a body: the name, the group and the quantity of every stack, and how many parts
 * of its kind lie on the body against the room of the kind, from the stock of the production
 * module.
 *
 * @param planetId The body.
 */
export function ModulesBlock({ planetId }: { planetId: number }) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const { stock, loaded, failed } = useStock(planetId);
  const kinds = new Map((stock?.storage.kinds ?? []).map((kind) => [kind.partTypeId, kind]));

  return (
    <section aria-labelledby="storage-modules" className="panel">
      <h2 className="panel__title" id="storage-modules">
        {t('storage.modules')}
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
              { id: 'kind', header: t('storage.kindTotal'), numeric: true },
            ]}
            label={t('storage.modules')}
            rows={stock.stacks.map((stack) => {
              const kind = kinds.get(stack.partTypeId);

              return {
                key: `${stack.partTypeId}-${stack.markLevel}-${stack.hasRaceBonus}-${stack.conditionPercent}`,
                cells: [
                  text(stack.nameKey) ?? stack.nameKey,
                  stack.partGroupNameKey ? text(stack.partGroupNameKey) ?? stack.partGroupNameKey : '—',
                  formatNumber(stack.quantity, language),
                  kind ? (
                    <span className="storage-resource__value" key="kind">
                      {kind.isFull ? <span className="badge badge--warning">{t('planet.storageFullBadge')}</span> : null}
                      <span>{t('planet.storageAmount', { amount: formatAmount(kind.quantity, language), capacity: formatAmount(kind.capacity, language) })}</span>
                    </span>
                  ) : (
                    '—'
                  ),
                ],
              };
            })}
          />
        </>
      )}
    </section>
  );
}
