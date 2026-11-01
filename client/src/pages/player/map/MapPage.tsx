import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import type { MapField, MapGalaxy, MapObject, MapSearchResult, MapSectorView, MapSystem, MapSystemView } from '../../../api/mapApi';
import { useApi } from '../../../api/useApi';
import { Alert } from '../../../components/Alert';
import { GameIcon, type GameIconName } from '../../../components/GameIcon';
import { Glyph } from '../../../components/Glyph';
import { useDocumentTitle } from '../../../components/useDocumentTitle';
import { useI18n } from '../../../i18n/useI18n';
import { routes } from '../../../router/context';
import { Link } from '../../../router/Router';
import { useRouter } from '../../../router/useRouter';
import { useTexts } from '../../../texts/useTexts';
import { ScreenHead } from '../ScreenHead';
import { formatCoefficient, formatDuration } from '../planetText';
import { usePlanet } from '../usePlanet';
import { useResourceGroups } from '../useResourceGroups';
import { useResourceNames } from '../useResourceNames';
import { useCurrentPlanetId, useShellActions } from '../useShell';
import { useWorldReference } from '../useWorldReference';
import { MapCard, type MapCardContext, type MapSelection } from './MapCard';
import { MapList, type MapListRow } from './MapList';
import { GalaxyLayer, SectorLayer, SystemLayer, type LayerContext } from './MapLayersDraw';
import { allLayersOn, objectClasses, type MapLayer, type MapLayers } from './mapLayers';
import { buildQuery, parseLocation, targetToText, type MapLocation, type MapTarget } from './mapLocation';
import { orbitPlace, systemWorldSize, type Bounds, type MapView, type Viewport } from './mapGeometry';
import { LayersPanel, MapLegend, MiniMap, type MiniPoint } from './MapOverlays';
import { MapRoute } from './MapRoute';
import { MapSearch } from './MapSearch';
import { useMapTarget } from './useMapTarget';
import { MapViewportArea, type MapFocus } from './MapViewport';
import { useMapResource } from './useMapResource';

type Level = 'galaxy' | 'sector' | 'system';

/** The share of a sector added round the galaxy when it is fitted into the area. */
const galaxyPadding = 0.1;

const stateIcon: Record<string, GameIconName> = { star: 'mapSystem', planet: 'mapPlanet', gas_giant: 'mapGiant', asteroid_field: 'mapField', unknown: 'mapUnknown' };

/**
 * The map (task 00034): the galaxy, a sector and a star system as one screen with three scales,
 * the layers, the search, the card of what is chosen, the list view and the pointing at a target of
 * a flight. Everything the player sees, every distance and every time comes from the server; the
 * screen draws the answers. What is chosen, where the map is and the target are in the address.
 */
