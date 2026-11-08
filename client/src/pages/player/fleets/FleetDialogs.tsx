import { useMemo, useState } from 'react';
import type { Fleet, FleetShip } from '../../../api/fleetApi';
import { Alert } from '../../../components/Alert';
import { useI18n } from '../../../i18n/useI18n';
import { PlanetAvatar } from './FleetBits';
import { FleetDialog } from './FleetDialog';
import { groupByClass, groupByPlanet, pickShipIds } from './fleetsParts';
import { ProjectQuantities } from './ProjectLists';
import { useFleetsScene } from './useFleetsScene';

/**
 * Sets the number of ships of one project in a map of quantities.
 */
function withQuantity(current: ReadonlyMap<number, number>, designId: number, quantity: number): ReadonlyMap<number, number> {
  const next = new Map(current);

  next.set(designId, quantity);

  return next;
}

/**
 * The window that forms a fleet (mock-up `dialog-create`): the name, the body the ships stand on (it
 * becomes the home of the fleet) and, for that body, the free ships by class and project with a field for
 * how many ships of each project go into the fleet. A fleet needs a name and at least one ship, so the
 * button waits for both.
 *
 * @param freeShips The ships that are in no fleet.
 * @param busy Whether a command is under way.
 * @param error The refusal of the last attempt, or `null`.
 * @param onCreate Forms the fleet.
 * @param onClose Closes the window.
 */
