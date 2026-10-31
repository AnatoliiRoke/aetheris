import type { Planet } from '../../api/planetApi';
import { useI18n } from '../../i18n/useI18n';
import { formatNumber, namer } from './planetText';
import { FullResourcesAlert, ResourceStorage } from './ResourceStorage';

type Names = ReturnType<typeof namer>;

/**
 * The growth of the body: what every resource brings per hour, or a dash for a resource that is
 * not mined here, with the alarm of the resources that are full.
 */
export function ResourcesPanel({ planet, names }: { planet: Planet; names: Names }) {
  const { t, language } = useI18n();

  return (
    <section className="panel" aria-labelledby="planet-resources">
      <h2 className="panel__title" id="planet-resources">
        {t('planet.resources')}
      </h2>
      <FullResourcesAlert names={names} planet={planet} />
      <div className="planet-table-wrap">
        <table className="planet-table">
          <thead>
            <tr>
              <th scope="col">{t('planet.resource')}</th>
              <th scope="col">{t('planet.rate')}</th>
            </tr>
          </thead>
          <tbody>
            {planet.resources.map((resource) => (
              <tr key={resource.resourceTypeId}>
                <td>{names.resource(resource.resourceTypeId)}</td>
                <td className="num">{resource.ratePerHour > 0 ? `+${formatNumber(resource.ratePerHour, language)}` : t('planet.notMined')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
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
        <dd className="num">{formatNumber(energy.balance, language)}</dd>
        <dt>{t('planet.energyQueued')}</dt>
        <dd className="num">{formatNumber(energy.queuedBalance, language)}</dd>
      </dl>
    </section>
  );
}
