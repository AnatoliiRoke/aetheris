import { useMemo, useState } from 'react';
import type { QueueItem } from '../../api/planetApi';
import type { OwnedPlanet } from '../../api/worldApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { BodyIdentity, Deposits, FactBar } from './CivilizationParts';
import { CivShell } from './CivShell';
import { BuildingQueue } from './BuildingQueue';
import { EnergyPanel } from './PlanetPanels';
import { formatNumber, fullestResource, namer } from './planetText';
import { ProcessesPanel } from './ProcessesPanel';
import { ProductionPanel } from './ProductionPanel';
import { ResearchPanel } from './ResearchPanel';
import { useBodies } from './useBodies';
import { useEconomy } from './useEconomy';
import { usePlanet } from './usePlanet';
import { useResourceGroups } from './useResourceGroups';
import { useResourceNames } from './useResourceNames';

/**
 * The screen of the civilization (task 00010): the bodies of the player with their real
 * resources, their storage, their energy, their population and their queues.
 *
 * Everything the tasks 00011–00015 have made is shown with the data of the server. The blocks
 * that no task has made yet are visual placeholders with the shape of the real content and
 * the word "Soon", never a made-up number, a made-up name or a made-up timer.
 *
 * The frame of the screen — the header, the menu and the bottom bar — is the common one of
 * every screen of the game (task 00020, section 8.1); this page gives it its own content.
 */
export function GamePage() {
  const { t } = useI18n();
  const { session } = useSession();

  if (session.status !== 'signed-in') {
    return null;
  }

  return (
    <CivShell active="overview">
      <div className="civ-main__in">
        <div className="civ-main__head">
          <h1>{t('player.game.title', { nickname: session.me.nickname })}</h1>
          <p className="lead">{t('civ.lead')}</p>
          <h2 className="civ-main__title">{t('civ.planets')}</h2>
          {session.me.emailConfirmed ? (
            <Link className="btn btn--secondary" to={routes.planet}>
              {t('player.game.build')}
            </Link>
          ) : (
            <button className="btn btn--secondary" disabled title={t('player.game.locked')} type="button">
              {t('player.game.build')}
            </button>
          )}
        </div>
        <CivBodies />
      </div>
    </CivShell>
  );
}

/**
 * The bodies of the player: the cards with the real data of every body.
 */
function CivBodies() {
  const { t } = useI18n();
  const { text } = useTexts();
  const [selected, setSelected] = useState<number | null>(null);
  const { bodies, reference, loaded, failed } = useBodies();
  const names = useResourceNames(reference, text);
  const groups = useResourceGroups(reference);
  const types = useMemo(() => new Map((reference?.objectTypes ?? []).map((type) => [type.id, type])), [reference]);
  const current = selected ?? bodies[0]?.planetId ?? null;

  return (
    <div className="civ-main__in">
      {failed ? <Alert kind="error">{t('civ.loadFailed')}</Alert> : null}

      {!loaded ? (
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      ) : bodies.length === 0 ? (
        <div className="civ-empty">
          <p>{t('civ.noBodies')}</p>
        </div>
      ) : (
        <div className="civ-bodies">
          {bodies.map((planet) => (
            <BodyCard
              current={current === planet.planetId}
              groups={groups}
              key={planet.planetId}
              names={names}
              onSelect={() => setSelected(planet.planetId)}
              planet={planet}
              type={types.get(planet.objectTypeId) ?? null}
            />
          ))}
        </div>
      )}
    </div>
  );
}

/**
 * A card of a body: the picture with the type and the place, the real data of the body and
 * the queues and the panels that the server has.
 */