export function CreateFleetDialog({
  freeShips,
  busy,
  error,
  onCreate,
  onClose,
}: {
  freeShips: readonly FleetShip[];
  busy: boolean;
  error: string | null;
  onCreate: (planetId: number, name: string, shipIds: number[]) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const scene = useFleetsScene();
  const groups = useMemo(() => groupByPlanet(freeShips), [freeShips]);
  const [name, setName] = useState('');
  const [planetId, setPlanetId] = useState<number>(groups[0]?.planetId ?? 0);
  const [quantities, setQuantities] = useState<ReadonlyMap<number, number>>(new Map());
  const ships = groups.find((group) => group.planetId === planetId)?.ships ?? [];
  const shipIds = pickShipIds(groupByClass(ships), quantities);
  const ready = name.trim() !== '' && shipIds.length > 0;

  return (
    <FleetDialog
      actions={
        <>
          <button className="btn fl-btn" onClick={onClose} type="button">
            {t('fleets.cancel')}
          </button>
          <button
            className="btn btn--primary"
            disabled={busy || !ready}
            onClick={() => onCreate(planetId, name.trim(), shipIds)}
            title={ready ? undefined : t('fleets.createHint')}
            type="button"
          >
            {t('fleets.createBtn')}
          </button>
        </>
      }
      onClose={onClose}
      title={t('fleets.createTitle')}
    >
      <div className="fl-field">
        <label className="fl-label" htmlFor="fleet-name">
          {t('fleets.fleetName')}
        </label>
        <input
          autoComplete="off"
          className="fl-input"
          id="fleet-name"
          onChange={(event) => setName(event.target.value)}
          placeholder={t('fleets.fleetNamePlaceholder')}
          value={name}
        />
      </div>
      <fieldset className="fl-field fl-fieldset">
        <legend className="fl-label">{t('fleets.homePlanet')}</legend>
        <div className="fl-picks">
          {groups.map((group) => (
            <label className={group.planetId === planetId ? 'fl-row-pick fl-row-pick--on' : 'fl-row-pick'} key={group.planetId}>
              <input
                checked={group.planetId === planetId}
                className="fl-native"
                name="fleet-home"
                onChange={() => {
                  setPlanetId(group.planetId);
                  setQuantities(new Map());
                }}
                type="radio"
              />
              <PlanetAvatar planetId={group.planetId} size="lg" />
              <span className="fl-row-pick__text fl-row-pick__text--bold">{scene.nameOf(group.planetId)}</span>
              <span className="fl-row-pick__count">{t('fleets.freeCount', { count: group.ships.length })}</span>
            </label>
          ))}
        </div>
      </fieldset>
      <div className="fl-field">
        <span className="fl-label">{t('fleets.pickShipsOn', { name: scene.nameOf(planetId) })}</span>
        {ships.length === 0 ? (
          <p className="fl-note">{t('fleets.noFreeShips')}</p>
        ) : (
          <ProjectQuantities
            onChange={(designId, quantity) => setQuantities((current) => withQuantity(current, designId, quantity))}
            quantities={quantities}
            ships={ships}
          />
        )}
      </div>
      {error ? <Alert kind="error">{error}</Alert> : null}
    </FleetDialog>
  );
}

/**
 * The window that moves ships by number (mock-up `dialog-add-to-fleet`): the ships by class and project
 * with a field for how many ships of each project go. It serves three steps: adding free ships of a body
 * to a fleet, taking ships out of a fleet, and moving ships to another fleet. When the player has to
 * say which fleet gets the ships, the window asks for it; when the fleet is fixed, it does not.
 *
 * @param title The name of the window.
 * @param lead The line under the name, or `null`.
 * @param confirmLabel The text of the button that does the step.
 * @param ships The ships the player picks from.
 * @param fleets The fleets the ships may go to, or `null` when the step names no fleet (taking ships out).
 * @param keepOne Whether the step must leave at least one ship, as taking ships out of a fleet must.
 * @param busy Whether a command is under way.
 * @param error The refusal of the last attempt, or `null`.
 * @param onConfirm Does the step; called with the chosen fleet (`null` when there is none) and the ships.
 * @param onClose Closes the window.
 */
export function ShipQuantityDialog({
  title,
  lead,
  confirmLabel,
  ships,
  fleets,
  keepOne,
  busy,
  error,
  onConfirm,
  onClose,
}: {
  title: string;
  lead?: string | null;
  confirmLabel: string;
  ships: readonly FleetShip[];
  fleets: readonly Fleet[] | null;
  keepOne: boolean;
  busy: boolean;
  error: string | null;
  onConfirm: (fleetId: number | null, shipIds: number[]) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [fleetId, setFleetId] = useState<number | null>(fleets !== null && fleets.length === 1 ? fleets[0].id : null);
  const [quantities, setQuantities] = useState<ReadonlyMap<number, number>>(new Map());
  const shipIds = pickShipIds(groupByClass(ships), quantities);
  const takesAll = keepOne && shipIds.length >= ships.length;
  const needsFleet = fleets !== null && fleetId === null;

  return (
    <FleetDialog
      actions={
        <>
          <button className="btn fl-btn" onClick={onClose} type="button">
            {t('fleets.cancel')}
          </button>
          <button
            className="btn btn--primary"
            disabled={busy || needsFleet || shipIds.length === 0 || takesAll}
            onClick={() => onConfirm(fleetId, shipIds)}
            type="button"
          >
            {confirmLabel}
          </button>
        </>
      }
      lead={lead}
      onClose={onClose}
      title={title}
    >
      {fleets === null ? null : fleets.length === 0 ? (
        <p className="fl-note">{t('fleets.noFleetsHere')}</p>
      ) : fleets.length === 1 ? null : (
        <fieldset className="fl-field fl-fieldset">
          <legend className="fl-label">{t('fleets.chooseFleet')}</legend>
          <div className="fl-picks">
            {fleets.map((fleet) => (
              <label className={fleetId === fleet.id ? 'fl-row-pick fl-row-pick--on' : 'fl-row-pick'} key={fleet.id}>
                <input checked={fleetId === fleet.id} className="fl-native" name="fleet-target" onChange={() => setFleetId(fleet.id)} type="radio" />
                <span className="fl-row-pick__text fl-row-pick__text--bold">{fleet.name}</span>
                <span className="fl-row-pick__count">{t('fleets.shipsCount', { count: fleet.ships.length })}</span>
              </label>
            ))}
          </div>
        </fieldset>
      )}
      {ships.length === 0 ? (
        <p className="fl-note">{t('fleets.noFreeShips')}</p>
      ) : (
        <ProjectQuantities
          onChange={(designId, quantity) => setQuantities((current) => withQuantity(current, designId, quantity))}
          quantities={quantities}
          ships={ships}
        />
      )}
      {takesAll ? <p className="fl-note">{t('fleets.keepOne')}</p> : null}
      {error ? <Alert kind="error">{error}</Alert> : null}
    </FleetDialog>
  );
}

/**
 * A window that asks the player to confirm one step: the disbanding of a fleet, or the offer to
 * disband a fleet whose last ship the player tried to take out (mock-up `dialog-disband`).
 *
 * @param title The question.
 * @param subject The name of the fleet, shown under the question, or `null`.
 * @param body The line under the question.
 * @param confirmLabel The text of the button that confirms.
 * @param busy Whether a command is under way.
 * @param error The refusal of the last attempt, or `null`.
 * @param onConfirm Confirms.
 * @param onClose Closes the window.
 */
export function ConfirmDialog({
  title,
  subject,
  body,
  confirmLabel,
  busy,
  error,
  onConfirm,
  onClose,
}: {
  title: string;
  subject?: string | null;
  body: string;
  confirmLabel: string;
  busy: boolean;
  error: string | null;
  onConfirm: () => void;
  onClose: () => void;
}) {
  const { t } = useI18n();

  return (
    <FleetDialog
      actions={
        <>
          <button className="btn fl-btn" onClick={onClose} type="button">
            {t('fleets.cancel')}
          </button>
          <button className="btn fl-btn--danger-solid" disabled={busy} onClick={onConfirm} type="button">
            {confirmLabel}
          </button>
        </>
      }
      onClose={onClose}
      title={title}
    >
      {subject ? <p className="fl-dialog__subject">{subject}</p> : null}
      <p className="fl-dialog__text">{body}</p>
      {error ? <Alert kind="error">{error}</Alert> : null}
    </FleetDialog>
  );
}
