import { useCallback, useMemo, useState } from 'react';
import type { ShipCatalog, ShipDesign, ShipDesignSlotInput, ShipHull, ShipModule, ShipyardCost } from '../../api/fleetApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { useRead } from './market/useRead';
import { ScreenHead } from './ScreenHead';
import { noticeFromError, type Notice } from './screenNotice';
import { Tabs, type TabItem } from './Tabs';
import { useCurrentPlanetId } from './useShell';
import { DesignWindow } from './shipyard/DesignWindow';
import { HullPicker } from './shipyard/HullPicker';
import { ModulePicker } from './shipyard/ModulePicker';
import { ShipDesignCard } from './shipyard/ShipDesignCard';
import { ShipyardWork } from './shipyard/ShipyardWork';
import { hullClasses } from './shipyard/shipyardParts';

/**
 * The prefixes of the refusals of the server that have their own text for the player here.
 */
const reasonPrefixes = ['fleet_'];

const noCosts: readonly ShipyardCost[] = [];

/**
 * The screen of the shipyard (task 00038): the tabs Design, Assembly and Dismantle, the list of
 * the designs of the player with the hulls and the modules, and the two steps of a new design:
 * the hull and the slots.
 *
 * The rules are the answer of the server: the screen shows why a module does not go into the
 * hull and never decides a rule of its own.
 */