export function MapPage() {
  const { t, language } = useI18n();
  const { text } = useTexts();
  useDocumentTitle(t('meta.map.title'));

  const { map } = useApi();
  const { query, navigate } = useRouter();
  const planetId = useCurrentPlanetId();
  const { planet } = usePlanet(planetId);
  const reference = useWorldReference();
  const { selectPlanet } = useShellActions();
  const targetContext = useMapTarget();
  const resourceName = useResourceNames(reference, text);
  const resourceGroup = useResourceGroups(reference);
  const location = useMemo(() => parseLocation(query), [query]);
  const home = planet?.coordinates ?? null;

  const [layers, setLayers] = useState<MapLayers>(allLayersOn);
  const [focus, setFocus] = useState<MapFocus | null>(null);
  const miniUpdate = useRef<((view: MapView, viewport: Viewport) => void) | null>(null);
  const focusToken = useRef(0);

  const level: Level = location.systemId !== null ? 'system' : location.sectorId !== null ? 'sector' : 'galaxy';
  const levelKey = `${level}:${location.sectorId ?? ''}:${location.systemId ?? ''}`;
  const [chosen, setChosen] = useState<{ levelKey: string; key: string | null } | null>(null);
  const selectedKey = chosen && chosen.levelKey === levelKey ? chosen.key : location.objectId !== null ? `object:${location.objectId}` : null;

  const go = useCallback(
    (patch: Partial<MapLocation>, replace = false) => navigate(`${routes.map}${buildQuery({ ...location, ...patch })}`, { replace }),
    [location, navigate],
  );

  // A visit with no place shows the sector of the body the player looks from.
  useEffect(() => {
    if (!location.galaxy && location.sectorId === null && location.systemId === null && home) {
      navigate(`${routes.map}${buildQuery({ ...location, sectorId: home.sectorId })}`, { replace: true });
    }
  }, [home, location, navigate]);

  // The target lives in the address and in the context the fleets will read.
  const addressTarget = location.target ? targetToText(location.target) : null;
  const contextTarget = targetContext.target ? targetToText(targetContext.target) : null;
  const { setTarget: setContextTarget } = targetContext;

  useEffect(() => {
    if (addressTarget !== contextTarget) {
      setContextTarget(location.target);
    }
    // The texts are the comparison: the objects are new on every read of the address.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [addressTarget, contextTarget, setContextTarget]);

  const galaxyRead = useMapResource('galaxy', () => map.galaxy());
  const sectorRead = useMapResource(level === 'sector' ? `sector:${location.sectorId}:${planetId}` : null, () => map.sector(location.sectorId!, planetId));
  const systemRead = useMapResource(level === 'system' ? `system:${location.systemId}:${planetId}` : null, () => map.system(location.systemId!, planetId));
  const pointTarget = location.target?.kind === 'point' && level === 'sector' && location.target.sectorId === location.sectorId ? location.target : null;
  const pointRead = useMapResource(pointTarget ? `target:${targetToText(pointTarget)}:${planetId}` : null, () => map.target(pointTarget!.sectorId, pointTarget!.x, pointTarget!.y, pointTarget!.z, planetId));

  const galaxy: MapGalaxy | null = galaxyRead.data;
  const sector: MapSectorView | null = sectorRead.data;
  const systemView: MapSystemView | null = systemRead.data;
  const sectorNames = useMemo(() => new Map((galaxy?.sectors ?? []).map((entry) => [entry.id, entry.name])), [galaxy]);
  const sectorName = level === 'system' ? systemView?.sectorName ?? '' : sector?.name ?? sectorNames.get(location.sectorId ?? 0) ?? '';

  const typeById = useMemo(() => new Map((reference?.objectTypes ?? []).map((type) => [type.id, type])), [reference]);
  const typeName = useCallback((id: number | null) => (id === null ? t('map.unknownKind') : text(typeById.get(id)?.nameKey ?? '') ?? typeById.get(id)?.nameKey ?? t('map.unknownKind')), [typeById, text, t]);
  const typeImage = useCallback((id: number | null) => (id === null ? null : typeById.get(id)?.imageKey ?? null), [typeById]);
  const sizeName = useCallback(
    (id: number | null) => {
      const size = reference?.sizeClasses.find((entry) => entry.id === id);

      return size ? text(size.nameKey) ?? size.nameKey : '—';
    },
    [reference, text],
  );

  const setSelectedKey = useCallback(
    (key: string | null) => {
      setChosen({ levelKey, key });
      const objectId = key?.startsWith('object:') ? Number(key.slice('object:'.length)) : null;

      if (objectId !== location.objectId) {
        go({ objectId }, true);
      }
    },
    [levelKey, location.objectId, go],
  );

  const setTarget = useCallback(
    (target: MapTarget | null) => {
      setContextTarget(target);
      go({ target }, true);
    },
    [go, setContextTarget],
  );

  const requestFocus = (x: number, y: number) => {
    focusToken.current += 1;
    setFocus({ x, y, token: focusToken.current });
  };

  const bounds = useMemo<Bounds>(() => {
    if (level === 'sector') {
      const size = sector?.sectorSize ?? galaxy?.sectorSize ?? 1;

      return { minX: 0, minY: 0, maxX: size, maxY: size };
    }

    if (level === 'system') {
      return { minX: 0, minY: 0, maxX: systemWorldSize, maxY: systemWorldSize };
    }

    const sectors = galaxy?.sectors ?? [];
    const size = galaxy?.sectorSize ?? 1;

    if (sectors.length === 0) {
      return { minX: 0, minY: 0, maxX: size, maxY: size };
    }

    return {
      minX: Math.min(...sectors.map((entry) => entry.x)) - size * galaxyPadding,
      minY: Math.min(...sectors.map((entry) => entry.y)) - size * galaxyPadding,
      maxX: Math.max(...sectors.map((entry) => entry.x)) + size * (1 + galaxyPadding),
      maxY: Math.max(...sectors.map((entry) => entry.y)) + size * (1 + galaxyPadding),
    };
  }, [level, sector, galaxy]);

  const inSameSystem = level === 'system' && systemView !== null && home?.systemId === systemView.system.id;
  const originInSector = level === 'sector' && sector && home && home.sectorId === sector.sectorId ? sector.systems.find((entry) => entry.id === home.systemId) ?? null : null;
  const ownBody = level === 'system' ? systemView?.objects.find((entry) => entry.isOwn && entry.planetId === planetId) ?? null : null;

  const selection: MapSelection | null = useMemo(() => {
    if (selectedKey === 'target' && pointRead.data) {
      return { kind: 'point', info: pointRead.data, sectorName };
    }

    if (selectedKey === null) {
      return null;
    }

    const id = Number(selectedKey.slice(selectedKey.indexOf(':') + 1));

    if (level === 'galaxy') {
      const found = galaxy?.sectors.find((entry) => entry.id === id);

      return found ? { kind: 'sector', sector: found } : null;
    }

    if (level === 'sector' && sector) {
      if (selectedKey.startsWith('system:')) {
        const found = sector.systems.find((entry) => entry.id === id);

        return found ? { kind: 'system', system: found, sectorName: sector.name } : null;
      }

      const field = sector.fields.find((entry) => entry.objectId === id);

      return field ? { kind: 'field', field, sectorName: sector.name } : null;
    }

    if (level === 'system' && systemView) {
      const found = systemView.objects.find((entry) => entry.id === id);

      return found ? { kind: 'object', object: found, systemName: systemView.system.name ?? String(systemView.system.number), sectorName: systemView.sectorName } : null;
    }

    return null;
  }, [selectedKey, level, galaxy, sector, systemView, pointRead.data, sectorName]);

  const selectionTarget = useMemo<MapTarget | null>(() => {
    if (!selection) {
      return null;
    }

    if (selection.kind === 'system') {
      return { kind: 'system', systemId: selection.system.id };
    }

    if (selection.kind === 'object') {
      return { kind: 'object', objectId: selection.object.id, systemId: systemView?.system.id ?? null, sectorId: null };
    }

    if (selection.kind === 'field') {
      return { kind: 'object', objectId: selection.field.objectId, systemId: null, sectorId: sector?.sectorId ?? null };
    }

    if (selection.kind === 'point') {
      return location.target;
    }

    return null;
  }, [selection, systemView, sector, location.target]);

  const isTarget = selectionTarget !== null && addressTarget !== null && targetToText(selectionTarget) === addressTarget;

  const onSelect = (key: string) => {
    setSelectedKey(key);

    if (targetContext.picking && level !== 'galaxy') {
      const [kind, rawId] = key.split(':');
      const id = Number(rawId);

      if (kind === 'system') {
        setTarget({ kind: 'system', systemId: id });
      } else if (kind === 'object') {
        setTarget({ kind: 'object', objectId: id, systemId: systemView?.system.id ?? null, sectorId: sector?.sectorId ?? null });
      }
    }
  };

  const onOpen = (key: string) => {
    const [kind, rawId] = key.split(':');
    const id = Number(rawId);

    if (kind === 'sector') {
      go({ galaxy: false, sectorId: id, systemId: null, objectId: null });
    } else if (kind === 'system') {
      const system = sector?.systems.find((entry) => entry.id === id);

      if (system?.state === 'open') {
        go({ systemId: id, objectId: null });
      }
    }
  };

  const onBackgroundPoint = (world: { x: number; y: number }) => {
    if (targetContext.picking && level === 'sector' && sector) {
      const point: MapTarget = { kind: 'point', sectorId: sector.sectorId, x: Math.round(world.x * 10) / 10, y: Math.round(world.y * 10) / 10, z: 0 };

      setChosen({ levelKey, key: 'target' });
      setTarget(point);

      return;
    }

    if (!targetContext.picking) {
      setSelectedKey(null);
    }
  };

  const onEscape = () => {
    if (targetContext.picking) {
      targetContext.stopPicking();
    } else {
      setSelectedKey(null);
    }
  };

  const cardContext: MapCardContext = {
    typeName,
    typeImage,
    sizeName,
    resourceName,
    resourceGroup,
    inSameSystem,
    isTarget,
    onClose: () => setSelectedKey(null),
    onOpenSector: (id) => go({ galaxy: false, sectorId: id, systemId: null, objectId: null }),
    onOpenSystem: (id) => go({ systemId: id, objectId: null }),
    onOpenPlanet: (id) => {
      selectPlanet(id);
      navigate(routes.planet);
    },
    onSetTarget: () => selectionTarget && setTarget(selectionTarget),
    onClearTarget: () => setTarget(null),
  };

  const layerContext: LayerContext = { typeName, typeImage, selectedKey, onSelect, onOpen };

  const onZoomTo = (point: { x: number; y: number }) => requestFocus(point.x, point.y);

  const goHome = () => {
    if (!home) {
      return;
    }

    if (level === 'galaxy') {
      const own = galaxy?.sectors.find((entry) => entry.id === home.sectorId);

      if (own) {
        requestFocus(own.x + (galaxy?.sectorSize ?? 0) / 2, own.y + (galaxy?.sectorSize ?? 0) / 2);
      }
    } else if (level === 'sector' && originInSector) {
      requestFocus(originInSector.x, originInSector.y);
    } else if (level === 'system' && ownBody?.orbit) {
      const place = orbitPlace(ownBody.id, ownBody.orbit, systemView?.orbitCount ?? 1);

      requestFocus(place.x, place.y);
    } else {
      go({ galaxy: false, sectorId: home.sectorId, systemId: home.systemId, objectId: null });
    }
  };

  const unitOf = (same: boolean) => t(same ? 'map.unitAu' : 'map.unitLy');
  const distanceText = (value: number | null, same: boolean) => (value === null ? '—' : `${formatCoefficient(value, language)} ${unitOf(same)}`);
  const placeOfResult = (result: MapSearchResult) =>
    [t('map.sector', { name: sectorNames.get(result.sectorId) ?? String(result.sectorId) }), result.systemName ? `${t('civ.system')} ${result.systemName}` : '', result.orbit ? `${t('civ.orbit')} ${result.orbit}` : ''].filter(Boolean).join(' · ');

  const onChooseResult = (result: MapSearchResult) => {
    if (result.kind === 'system') {
      go({ galaxy: false, sectorId: result.sectorId, systemId: result.systemId, objectId: null });
    } else {
      go({ galaxy: false, sectorId: result.sectorId, systemId: result.systemId, objectId: result.objectId });
    }
  };

  const rows = useMemo<MapListRow[]>(() => {
    const show = (key: string, mode: 'map' | 'list' = 'map') => () => {
      setChosen({ levelKey, key });
      go({ mode, objectId: key.startsWith('object:') ? Number(key.slice('object:'.length)) : null });
    };
    const out: MapListRow[] = [];

    if (level === 'galaxy') {
      for (const entry of galaxy?.sectors ?? []) {
        out.push({ key: `sector:${entry.id}`, icon: 'mapGalaxy', name: t('map.sector', { name: entry.name }), type: t('map.sectorKind'), owner: entry.ownPlanetCount > 0 ? t('map.mine') : '—', place: t('map.systemCount', { count: entry.systemCount }), distance: '—', onShow: show(`sector:${entry.id}`) });
      }
    } else if (level === 'sector' && sector) {
      for (const entry of sector.systems.filter((system: MapSystem) => system.state === 'open' || system.ownPlanetCount > 0)) {
        out.push({ key: `system:${entry.id}`, icon: 'mapSystem', name: entry.name ?? t('map.unknownStar'), type: t('map.starKind'), owner: entry.ownPlanetCount > 0 ? t('map.mine') : '—', place: t('map.sector', { name: sector.name }), distance: distanceText(entry.distance, false), onShow: show(`system:${entry.id}`) });
      }

      for (const entry of sector.fields as MapField[]) {
        out.push({ key: `object:${entry.objectId}`, icon: 'mapField', name: entry.name, type: typeName(entry.objectTypeId), owner: '—', place: `${t('map.sector', { name: sector.name })} · ${t('map.openSpace')}`, distance: distanceText(entry.distance, false), onShow: show(`object:${entry.objectId}`) });
      }
    } else if (level === 'system' && systemView) {
      for (const entry of systemView.objects.filter((object: MapObject) => object.objectClass !== objectClasses.star)) {
        const known = entry.state === 'known';

        out.push({
          key: `object:${entry.id}`,
          icon: stateIcon[entry.objectClass] ?? 'mapPlanet',
          name: known ? t('map.unknownObject', { orbit: entry.orbit ?? 0 }) : entry.name ?? '',
          type: known ? t('map.unknownKind') : typeName(entry.objectTypeId),
          owner: entry.owner ? (
            <span className="map-owner">
              <Glyph colorKey={entry.owner.raceColorKey} small />
              {entry.owner.nickname}
            </span>
          ) : entry.isOwn ? (
            t('map.mine')
          ) : entry.isFree ? (
            t('map.free')
          ) : (
            '—'
          ),
          place: `${t('map.sector', { name: systemView.sectorName })} · ${t('civ.system')} ${systemView.system.name ?? systemView.system.number} · ${t('civ.orbit')} ${entry.orbit ?? ''}`,
          distance: distanceText(entry.distance, inSameSystem),
          onShow: show(`object:${entry.id}`),
        });
      }
    }

    return out;
    // The helpers read the language and the data above; they change with them.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [level, galaxy, sector, systemView, levelKey, inSameSystem, language, typeName, t]);

  const miniPoints = useMemo<MiniPoint[]>(() => {
    if (level === 'galaxy') {
      return (galaxy?.sectors ?? []).map((entry) => ({ x: entry.x + (galaxy?.sectorSize ?? 0) / 2, y: entry.y + (galaxy?.sectorSize ?? 0) / 2, mine: entry.ownPlanetCount > 0 }));
    }

    if (level === 'sector') {
      return (sector?.systems ?? []).map((entry) => ({ x: entry.x, y: entry.y, mine: entry.ownPlanetCount > 0 }));
    }

    return (systemView?.objects ?? []).filter((entry) => entry.orbit !== null).map((entry) => ({ ...orbitPlace(entry.id, entry.orbit!, systemView?.orbitCount ?? 1), mine: entry.isOwn }));
  }, [level, galaxy, sector, systemView]);

  const fog = (level === 'galaxy' ? galaxy?.seesOnlyOwn : level === 'sector' ? sector?.seesOnlyOwn : false) === true;
  const read = level === 'galaxy' ? galaxyRead : level === 'sector' ? sectorRead : systemRead;
  const ready = level === 'galaxy' ? galaxy !== null : level === 'sector' ? sector !== null : systemView !== null;
  const modeSwitch = (
    <div aria-label={t('map.view')} className="map-modes" role="group">
      {(['map', 'list'] as const).map((mode) => (
        <button aria-pressed={location.mode === mode} className="map-modes__button" key={mode} type="button" onClick={() => go({ mode })}>
          <GameIcon name={mode === 'map' ? 'map' : 'mapList'} size={18} />
          {t(mode === 'map' ? 'map.viewMap' : 'map.viewList')}
        </button>
      ))}
    </div>
  );

  const crumbs = [
    { label: t('map.galaxy'), to: level === 'galaxy' ? undefined : `${routes.map}?level=galaxy` },
    ...(level !== 'galaxy' && sectorName ? [{ label: t('map.sector', { name: sectorName }), to: level === 'sector' ? undefined : `${routes.map}?sector=${location.sectorId ?? sector?.sectorId ?? ''}` }] : []),
    ...(level === 'system' && systemView ? [{ label: t('map.system', { name: systemView.system.name ?? String(systemView.system.number) }) }] : []),
  ];

  let drawn: ReactNode = null;

  if (ready) {
    const routeFrom = level === 'sector' ? (originInSector ? { x: originInSector.x, y: originInSector.y } : null) : level === 'system' && ownBody?.orbit ? orbitPlace(ownBody.id, ownBody.orbit, systemView?.orbitCount ?? 1) : null;
    let route: ReactNode = null;

    if (level === 'sector' && sector && location.target) {
      const targetInfo = location.target;
      const to =
        targetInfo.kind === 'system'
          ? sector.systems.find((entry) => entry.id === targetInfo.systemId)
          : targetInfo.kind === 'object'
            ? sector.fields.find((entry) => entry.objectId === targetInfo.objectId)
            : { x: targetInfo.x, y: targetInfo.y, distance: pointRead.data?.distance ?? null, travelSeconds: pointRead.data?.travelSeconds ?? null };

      if (to) {
        route = <MapRoute from={routeFrom} label={to.travelSeconds !== null && to.distance !== null ? `${distanceText(to.distance, false)} · ${formatDuration(language, to.travelSeconds)}` : null} name={t('map.targetMark')} size={sector.sectorSize} to={to} />;
      }
    } else if (level === 'system' && systemView && location.target?.kind === 'object') {
      const targetObject = systemView.objects.find((entry) => entry.id === (location.target as { objectId: number }).objectId);

      if (targetObject?.orbit) {
        route = <MapRoute from={routeFrom} label={targetObject.travelSeconds !== null && targetObject.distance !== null ? `${distanceText(targetObject.distance, inSameSystem)} · ${formatDuration(language, targetObject.travelSeconds)}` : null} name={t('map.targetMark')} size={systemWorldSize} to={orbitPlace(targetObject.id, targetObject.orbit, systemView.orbitCount)} />;
      }
    }

    drawn = (
      <div className="map-layout">
        <MapViewportArea
          bounds={bounds}
          controls={
            <>
              <button aria-label={t('map.myPlanet')} className="map-round" disabled={!home} title={t('map.myPlanet')} type="button" onClick={goHome}>
                <GameIcon name="planet" size={20} />
              </button>
              <button aria-label={t('map.pickTarget')} aria-pressed={targetContext.picking} className="map-round" title={t('map.pickTarget')} type="button" onClick={() => (targetContext.picking ? targetContext.stopPicking() : targetContext.startPicking())}>
                <GameIcon name="mapTarget" size={20} />
              </button>
            </>
          }
          focus={focus}
          key={levelKey}
          label={t('map.canvas')}
          overlay={
            <>
              <LayersPanel layers={layers} onChange={(layer: MapLayer, on) => setLayers((previous) => ({ ...previous, [layer]: on }))} />
              <MapLegend />
              <MiniMap bounds={bounds} label={level === 'system' ? t('map.system', { name: systemView?.system.name ?? '' }) : level === 'sector' ? t('map.sector', { name: sectorName }) : t('map.galaxy')} points={miniPoints} updateRef={miniUpdate} />
              {targetContext.picking ? <p className="map-hint">{level === 'galaxy' ? t('map.pickInGalaxy') : t('map.pickHint')}</p> : null}
            </>
          }
          picking={targetContext.picking}
          onBackgroundPoint={onBackgroundPoint}
          onEscape={onEscape}
          onViewChange={(view, viewport) => miniUpdate.current?.(view, viewport)}
        >
          {level === 'galaxy' && galaxy ? <GalaxyLayer context={layerContext} galaxy={galaxy} /> : null}
          {level === 'sector' && sector ? <SectorLayer context={layerContext} layers={layers} sector={sector} onZoomTo={onZoomTo} /> : null}
          {level === 'system' && systemView ? <SystemLayer context={layerContext} layers={layers} view={systemView} /> : null}
          {route}
        </MapViewportArea>
        {selection ? <MapCard context={cardContext} selection={selection} /> : null}
      </div>
    );
  }

  return (
    <div className="civ-main__in">
      <ScreenHead actions={modeSwitch} breadcrumbs={crumbs} title={t('civ.nav.map')} />
      <MapSearch placeText={placeOfResult} search={(queryText) => map.search(queryText)} onChoose={onChooseResult} />
      {fog ? (
        <Alert kind="info">
          <GameIcon name="mapRadar" size={16} /> {t('map.fog')} <Link to={routes.planetBuild}>{t('map.fogLink')}</Link>
        </Alert>
      ) : null}
      {read.failed && !ready ? (
        <div className="panel">
          <Alert kind="error">{t('map.loadFailed')}</Alert>
          <button className="btn btn--secondary" type="button" onClick={() => void read.reload()}>
            {t('planet.retry')}
          </button>
        </div>
      ) : !ready ? (
        <div aria-busy="true" className="map-skeleton" role="status">
          <span className="visually-hidden">{t('common.loading')}</span>
        </div>
      ) : location.mode === 'list' ? (
        <MapList rows={rows} />
      ) : (
        drawn
      )}
    </div>
  );
}
