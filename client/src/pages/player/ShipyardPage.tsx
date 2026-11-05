import { useCallback, useMemo, useState } from 'react';
import type { ShipDesign, ShipDesignSlotInput, ShipHull, ShipModule } from '../../api/fleetApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import type { MessageKey } from '../../i18n/uk';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { useRead } from './market/useRead';
import { ScreenHead } from './ScreenHead';
import { noticeFromError, type Notice } from './screenNotice';
import { useCurrentPlanetId } from './useShell';

/**
 * The prefixes of the refusals of the server that have their own text for the player here.
 */
const reasonPrefixes = ['fleet_'];

/**
 * The screen of the shipyard (task 00038, section 3.3): the hulls of the catalog, the modules
 * that go into them, the slots of the project and the list of the projects of the player.
 *
 * Every rule is the answer of the server: the page shows why a module does not go into the
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

  const [hullId, setHullId] = useState<number | null>(null);
  const [designId, setDesignId] = useState<number | null>(null);
  const [name, setName] = useState('');
  const [hasRaceBonusSlot, setHasRaceBonusSlot] = useState(false);
  const [slots, setSlots] = useState<ShipDesignSlotInput[]>([]);

  const loadCatalog = useCallback(() => fleet.shipCatalog(planetId ?? 0), [fleet, planetId]);
  const loadDesigns = useCallback(() => fleet.designs(planetId ?? undefined), [fleet, planetId]);

  const catalogState = useRead(loadCatalog, Boolean(planetId));
  const designsState = useRead(loadDesigns, Boolean(planetId));

  const reloadCatalog = catalogState.reload;
  const reloadDesigns = designsState.reload;

  const catalog = catalogState.data;
  const designs = useMemo(() => designsState.data ?? [], [designsState.data]);
  const hulls = useMemo(() => catalog?.hulls ?? [], [catalog]);
  const modules = useMemo(() => catalog?.modules ?? [], [catalog]);
  const hull = hulls.find((entry) => entry.id === hullId) ?? null;

  const totalSlots = hull ? hull.slots + (hasRaceBonusSlot && hull.allowsRaceBonusSlot ? 1 : 0) : 0;

  const byId = useMemo(() => new Map(modules.map((module) => [module.id, module])), [modules]);

  const nameOf = useCallback(
    (module: ShipModule) => text(module.nameKey) ?? module.nameKey,
    [text],
  );

  const hullNameOf = useCallback(
    (entry: ShipHull) => text(entry.nameKey) ?? entry.nameKey,
    [text],
  );

  const setSlot = useCallback((index: number, partTypeId: number | null) => {
    setSlots((current) => {
      const next = current.filter((slot) => slot.slotIndex !== index);
      if (partTypeId !== null) {
        next.push({ slotIndex: index, partTypeId, markLevel: 1, hasRaceBonus: false });
      }
      return next.sort((left, right) => left.slotIndex - right.slotIndex);
    });
  }, []);

  const occupied = useMemo(() => new Map(slots.map((slot) => [slot.slotIndex, slot.partTypeId])), [slots]);

  const save = useCallback(async () => {
    if (!hull || !planetId) {
      return;
    }

    setBusy(true);
    try {
      if (designId === null) {
        // The project is created first and the modules are put into it right after, so one
        // click of the player saves the whole design.
        const created = await fleet.createDesign({ planetId, name, hullPartTypeId: hull.id, hasRaceBonusSlot });
        setDesignId(created.id);

        const saved = await fleet.saveDesign({
          designId: created.id,
          hullPartTypeId: hull.id,
          hasRaceBonusSlot,
          slots,
        });

        setSlots(saved.slots.map((slot) => ({ slotIndex: slot.slotIndex, partTypeId: slot.partTypeId, markLevel: slot.markLevel, hasRaceBonus: slot.hasRaceBonus })));
        setNotice({ kind: 'success', text: t('shipyard.created') });
      } else {
        const saved = await fleet.saveDesign({ designId, hullPartTypeId: hull.id, hasRaceBonusSlot, slots });
        setSlots(saved.slots.map((slot) => ({ slotIndex: slot.slotIndex, partTypeId: slot.partTypeId, markLevel: slot.markLevel, hasRaceBonus: slot.hasRaceBonus })));
        setNotice({ kind: 'success', text: t('shipyard.saved') });
      }
      await Promise.all([reloadCatalog(), reloadDesigns()]);
    } catch (error) {
      setNotice(noticeFromError(error, language, t('shipyard.failed'), reasonPrefixes));
    } finally {
      setBusy(false);
    }
  }, [designId, fleet, hasRaceBonusSlot, hull, language, name, planetId, reloadCatalog, reloadDesigns, slots, t]);

  const rename = useCallback(async () => {
    if (designId === null) {
      return;
    }

    setBusy(true);
    try {
      await fleet.renameDesign(designId, name);
      setNotice({ kind: 'success', text: t('shipyard.renamed') });
      await Promise.all([reloadCatalog(), reloadDesigns()]);
    } catch (error) {
      setNotice(noticeFromError(error, language, t('shipyard.failed'), reasonPrefixes));
    } finally {
      setBusy(false);
    }
  }, [designId, fleet, language, name, reloadCatalog, reloadDesigns, t]);

  const archive = useCallback(
    async (id: number) => {
      setBusy(true);
      try {
        await fleet.archiveDesign(id);
        setNotice({ kind: 'success', text: t('shipyard.archived') });
        if (designId === id) {
          setDesignId(null);
          setSlots([]);
        }
        await reloadDesigns();
      } catch (error) {
        setNotice(noticeFromError(error, language, t('shipyard.failed'), reasonPrefixes));
      } finally {
        setBusy(false);
      }
    },
    [designId, fleet, language, reloadDesigns, t],
  );

  const open = useCallback((design: ShipDesign) => {
    setDesignId(design.id);
    setName(design.name);
    setHullId(design.hullPartTypeId);
    setHasRaceBonusSlot(design.hasRaceBonusSlot);
    setSlots(design.slots.map((slot) => ({ slotIndex: slot.slotIndex, partTypeId: slot.partTypeId, markLevel: slot.markLevel, hasRaceBonus: slot.hasRaceBonus })));
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

      {notice ? (
        <Alert kind={notice.kind === 'error' ? 'error' : 'info'}>{notice.text}</Alert>
      ) : null}

      <section aria-labelledby="hulls-title" className="panel">
        <h2 className="panel__title" id="hulls-title">{t('shipyard.hulls')}</h2>
        <div className="grid-cards">
          {hulls.map((entry) => (
            <button
              aria-pressed={entry.id === hullId}
              className="card"
              key={entry.id}
              onClick={() => {
                setHullId(entry.id);
                setDesignId(null);
                setSlots([]);
              }}
              type="button"
            >
              <div className="card__body">
                <h3>{hullNameOf(entry)}</h3>
                <p className="card__text">{t('shipyard.hullSlots', { slots: entry.slots })}</p>
                <p className="card__text">{t('shipyard.hullHold', { capacity: entry.holdCapacity })}</p>
                <p className="card__text">{t('shipyard.hullIntegrity', { integrity: entry.integrity })}</p>
              </div>
            </button>
          ))}
        </div>
      </section>

      {hull ? (
        <section aria-labelledby="slots-title" className="panel">
          <h2 className="panel__title" id="slots-title">{t('shipyard.slots')}</h2>
          <p className="meta">
            {t('shipyard.slotCounter', { used: slots.length, total: totalSlots })}
          </p>
          {hull.allowsRaceBonusSlot ? (
            <label className="field">
              <span className="field__label">{t('shipyard.raceSlot')}</span>
              <input
                checked={hasRaceBonusSlot}
                onChange={(event) => setHasRaceBonusSlot(event.target.checked)}
                type="checkbox"
              />
            </label>
          ) : null}
          <div className="grid-cards">
            {Array.from({ length: totalSlots }, (_, index) => {
              const partTypeId = occupied.get(index) ?? null;
              const module = partTypeId === null ? null : (byId.get(partTypeId) ?? null);

              return (
                <div className="card" key={index}>
                  <div className="card__body">
                    <h3>{t('shipyard.slot', { index: index + 1 })}</h3>
                    <p className="card__text">{module ? nameOf(module) : t('shipyard.slotEmpty')}</p>
                    {module ? (
                      <button className="btn btn--secondary" onClick={() => setSlot(index, null)} type="button">
                        {t('shipyard.remove')}
                      </button>
                    ) : null}
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      ) : null}

      <section aria-labelledby="modules-title" className="panel">
        <h2 className="panel__title" id="modules-title">{t('shipyard.modules')}</h2>
        <div className="grid-cards">
          {modules.map((module) => {
            const reason = moduleReason(module, hull, slots.length, totalSlots);

            return (
              <div className="card" key={module.id}>
                <div className="card__body">
                  <h3>{nameOf(module)}</h3>
                  <p className="card__text">{t('shipyard.moduleEnergy', { energy: module.energyUse, power: module.power })}</p>
                  {reason ? (
                    <p className="card__text">{t(reason)}</p>
                  ) : null}
                  <button
                    className="btn btn--primary"
                    disabled={busy || reason !== null || !hull}
                    onClick={() => setSlot(slots.length, module.id)}
                    type="button"
                  >
                    {t('shipyard.put')}
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </section>

      <section aria-labelledby="project-title" className="panel">
        <h2 className="panel__title" id="project-title">{t('shipyard.project')}</h2>
        <label className="field">
          <span className="field__label">{t('shipyard.projectName')}</span>
          <input maxLength={128} onChange={(event) => setName(event.target.value)} value={name} />
        </label>
        <button className="btn btn--primary" disabled={busy || !hull || name.trim().length === 0} onClick={() => void save()} type="button">
          {t('shipyard.save')}
        </button>
        {designId !== null ? (
          <button className="btn btn--secondary" disabled={busy} onClick={() => void rename()} type="button">
            {t('shipyard.rename')}
          </button>
        ) : null}
      </section>

      <section aria-labelledby="projects-title" className="panel">
        <h2 className="panel__title" id="projects-title">{t('shipyard.projects')}</h2>
        <p className="meta">
          {t('shipyard.projectCounter', { count: designs.length, limit: catalog.designLimit })}
        </p>
        {designs.length === 0 ? (
          <p className="meta">{t('shipyard.noProjects')}</p>
        ) : (
          <div className="grid-cards">
            {designs.map((design) => (
              <div className="card" key={design.id}>
                <div className="card__body">
                  <h3>{design.name}</h3>
                  <p className="card__text">
                    {t('shipyard.projectSlots', { used: design.summary.usedSlots, total: design.summary.totalSlots })}
                  </p>
                  <button className="btn btn--secondary" onClick={() => open(design)} type="button">
                    {t('shipyard.open')}
                  </button>
                  <button className="btn btn--secondary" disabled={busy} onClick={() => void archive(design.id)} type="button">
                    {t('shipyard.archive')}
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
        {catalog.designCount >= catalog.designLimit ? <p className="meta">{t('shipyard.limitReached')}</p> : null}
      </section>

      <p className="meta">
        <Link to={routes.game}>{t('shipyard.back')}</Link>
      </p>
    </div>
  );
}

/**
 * Tells why the module does not go into the hull, or `null` when it does.
 *
 * @param module The module.
 * @param hull The hull of the project, or `null` when the player has not chosen one.
 * @param used The number of the slots that are taken.
 * @param total The number of the slots of the hull.
 */
function moduleReason(module: ShipModule, hull: ShipHull | null, used: number, total: number): MessageKey | null {
  if (!hull) {
    return 'shipyard.reasonNoHull';
  }

  if (used >= total) {
    return 'shipyard.reasonSlotsFull';
  }

  if (module.role !== 'universal' && module.role !== hull.role) {
    return 'shipyard.reasonRole';
  }

  if (module.compatibleHullIds.length > 0 && !module.compatibleHullIds.includes(hull.id)) {
    return 'shipyard.reasonNotCompatible';
  }

  if (module.stock <= 0) {
    return 'shipyard.reasonNoStock';
  }

  return null;
}
