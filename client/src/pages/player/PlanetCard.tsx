import type { Planet } from '../../api/planetApi';
import { bodyArtClass } from '../../assets/images';
import { ResourceIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { BodyArt } from './CivilizationParts';
import { formatCoefficient, formatNumber, galaxyPlace } from './planetText';

/**
 * The card of a body (task 00020): the picture of its type, the type, the place in the galaxy,
 * the population, the energy and the richness of its deposits. Every number comes from the
 * answer of the server; a fact the server did not send is a dash, never a made-up number.
 *
 * @param planet The body.
 * @param typeName The name of the type of the body.
 * @param typeImageKey The key of the picture of the type.
 * @param resourceName The name of a resource by its identifier.
 * @param resourceGroup The code of the group of a resource, for its icon.
 */
export function PlanetCard({
  planet,
  typeName,
  typeImageKey,
  resourceName,
  resourceGroup,
}: {
  planet: Planet;
  typeName: string;
  typeImageKey: string | null;
  resourceName: (id: number) => string;
  resourceGroup: (id: number) => string | null;
}) {
  const { t, language } = useI18n();
  const { coordinates, energy } = planet;
  const coefficients = new Map(planet.depositRichness.map((deposit) => [deposit.resourceTypeId, deposit.coefficient]));
  const resourceIds = [
    ...planet.resources.map((resource) => resource.resourceTypeId),
    ...planet.depositRichness.map((deposit) => deposit.resourceTypeId).filter((id) => !planet.resources.some((resource) => resource.resourceTypeId === id)),
  ];

  return (
    <section aria-label={t('planet.card')} className="panel planet-card">
      <div className={bodyArtClass(typeImageKey)}>
        <BodyArt imageKey={typeImageKey} name={planet.name ?? typeName} />
      </div>
      <div className="planet-card__facts">
        <dl className="facts">
          <dt>{t('planet.type')}</dt>
          <dd>{typeName}</dd>
          <dt>{t('planet.coordinates')}</dt>
          <dd>{coordinates ? galaxyPlace(t, coordinates) : '—'}</dd>
          <dt>{t('planet.population')}</dt>
          <dd className="num">{`${formatNumber(planet.population, language)} / ${formatNumber(planet.populationMax, language)}`}</dd>
          <dt>{t('planet.energy')}</dt>
          <dd className="num">{`${formatNumber(energy.output, language)} / ${formatNumber(energy.consumption, language)}`}</dd>
        </dl>
        <div className="planet-card__deposits">
          <span className="civ-label">{t('planet.richness')}</span>
          <ul className="deposit-rows">
            {resourceIds.map((id) => {
              const coefficient = coefficients.get(id);

              return (
                <li key={id}>
                  <ResourceIcon groupCode={resourceGroup(id)} />
                  <span>{resourceName(id)}</span>
                  <span className={coefficient === undefined ? 'deposit-rows__empty' : 'num'}>
                    {coefficient === undefined ? '—' : `${formatCoefficient(coefficient, language)}×`}
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      </div>
    </section>
  );
}