function BodyCard({
  planet,
  type,
  names,
  groups,
  current,
  onSelect,
}: {
  planet: OwnedPlanet;
  type: { id: number; nameKey: string; imageKey: string | null } | null;
  names: (id: number) => string;
  groups: (id: number) => string | null;
  current: boolean;
  onSelect: () => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const { planets } = useApi();
  const { session } = useSession();
  const { planet: data, receivedAt, apply, reload } = usePlanet(planet.planetId);
  const { population } = useEconomy(planet.planetId);
  const confirmed = session.status === 'signed-in' && session.me.emailConfirmed;

  const onCancel = (item: QueueItem) =>
    void planets.cancel(planet.planetId, item.id).then((answer) => apply(answer));

  const typeName = type ? text(type.nameKey) ?? type.nameKey : t('civ.unknownType');
  // Every resource has the capacity of its own, so the bar of the storage shows the resource that
  // is the nearest to its limit: a full resource first, then the one with the most of its capacity.
  const storage = fullestResource(data?.resources ?? []);
  const stored = storage?.amount ?? 0;
  const capacity = storage?.capacity ?? 0;
  const energy = data?.energy ?? null;
  const balance = energy?.balance ?? 0;
  const consumption = energy?.consumption ?? 0;

  return (
    <article aria-label={planet.name ?? typeName} className={current ? 'civ-body is-current' : 'civ-body'}>
      <div className="civ-body__id">
        <BodyIdentity
          onSelect={onSelect}
          planet={planet}
          selected={current}
          typeImageKey={type?.imageKey ?? null}
          typeName={typeName}
        />
        <div className="civ-body__deposits">
          <span className="civ-label">{t('civ.deposits')}</span>
          <Deposits groups={groups} names={names} planet={planet} />
        </div>
      </div>

      <div className="civ-body__col">
        <span className="civ-label">{t('civ.facts')}</span>
        <div className="civ-bars">
          <FactBar
            label={t('civ.population')}
            percent={population && population.maximum > 0 ? (population.amount / population.maximum) * 100 : 0}
            tone="accent"
            value={
              population
                ? `${formatNumber(population.amount, language)} / ${formatNumber(population.maximum, language)}`
                : t('civ.noData')
            }
          />
          <FactBar
            label={t('civ.energy')}
            percent={consumption > 0 ? (balance / consumption) * 100 : balance > 0 ? 100 : 0}
            tone="energy"
            value={
              energy
                ? `${formatNumber(balance, language)} / ${formatNumber(consumption, language)}${balance < consumption ? ` · ${t('civ.deficit')} ${formatNumber(consumption - balance, language)}` : ''}`
                : t('civ.noData')
            }
            warning={energy !== null && balance < consumption}
          />
          <FactBar
            label={storage && data ? `${t('civ.storage')} · ${namer(data, language, text).resource(storage.resourceTypeId)}` : t('civ.storage')}
            percent={capacity > 0 ? (stored / capacity) * 100 : 0}
            tone="store"
            value={
              storage
                ? `${formatNumber(stored, language)} / ${formatNumber(capacity, language)}${storage.isFull ? ` · ${t('civ.full')}` : ''}`
                : t('civ.noData')
            }
            warning={storage?.isFull === true}
          />
        </div>
      </div>

      <div className="civ-body__col">
        {data && confirmed ? (
          <>
            <EnergyPanel planet={data} />
            <BuildingQueue
              busy={false}
              names={namer(data, language, text)}
              onCancel={onCancel}
              onFinished={() => void reload()}
              planet={data}
              receivedAt={receivedAt}
            />
          </>
        ) : (
          <p className="lead" role="status">
            {t('common.loading')}
          </p>
        )}

        <span className="civ-label">{t('civ.resources')}</span>
        {data && data.resources.length > 0 ? (
          <ul className="civ-yields">
            {data.resources
              .filter((resource) => resource.ratePerHour > 0)
              .map((resource) => (
                <li className="civ-yield" key={resource.resourceTypeId}>
                  <span>{names(resource.resourceTypeId)}</span>
                  <span className="num">
                    +{formatNumber(resource.ratePerHour, language)} {t('civ.perHour')}
                  </span>
                </li>
              ))}
          </ul>
        ) : (
          <p className="meta">{t('civ.noData')}</p>
        )}
      </div>

      <div className="civ-body__col civ-body__col--wide">
        {confirmed ? (
          <>
            <ResearchPanel planetId={planet.planetId} />
            <ProductionPanel planetId={planet.planetId} />
            <ProcessesPanel planetId={planet.planetId} />
          </>
        ) : (
          <p className="meta">{t('civ.lockedUntilConfirmed')}</p>
        )}
      </div>
    </article>
  );
}
