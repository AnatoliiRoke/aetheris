import type { ReactNode } from 'react';
import type { FleetInFlight } from '../../../api/fleetApi';
import type { MapField, MapObject, MapSector, MapSystem, MapTargetInfo } from '../../../api/mapApi';
import { ResourceIcon } from '../../../components/GameIcon';
import { GameIcon, type GameIconName } from '../../../components/GameIcon';
import { CardArt } from '../../../components/CardArt';
import { Glyph } from '../../../components/Glyph';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatAmount, formatCoefficient, formatDuration, formatNumber } from '../planetText';

/** What the card shows. */
export type MapSelection =
  | { kind: 'sector'; sector: MapSector }
  | { kind: 'system'; system: MapSystem; sectorName: string }
  | { kind: 'object'; object: MapObject; systemName: string; sectorName: string }
  | { kind: 'field'; field: MapField; sectorName: string }
  | { kind: 'point'; info: MapTargetInfo; sectorName: string }
  | { kind: 'fleet'; fleet: FleetInFlight; secondsLeft: number };

/** What the card needs from the screen. */
export interface MapCardContext {
  typeName: (typeId: number | null) => string;
  typeImage: (typeId: number | null) => string | null;
  sizeName: (sizeClassId: number | null) => string;
  resourceName: (resourceId: number) => string;
  resourceGroup: (resourceId: number) => string | null;
  /** Whether the distances are in the units of a system (the flight starts in the same system). */
  inSameSystem: boolean;
  /** Whether this selection is the target of a flight. */
  isTarget: boolean;
  onClose: () => void;
  onOpenSector: (sectorId: number) => void;
  onOpenSystem: (systemId: number) => void;
  onOpenPlanet: (planetId: number) => void;
  onSetTarget: () => void;
  onClearTarget: () => void;
  /** Opens the window that writes the flight plan for the fleet the player chooses. */
  onSendFleet: () => void;
  /** Whether the player has a fleet that may take the flight: without one the button is off. */
  canSendFleet: boolean;
  /** Opens the screen of the fleets (task 00059). */
  onOpenFleets: () => void;
}

const futureActions: { icon: GameIconName; key: 'colonize' | 'mine' | 'scout' | 'attack' | 'escort' }[] = [
  { icon: 'mapPlus', key: 'colonize' },
  { icon: 'mine', key: 'mine' },
  { icon: 'mapScout', key: 'scout' },
  { icon: 'mapAttack', key: 'attack' },
  { icon: 'mapEscort', key: 'escort' },
];

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="map-card__row">
      <span className="map-card__key">{label}</span>
      <span className="map-card__value">{children}</span>
    </div>
  );
}

/**
 * The card of what the player chose on the map (task 00034): the picture of the type, the name, the
 * facts the server gave, the distance and the time of the flight, the actions. The actions that
 * need a fleet are drawn as finished buttons that are off, with the reason, so it is plain where
 * they will be; the card of a point of space shows its coordinates and what forbids it.
 */