export function ShipyardPage() {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const { fleet } = useApi();
  const planetId = useCurrentPlanetId();

  useDocumentTitle(t('meta.shipyard.title'));

  const [notice, setNotice] = useState<Notice>(null);
  const [busy, setBusy] = useState(false);
  const [tab, setTab] = useState('design');
  const [filter, setFilter] = useState('all');
  const [hullPicker, setHullPicker] = useState<'new' | 'change' | null>(null);
  const [editing, setEditing] = useState<ShipDesign | null>(null);
  const [working, setWorking] = useState<{ hull: ShipHull; name: string; hasRaceBonusSlot: boolean; slots: ShipDesignSlotInput[] } | null>(null);
  const [picking, setPicking] = useState<number | null>(null);

  const loadCatalog = useCallback(() => fleet.shipCatalog(planetId ?? 0), [fleet, planetId]);
  const loadDesigns = useCallback(() => fleet.designs(planetId ?? undefined), [fleet, planetId]);
  const loadShipyard = useCallback(() => fleet.shipyard(planetId ?? 0), [fleet, planetId]);

  const catalogState = useRead(loadCatalog, Boolean(planetId));
  const designsState = useRead(loadDesigns, Boolean(planetId));
  const shipyardState = useRead(loadShipyard, Boolean(planetId));

  const reloadCatalog = catalogState.reload;
  const reloadDesigns = designsState.reload;
  const reloadShipyard = shipyardState.reload;
  const costsByDesign = useMemo(() => {
    const byDesignId = new Map<number, readonly ShipyardCost[]>();

    for (const project of shipyardState.data?.designs ?? []) {
      byDesignId.set(project.designId, project.costs);
    }

    return byDesignId;
  }, [shipyardState.data]);
  const upkeepByDesign = useMemo(() => {
    const byDesignId = new Map<number, number>();

    for (const project of shipyardState.data?.designs ?? []) {
      if (project.upkeepPerHour !== undefined) {
        byDesignId.set(project.designId, project.upkeepPerHour);
      }
    }

    return byDesignId;
  }, [shipyardState.data]);

  const catalog: ShipCatalog | null = catalogState.data;
  const designs = useMemo(() => designsState.data ?? [], [designsState.data]);
  const hulls = useMemo(() => catalog?.hulls ?? [], [catalog]);
  const modules = useMemo(() => catalog?.modules ?? [], [catalog]);
  const classes = useMemo(() => hullClasses(hulls, designs), [designs, hulls]);

  const tabs: TabItem[] = [
    { id: 'design', label: t('shipyard.tabDesign') },
    { id: 'assembly', label: t('shipyard.tabAssembly') },
    { id: 'dismantle', label: t('shipyard.tabDismantle') },
  ];

  const visible = filter === 'all' ? designs : designs.filter((design) => hulls.find((hull) => hull.id === design.hullPartTypeId)?.partGroupCode === filter);

  const save = useCallback(async () => {
    if (!working) {
      return;
    }

    setBusy(true);
    try {
      if (editing) {
        await fleet.saveDesign({
          designId: editing.id,
          hullPartTypeId: working.hull.id,
          hasRaceBonusSlot: working.hasRaceBonusSlot,
          slots: working.slots,
        });
        setNotice({ kind: 'success', text: t('shipyard.saved') });
      } else {
        const created = await fleet.createDesign({
          planetId: planetId ?? 0,
          name: working.name,
          hullPartTypeId: working.hull.id,
          hasRaceBonusSlot: working.hasRaceBonusSlot,
        });

        await fleet.saveDesign({
          designId: created.id,
          hullPartTypeId: working.hull.id,
          hasRaceBonusSlot: working.hasRaceBonusSlot,
          slots: working.slots,
        });

        setNotice({ kind: 'success', text: t('shipyard.created') });
      }

      setWorking(null);
      setEditing(null);
      await Promise.all([reloadCatalog(), reloadDesigns(), reloadShipyard()]);
    } catch (error) {
      setNotice(noticeFromError(error, language, t('shipyard.failed'), reasonPrefixes));
    } finally {
      setBusy(false);
    }
  }, [editing, fleet, language, planetId, reloadCatalog, reloadDesigns, reloadShipyard, t, working]);

  const archive = useCallback(
    async (design: ShipDesign) => {
      setBusy(true);
      try {
        await fleet.archiveDesign(design.id);
        setNotice({ kind: 'success', text: t('shipyard.archived') });
        await Promise.all([reloadDesigns(), reloadShipyard()]);
      } catch (error) {
        setNotice(noticeFromError(error, language, t('shipyard.failed'), reasonPrefixes));
      } finally {
        setBusy(false);
      }
    },
    [fleet, language, reloadDesigns, reloadShipyard, t],
  );

  const openDesign = useCallback((design: ShipDesign) => {
    const hull = hulls.find((entry) => entry.id === design.hullPartTypeId);

    if (!hull) {
      return;
    }

    setEditing(design);
    setWorking({
      hull,
      name: design.name,
      hasRaceBonusSlot: design.hasRaceBonusSlot,
      slots: design.slots.map((slot) => ({ slotIndex: slot.slotIndex, partTypeId: slot.partTypeId, markLevel: slot.markLevel, hasRaceBonus: slot.hasRaceBonus })),
    });
  }, [hulls]);

  const chooseHull = useCallback((hull: ShipHull) => {
    setHullPicker(null);

    if (hullPicker === 'change') {
      // The hull changed, so the slots that do not fit the new hull are cleared, as the server does.
      setWorking((current) => {
        if (!current) {
          return current;
        }

        const available = hull.slots + (hull.raceBonusSlots > 0 ? hull.raceBonusSlots : 0);

        return {
          ...current,
          hull,
          hasRaceBonusSlot: hull.raceBonusSlots > 0,
          slots: current.slots.filter((slot) => slot.slotIndex < available),
        };
      });

      return;
    }

    setEditing(null);
    setWorking({ hull, name: '', hasRaceBonusSlot: hull.raceBonusSlots > 0, slots: [] });
  }, [hullPicker]);

  const pickModule = useCallback(
    (module: ShipModule) => {
      if (picking === null) {
        return;
      }

      setWorking((current) =>
        current
          ? {
              ...current,
              slots: [
                ...current.slots.filter((slot) => slot.slotIndex !== picking),
                { slotIndex: picking, partTypeId: module.id, markLevel: module.markLevels[0] ?? 1, hasRaceBonus: false },
              ].sort((left, right) => left.slotIndex - right.slotIndex),
            }
          : current,
      );

      setPicking(null);
    },
    [picking],
  );

  const removeModule = useCallback((slotIndex: number) => {
    setWorking((current) => (current ? { ...current, slots: current.slots.filter((slot) => slot.slotIndex !== slotIndex) } : current));
  }, []);

  if (catalogState.failed) {
    return (
      <div className="civ-main__in">
        <Alert kind="error">{t('shipyard.loadFailed')}</Alert>
      </div>
    );
  }

  if (!catalog) {
    return (
      <div className="civ-main__in">
        <p className="meta">{t('shipyard.loading')}</p>
      </div>
    );
  }

  return (
    <div className="civ-main__in">
      <ScreenHead
        breadcrumbs={[
          { label: t('civ.nav.overview'), to: routes.game },
          { label: t('civ.nav.shipyard') },
        ]}
        subtitle={t('shipyard.subtitle')}
        title={t('shipyard.title')}
      />

      {notice ? <Alert kind={notice.kind === 'error' ? 'error' : 'info'}>{notice.text}</Alert> : null}

      <Tabs active={tab} label={t('shipyard.tabs')} onChange={setTab} tabs={tabs} />

      {tab === 'design' ? (
        <>
          <div className="sh-overline">
            <p className="sh-overline__counter">
              {t('shipyard.designsCounter', { count: catalog.designCount, limit: catalog.designLimit })}
            </p>
            <button className="btn btn--primary" onClick={() => setHullPicker('new')} type="button">
              {t('shipyard.addDesign')}
            </button>
          </div>

          <div aria-label={t('shipyard.hullFilter')} className="sh-filters" role="group">
            <button aria-pressed={filter === 'all'} className="sh-filter" onClick={() => setFilter('all')} type="button">
              {t('shipyard.classAll')} <span className="sh-filter__count">{designs.length}</span>
            </button>
            {classes.map((entry) => (
              <button
                aria-pressed={filter === entry.code}
                className="sh-filter"
                key={entry.code}
                onClick={() => setFilter(entry.code)}
                type="button"
              >
                {text(entry.nameKey) ?? entry.nameKey} <span className="sh-filter__count">{entry.count}</span>
              </button>
            ))}
          </div>

          {visible.length === 0 ? (
            <div className="sh-empty">
              <p className="sh-empty__text">{t('shipyard.noProjects')}</p>
              <button className="btn btn--primary" onClick={() => setHullPicker('new')} type="button">
                {t('shipyard.addDesign')}
              </button>
            </div>
          ) : (
            <div className="sh-designs">
              {visible.map((design) => (
                <ShipDesignCard
                  busy={busy}
                  costs={costsByDesign.get(design.id) ?? noCosts}
                  design={design}
                  upkeepPerHour={upkeepByDesign.get(design.id) ?? null}
                  hull={hulls.find((hull) => hull.id === design.hullPartTypeId) ?? null}
                  key={design.id}
                  modules={modules}
                  onArchive={(entry) => void archive(entry)}
                  onAssembly={() => setTab('assembly')}
                  onEdit={openDesign}
                />
              ))}
            </div>
          )}

          <div className="sh-overline">
            <button className="btn btn--primary" onClick={() => setHullPicker('new')} type="button">
              {t('shipyard.addDesign')}
            </button>
          </div>
        </>
      ) : planetId ? (
        <ShipyardWork
          failed={shipyardState.failed}
          hulls={hulls}
          mode={tab === 'dismantle' ? 'dismantle' : 'assembly'}
          planetId={planetId}
          receivedAt={shipyardState.receivedAt}
          reload={reloadShipyard}
          yard={shipyardState.data}
        />
      ) : (
        <div className="sh-empty">
          <p className="sh-empty__text">{t(tab === 'assembly' ? 'shipyard.assemblySoon' : 'shipyard.dismantleSoon')}</p>
        </div>
      )}

      <p className="meta">
        <Link to={routes.game}>{t('shipyard.back')}</Link>
      </p>

      {hullPicker ? <HullPicker classes={classes} hulls={hulls} onChoose={chooseHull} onClose={() => setHullPicker(null)} /> : null}

      {working ? (
        <DesignWindow
          busy={busy}
          design={editing}
          hasRaceBonusSlot={working.hasRaceBonusSlot}
          hull={working.hull}
          ammunition={catalog?.ammunition ?? []}
          modules={modules}
          name={working.name}
          onClose={() => {
            setWorking(null);
            setEditing(null);
          }}
          onChangeHull={() => setHullPicker('change')}
          onName={(name) => setWorking((current) => (current ? { ...current, name } : current))}
          onPick={(slotIndex) => setPicking(slotIndex)}
          onRemove={removeModule}
          onSave={() => void save()}
          slots={working.slots}
        />
      ) : null}

      {picking !== null && working ? (
        <ModulePicker
          hull={working.hull}
          moduleGroups={catalog?.moduleGroups ?? []}
          modules={modules}
          onChoose={pickModule}
          onClose={() => setPicking(null)}
          total={working.hull.slots + (working.hasRaceBonusSlot ? working.hull.raceBonusSlots : 0)}
          used={working.slots.length}
        />
      ) : null}
    </div>
  );
}
