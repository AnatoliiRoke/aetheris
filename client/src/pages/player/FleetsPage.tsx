import { useCallback, useMemo, useState } from 'react';
import type { Fleet, FleetsView } from '../../api/fleetApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { GameIcon } from '../../components/GameIcon';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRead } from './market/useRead';
import { ScreenHead } from './ScreenHead';
import { noticeFromError, type Notice } from './screenNotice';
import { FleetCard } from './fleets/FleetCard';
import { ConfirmDialog, CreateFleetDialog, ShipQuantityDialog } from './fleets/FleetDialogs';
import { FleetPanel } from './fleets/FleetPanel';
import { FreeShipsPanel } from './fleets/FreeShipsPanel';
import { fleetsOn, passesState, type StateFilter } from './fleets/fleetsParts';
import { FleetsSceneProvider } from './fleets/fleetsScene';
import { useFleetsScene } from './fleets/useFleetsScene';

/**
 * The prefixes of the refusals of the server that have their own text for the player here.
 */
const reasonPrefixes = ['fleet_'];

/**
 * The window that is open on the screen of the fleets, if one is.
 */
type Dialog =
  | { kind: 'create' }
  | { kind: 'disband'; fleetId: number }
  | { kind: 'add'; planetId: number; fleetId: number | null }
  | { kind: 'remove'; fleetId: number }
  | { kind: 'move'; fleetId: number };

/**
 * The screen of the fleets (task 00040, mock-up `documentation/design/AETHERIS_fleets`): the list of
 * the fleets of the player with the limit that the spaceports give, the panel of the chosen fleet, the
 * ships that are in no fleet, and the windows that form a fleet, add ships, move a ship and disband.
 *
 * Every rule is the answer of the server: the screen shows why a command is refused and never decides
 * a rule of its own. Every command answers with the whole list, so the screen does not ask again.
 */
export function FleetsPage() {
  const { t, language } = useI18n();
  const { fleet: fleetApi } = useApi();

  useDocumentTitle(t('meta.fleets.title'));

  const load = useCallback(() => fleetApi.fleets(), [fleetApi]);
  const read = useRead(load);
  const [fresh, setFresh] = useState<{ view: FleetsView; at: number } | null>(null);
  const [notice, setNotice] = useState<Notice>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<Dialog | null>(null);

  // An answer of a command is newer than the last read until a read arrives after it.
  const view: FleetsView | null = fresh && (read.receivedAt === null || fresh.at > read.receivedAt) ? fresh.view : read.data;

  const run = useCallback(
    async (command: () => Promise<FleetsView>, done: string): Promise<boolean> => {
      setBusy(true);
      try {
        const answer = await command();

        setFresh({ view: answer, at: Date.now() });
        setNotice({ kind: 'success', text: done });
        setDialogError(null);
        setDialog(null);

        return true;
      } catch (error) {
        const refusal = noticeFromError(error, language, t('fleets.failed'), reasonPrefixes);

        if (dialog) {
          setDialogError(refusal?.text ?? null);
        } else {
          setNotice(refusal);
        }

        return false;
      } finally {
        setBusy(false);
      }
    },
    [dialog, language, t],
  );

  const openDialog = useCallback((next: Dialog | null) => {
    setDialogError(null);
    setDialog(next);
  }, []);

  if (read.failed && !view) {
    return (
      <div className="civ-main__in">
        <Alert kind="error">{t('fleets.loadFailed')}</Alert>
      </div>
    );
  }

  if (!view) {
    return (
      <div className="civ-main__in">
        <p className="meta">{t('fleets.loading')}</p>
      </div>
    );
  }

  return (
    <FleetsSceneProvider view={view}>
      <FleetsContent
        busy={busy}
        dialog={dialog}
        dialogError={dialogError}
        notice={notice}
        onDialog={openDialog}
        run={run}
        view={view}
      />
    </FleetsSceneProvider>
  );
}

/**
 * The content of the screen once the fleets have arrived.
 *
 * @param view The fleets of the player.
 * @param busy Whether a command is under way.
 * @param dialog The window that is open, or `null`.
 * @param dialogError The refusal shown in the window, or `null`.
 * @param notice The message above the content.
 * @param onDialog Opens a window or closes the open one.
 * @param run Runs a command and keeps its answer.
 */