export function MapCard({ selection, context }: { selection: MapSelection; context: MapCardContext }) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const unit = t(context.inSameSystem ? 'map.unitAu' : 'map.unitLy');
  const distanceText = (value: number | null) => (value === null ? '—' : `${formatCoefficient(value, language)} ${unit}`);
  const timeText = (seconds: number | null, inSystem: boolean) =>
    seconds === null ? '—' : inSystem ? t('map.timeInSystem', { time: formatDuration(language, seconds) }) : `~${formatDuration(language, seconds)}`;

  let imageKey: string | null = null;
  let title = '';
  let chip = '';
  let rows: ReactNode = null;
  let actions: ReactNode = null;
  let showFlight = true;
  let distance: number | null = null;
  let seconds: number | null = null;
  let inSystemFlight = context.inSameSystem;
  let targetable = true;

  if (selection.kind === 'sector') {
    const { sector } = selection;

    title = t('map.sector', { name: sector.name });
    chip = t('map.sectorKind');
    showFlight = false;
    targetable = false;
    rows = (
      <>
        <Row label={t('map.systems')}>{formatNumber(sector.systemCount, language)}</Row>
        <Row label={t('map.ownPlanetsLabel')}>{formatNumber(sector.ownPlanetCount, language)}</Row>
        {sector.isNewbie ? <Row label={t('map.sectorKind')}>{t('map.newbies')}</Row> : null}
      </>
    );
    actions = (
      <button className="btn btn--primary" type="button" onClick={() => context.onOpenSector(sector.id)}>
        <GameIcon name="mapGalaxy" size={18} />
        {t('map.openSector')}
      </button>
    );
  } else if (selection.kind === 'system') {
    const { system } = selection;
    const open = system.state === 'open';

    title = system.name ?? t('map.unknownStar');
    chip = t('map.starKind');
    distance = system.distance;
    seconds = system.travelSeconds;
    rows = (
      <>
        <Row label={t('map.place')}>{`${t('map.sector', { name: selection.sectorName })} · ${t('civ.system')} ${system.name ?? system.number}`}</Row>
        {open ? <Row label={t('map.bodies')}>{formatNumber(system.objectCount, language)}</Row> : <p className="map-card__note">{t('map.needRadar')}</p>}
        {system.fromScan && system.scannedAt ? <p className="map-card__note">{t('map.scanData', { date: new Date(system.scannedAt).toLocaleString(language) })}</p> : null}
        {system.inNebula ? <Row label={t('map.nebulaKind')}>{t('map.inNebula')}</Row> : null}
      </>
    );
    actions = (
      <button className="btn btn--primary" disabled={!open} title={open ? undefined : t('map.needRadar')} type="button" onClick={() => context.onOpenSystem(system.id)}>
        <GameIcon name="search" size={18} />
        {t('map.openSystem')}
      </button>
    );
  } else if (selection.kind === 'field') {
    const { field } = selection;

    imageKey = context.typeImage(field.objectTypeId);
    title = field.name;
    chip = context.typeName(field.objectTypeId);
    distance = field.distance;
    seconds = field.travelSeconds;
    rows = (
      <>
        <Row label={t('map.place')}>{`${t('map.sector', { name: selection.sectorName })} · ${t('map.openSpace')}`}</Row>
        <Row label={t('map.mass')}>{formatAmount(field.mass, language)}</Row>
        {field.scannedAt ? <p className="map-card__note">{t('map.scanData', { date: new Date(field.scannedAt).toLocaleString(language) })}</p> : null}
        {field.resources?.map((share) => <Row key={share.resourceId} label={context.resourceName(share.resourceId)}>{formatCoefficient(share.coefficient, language)}</Row>)}
      </>
    );
  } else if (selection.kind === 'object') {
    const { object } = selection;
    const known = object.state === 'known';

    imageKey = context.typeImage(object.objectTypeId);
    title = known ? t('map.unknownObject', { orbit: object.orbit ?? 0 }) : object.name ?? '';
    chip = known ? t('map.unknownKind') : context.typeName(object.objectTypeId);
    distance = object.distance;
    seconds = object.travelSeconds;
    inSystemFlight = context.inSameSystem;
    rows = known ? (
      <>
        <Row label={t('map.place')}>{`${t('map.sector', { name: selection.sectorName })} · ${t('civ.system')} ${selection.systemName} · ${t('civ.orbit')} ${object.orbit ?? ''}`}</Row>
        <p className="map-card__note">{t('map.needRadar')}</p>
      </>
    ) : (
      <>
        {object.size !== null ? <Row label={t('map.size')}>{context.sizeName(object.sizeClassId)}</Row> : null}
        <Row label={t('map.place')}>{`${t('map.sector', { name: selection.sectorName })} · ${t('civ.system')} ${selection.systemName} · ${t('civ.orbit')} ${object.orbit ?? ''}`}</Row>
        {object.planetId !== null || object.owner ? (
          <Row label={t('map.owner')}>
            {object.owner ? (
              <span className="map-owner">
                <Glyph colorKey={object.owner.raceColorKey} small />
                {object.owner.nickname}
                <span className="meta"> · {text(object.owner.raceNameKey) ?? object.owner.raceNameKey}</span>
              </span>
            ) : object.isOwn ? (
              t('map.mine')
            ) : (
              t('map.free')
            )}
          </Row>
        ) : null}
        {object.mass !== null ? <Row label={t('map.mass')}>{formatAmount(object.mass, language)}</Row> : null}
        {object.planetId !== null || object.mass !== null ? (
          <div className="map-card__resources">
            <span className="map-card__key">{t('map.deposits')}</span>
            {object.resources.length > 0 ? (
              <ul className="map-chips">
                {object.resources.map((share) => (
                  <li className="map-chip" key={share.resourceId}>
                    <ResourceIcon groupCode={context.resourceGroup(share.resourceId)} />
                    {context.resourceName(share.resourceId)}
                    <span className="num">{formatCoefficient(share.coefficient, language)}</span>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="map-card__note">
                <GameIcon name="mapUnknown" size={14} />
                {t('map.notScanned')}
              </p>
            )}
          </div>
        ) : null}
      </>
    );
    targetable = true;
    actions = object.isOwn && object.planetId !== null ? (
      <button className="btn btn--primary" type="button" onClick={() => context.onOpenPlanet(object.planetId!)}>
        <GameIcon name="planet" size={18} />
        {t('map.openPlanet')}
      </button>
    ) : null;
  } else if (selection.kind === 'fleet') {
    const { fleet, secondsLeft } = selection;

    title = fleet.name;
    chip = t('map.fleetInFlight');
    showFlight = false;
    targetable = false;
    rows = (
      <>
        <Row label={t('map.fleetTo')}>
          {fleet.flight.toSystemId !== null ? (
            <span>{t('map.fleetToSystem')}</span>
          ) : fleet.flight.toObjectId !== null ? (
            <span>{t('map.fleetTarget')}</span>
          ) : (
            <span className="num">{`${formatCoefficient(fleet.flight.toX, language)} · ${formatCoefficient(fleet.flight.toY, language)} · ${formatCoefficient(fleet.flight.toZ, language)}`}</span>
          )}
        </Row>
        <Row label={t('map.fleetArrives')}>
          <span className="num">{secondsLeft > 0 ? t('map.fleetArrivesIn', { time: formatDuration(language, secondsLeft) }) : t('map.fleetArrived')}</span>
        </Row>
      </>
    );
    actions = (
      <button className="btn btn--primary" type="button" onClick={context.onOpenFleets}>
        <GameIcon name="fleets" size={18} />
        {t('map.openFleets')}
      </button>
    );
  } else {
    const { info } = selection;

    title = t('map.pointTitle');
    chip = t('map.pointKind');
    distance = info.distance;
    seconds = info.travelSeconds;
    inSystemFlight = false;
    rows = (
      <>
        <Row label={t('map.coordinates')}>
          <span className="num">{`${formatCoefficient(info.x, language)} · ${formatCoefficient(info.y, language)} · ${formatCoefficient(info.z, language)}`}</span>
        </Row>
        <Row label={t('map.place')}>
          {info.nearSystemName
            ? t('map.nearSystem', { sector: selection.sectorName, name: info.nearSystemName })
            : t('map.sector', { name: selection.sectorName })}
        </Row>
        {info.inNebula ? <Row label={t('map.nebulaKind')}>{t('map.inNebula')}</Row> : null}
        {info.jumpForbidden ? <p className="map-card__note">{t('map.jumpForbidden')}</p> : null}
        {info.state === 'unknown' ? <p className="map-card__note">{t('map.needRadar')}</p> : null}
        {info.errors.map((code) => (
          <p className="map-card__note map-card__note--error" key={code}>
            {t(`map.error.${code}` as never)}
          </p>
        ))}
      </>
    );
  }

  const unitFlight = inSystemFlight && selection.kind !== 'point' && selection.kind !== 'field';

  return (
    <aside aria-label={t('map.card')} className="map-card">
      {selection.kind === 'object' || selection.kind === 'field' ? <CardArt imageKey={imageKey} label={title} /> : null}
      <button aria-label={t('map.closeCard')} className="map-card__close" type="button" onClick={context.onClose}>
        <GameIcon name="mapClose" size={18} />
      </button>
      <div className="map-card__head">
        <h2 className="map-card__title">{title}</h2>
        {chip ? <span className="chip">{chip}</span> : null}
        {context.isTarget ? <span className="chip map-chip--target">{t('map.targetChip')}</span> : null}
      </div>
      <div className="map-card__rows">
        {rows}
        {showFlight ? (
          <>
            <Row label={t('map.distance')}>
              <span className="num">{distanceText(distance)}</span>
            </Row>
            <div className="map-card__flight">
              <GameIcon name="mapClock" size={16} />
              <span>{t('map.flightTime')}</span>
              <b className="num">{timeText(seconds, unitFlight)}</b>
            </div>
          </>
        ) : null}
      </div>
      <div className="map-card__actions">
        {actions}
        {targetable ? (
          context.isTarget ? (
            <button className="btn btn--secondary" type="button" onClick={context.onClearTarget}>
              <GameIcon name="mapClose" size={18} />
              {t('map.clearTarget')}
            </button>
          ) : (
            <button className="btn btn--secondary" type="button" onClick={context.onSetTarget}>
              <GameIcon name="mapTarget" size={18} />
              {t('map.setTarget')}
            </button>
          )
        ) : null}
        {showFlight ? (
          <>
            <button
              aria-describedby="map-fleet-note"
              className="btn btn--secondary"
              disabled={!context.canSendFleet}
              onClick={context.onSendFleet}
              title={context.canSendFleet ? undefined : t('map.needFleet')}
              type="button"
            >
              <GameIcon name="mapRoute" size={18} />
              {t('map.sendFleet')}
            </button>
            {futureActions.map((action) => (
              <button aria-describedby="map-fleet-note" className="btn btn--secondary" disabled key={action.key} title={t('map.needFleet')} type="button">
                <GameIcon name={action.icon} size={18} />
                {t(`map.action.${action.key}` as never)}
              </button>
            ))}
            <p className="map-card__note" id="map-fleet-note">
              <GameIcon name="mapClock" size={14} />
              {t('map.needFleet')}
            </p>
          </>
        ) : null}
      </div>
    </aside>
  );
}
