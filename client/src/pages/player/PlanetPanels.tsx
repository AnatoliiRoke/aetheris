import type { Planet } from '../../api/planetApi';
import { ResourceIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { formatNumber, formatSigned, namer } from './planetText';
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
      <div className="panel__head">
        <h2 className="panel__title" id="planet-resources">
          {t('planet.resources')}
        </h2>
        <span className="meta">{t('planet.resourcesSubtitle')}</span>
      </div>
      <FullResourcesAlert names={names} planet={planet} />
      <div className="planet-table-wrap">
        <table className="planet-table">
          <thead>
            <tr role="row">
              <th scope="col">{t('planet.resource')}</th>
              <th scope="col">{t('planet.rate')}</th>
            </tr>
          </thead>
          <tbody>
            {planet.resources.map((resource) => (
              <tr key={resource.resourceTypeId} role="row">
                <td>
                  <span className="resource-name">
                    <ResourceIcon groupCode={names.resourceGroupCode(resource.resourceTypeId)} />
                    <span>{names.resource(resource.resourceTypeId)}</span>
                  </span>
                </td>
                <td className="num">
                  {resource.ratePerHour > 0 ? (
                    <>
                      <span className="rate-plus">+{formatNumber(resource.ratePerHour, language)}</span>
                      <span className="rate-unit">{t('planet.rate')}</span>
                    </>
                  ) : (
                    <>
                      <span className="rate-dash">—</span>
                      <span>{t('planet.notMined')}</span>
                    </>
                  )}
                </td>
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
        <dd className="num">{formatSigned(energy.balance, language)}</dd>
        <dt>{t('planet.energyQueued')}</dt>
        <dd className="num">{formatSigned(energy.queuedBalance, language)}</dd>
      </dl>
      <p className="meta">{t('planet.energyNote')}</p>
    </section>
  );
}