function FleetsContent({
  view,
  busy,
  dialog,
  dialogError,
  notice,
  onDialog,
  run,
}: {
  view: FleetsView;
  busy: boolean;
  dialog: Dialog | null;
  dialogError: string | null;
  notice: Notice;
  onDialog: (dialog: Dialog | null) => void;
  run: (command: () => Promise<FleetsView>, done: string) => Promise<boolean>;
}) {
  const { t, language } = useI18n();
  const { fleet: fleetApi } = useApi();
  const scene = useFleetsScene();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [planetFilter, setPlanetFilter] = useState<number | null>(null);
  const [stateFilter, setStateFilter] = useState<StateFilter>('all');
  const [query, setQuery] = useState('');

  const visible = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase(language);

    return view.fleets.filter(
      (fleet) =>
        (planetFilter === null || fleet.homePlanetId === planetFilter) &&
        passesState(stateFilter, view.hasCredits) &&
        (needle === '' || fleet.name.toLocaleLowerCase(language).includes(needle)),
    );
  }, [language, planetFilter, query, stateFilter, view]);

  const planetsWithFleet = useMemo(() => new Set(view.fleets.map((fleet) => fleet.planetId)), [view]);
  const selected: Fleet | null = view.fleets.find((fleet) => fleet.id === selectedId) ?? visible[0] ?? null;
  const limitReached = view.used >= view.limit;
  const createBlocked = limitReached || view.freeShips.length === 0;
  const dialogFleet = dialog && 'fleetId' in dialog ? (view.fleets.find((fleet) => fleet.id === dialog.fleetId) ?? null) : null;
  const noShipsAtAll = view.fleets.length === 0 && view.freeShips.length === 0;

  return (
    <div className="civ-main__in">
      <ScreenHead
        actions={
          <div className="fl-head-actions">
            <button
              className="btn btn--primary"
              disabled={createBlocked}
              onClick={() => onDialog({ kind: 'create' })}
              title={limitReached ? t('fleets.limitReason') : view.freeShips.length === 0 ? t('fleets.noFree') : undefined}
              type="button"
            >
              <GameIcon name="mapPlus" size={16} />
              {t('fleets.create')}
            </button>
            {limitReached ? <span className="fl-note">{t('fleets.limitReason')}</span> : null}
          </div>
        }
        breadcrumbs={[
          { label: t('civ.nav.overview'), to: routes.game },
          { label: t('civ.nav.fleets') },
        ]}
        subtitle={t('fleets.count', { used: view.used, limit: view.limit })}
        title={t('fleets.title')}
      />

      {notice ? <Alert kind={notice.kind === 'error' ? 'error' : 'info'}>{notice.text}</Alert> : null}

      {noShipsAtAll ? (
        <div className="fl-empty">
          <svg aria-hidden="true" className="fl-empty__art" viewBox="0 0 160 92">
            <rect fill="none" height="90" rx="12" stroke="currentColor" strokeDasharray="4 4" width="158" x="1" y="1" />
            <path d="M28 46 L58 30 H104 L134 46 L104 62 H58 z M76 46h28" fill="none" stroke="var(--accent-hover)" strokeLinejoin="round" strokeWidth="2" />
          </svg>
          <h2 className="fl-empty__title">{t('fleets.noShipsTitle')}</h2>
          <p className="fl-empty__text">{t('fleets.noShipsText')}</p>
          <Link className="btn btn--primary" to={routes.shipyard}>
            {t('fleets.toShipyard')}
          </Link>
        </div>
      ) : (
        <>
          <div className="fl-toolbar">
            {scene.bodies.length > 1 ? (
              <div aria-label={t('fleets.planetFilter')} className="fl-filters" role="group">
                <button aria-pressed={planetFilter === null} className="fl-filter" onClick={() => setPlanetFilter(null)} type="button">
                  {t('fleets.allPlanets')}
                </button>
                {scene.bodies.map((body) => (
                  <button
                    aria-pressed={planetFilter === body.planetId}
                    className="fl-filter"
                    key={body.planetId}
                    onClick={() => setPlanetFilter(body.planetId)}
                    type="button"
                  >
                    {scene.nameOf(body.planetId)}
                  </button>
                ))}
              </div>
            ) : null}
            {view.fleets.length > 0 ? (
              <>
                <div aria-label={t('fleets.stateFilter')} className="fl-filters" role="group">
                  <button aria-pressed={stateFilter === 'all'} className="fl-filter" onClick={() => setStateFilter('all')} type="button">
                    {t('fleets.allStates')}
                  </button>
                  <button aria-pressed={stateFilter === 'ready'} className="fl-filter" onClick={() => setStateFilter('ready')} type="button">
                    {t('fleets.stateReadyFilter')}
                  </button>
                  <button className="fl-filter" disabled title={t('fleets.ordersSoon')} type="button">
                    {t('fleets.stateInFlightFilter')}
                  </button>
                  <button aria-pressed={stateFilter === 'noCredits'} className="fl-filter" onClick={() => setStateFilter('noCredits')} type="button">
                    {t('fleets.stateNoCreditsFilter')}
                  </button>
                </div>
                <div className="fl-search">
                  <GameIcon name="search" size={18} />
                  <input
                    aria-label={t('fleets.search')}
                    className="fl-search__input"
                    onChange={(event) => setQuery(event.target.value)}
                    placeholder={t('fleets.search')}
                    type="search"
                    value={query}
                  />
                </div>
              </>
            ) : null}
          </div>

          <div className="fl-layout">
            <div className="fl-list">
              {view.fleets.length === 0 ? (
                <div className="fl-empty">
                  <h2 className="fl-empty__title">{t('fleets.noFleetsTitle')}</h2>
                  <p className="fl-empty__text">{t('fleets.noFleetsText')}</p>
                  <button className="btn btn--primary" disabled={createBlocked} onClick={() => onDialog({ kind: 'create' })} type="button">
                    {t('fleets.createFirst')}
                  </button>
                </div>
              ) : visible.length === 0 ? (
                <p className="fl-note">{t('fleets.noMatch')}</p>
              ) : (
                <div aria-label={t('fleets.list')} className="fl-cards" role="group">
                  {visible.map((fleet) => (
                    <FleetCard fleet={fleet} hasCredits={view.hasCredits} key={fleet.id} onSelect={setSelectedId} selected={selected?.id === fleet.id} />
                  ))}
                </div>
              )}

              <FreeShipsPanel
                busy={busy}
                onAdd={(planetId) => onDialog({ kind: 'add', planetId, fleetId: null })}
                planetsWithFleet={planetsWithFleet}
                ships={view.freeShips}
              />
            </div>

            {selected ? (
              <FleetPanel
                busy={busy}
                canMove={fleetsOn(view, selected.planetId, selected.id).length > 0}
                fleet={selected}
                hasCredits={view.hasCredits}
                key={selected.id}
                onAdd={() => onDialog({ kind: 'add', planetId: selected.planetId, fleetId: selected.id })}
                onDisband={() => onDialog({ kind: 'disband', fleetId: selected.id })}
                onMove={() => onDialog({ kind: 'move', fleetId: selected.id })}
                onRemove={() => onDialog({ kind: 'remove', fleetId: selected.id })}
                onRename={(name) => run(() => fleetApi.renameFleet({ fleetId: selected.id, name }), t('fleets.renamed'))}
              />
            ) : null}
          </div>
        </>
      )}

      <p className="meta">
        <Link to={routes.game}>{t('fleets.back')}</Link>
      </p>

      {dialog?.kind === 'create' ? (
        <CreateFleetDialog
          busy={busy}
          error={dialogError}
          freeShips={view.freeShips}
          onClose={() => onDialog(null)}
          onCreate={(planetId, name, shipIds) => void run(() => fleetApi.createFleet({ planetId, name, shipIds }), t('fleets.created'))}
        />
      ) : null}

      {dialog?.kind === 'disband' && dialogFleet ? (
        <ConfirmDialog
          body={t('fleets.disbandText')}
          busy={busy}
          confirmLabel={t('fleets.disband')}
          error={dialogError}
          onClose={() => onDialog(null)}
          onConfirm={() => void run(() => fleetApi.dissolveFleet(dialogFleet.id), t('fleets.disbanded'))}
          subject={dialogFleet.name}
          title={t('fleets.disbandTitle')}
        />
      ) : null}

      {dialog?.kind === 'add' ? (
        <ShipQuantityDialog
          busy={busy}
          confirmLabel={t('fleets.addBtn')}
          error={dialogError}
          fleets={dialog.fleetId === null ? fleetsOn(view, dialog.planetId, null) : view.fleets.filter((fleet) => fleet.id === dialog.fleetId)}
          keepOne={false}
          lead={t('fleets.freeNote')}
          onClose={() => onDialog(null)}
          onConfirm={(fleetId, shipIds) => fleetId !== null && void run(() => fleetApi.addFleetShips({ fleetId, shipIds }), t('fleets.added'))}
          ships={view.freeShips.filter((ship) => ship.planetId === dialog.planetId)}
          title={dialog.fleetId === null ? t('fleets.addTitle') : `${t('fleets.addTitle')}: ${view.fleets.find((fleet) => fleet.id === dialog.fleetId)?.name ?? ''}`}
        />
      ) : null}

      {dialog?.kind === 'remove' && dialogFleet ? (
        <ShipQuantityDialog
          busy={busy}
          confirmLabel={t('fleets.removeBtn')}
          error={dialogError}
          fleets={null}
          keepOne
          onClose={() => onDialog(null)}
          onConfirm={(_, shipIds) => void run(() => fleetApi.removeFleetShips({ fleetId: dialogFleet.id, shipIds }), t('fleets.removed'))}
          ships={dialogFleet.ships}
          title={`${t('fleets.removeTitle')}: ${dialogFleet.name}`}
        />
      ) : null}

      {dialog?.kind === 'move' && dialogFleet ? (
        <ShipQuantityDialog
          busy={busy}
          confirmLabel={t('fleets.moveBtn')}
          error={dialogError}
          fleets={fleetsOn(view, dialogFleet.planetId, dialogFleet.id)}
          keepOne
          lead={t('fleets.pickFleetText')}
          onClose={() => onDialog(null)}
          onConfirm={(targetFleetId, shipIds) =>
            targetFleetId !== null && void run(() => fleetApi.transferFleetShips({ fleetId: dialogFleet.id, targetFleetId, shipIds }), t('fleets.moved'))
          }
          ships={dialogFleet.ships}
          title={`${t('fleets.moveTitle')}: ${dialogFleet.name}`}
        />
      ) : null}
    </div>
  );
}
