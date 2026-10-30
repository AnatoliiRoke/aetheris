import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../api/http';
import type { TerraformingDevice, TerraformingShip, TerraformingState, TerraformingTarget } from '../../api/processesApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { formatNumber, lookup } from './planetText';

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['processes_', 'planets_'];

/**
 * One device in the hold of one ship, as the player picks it.
 */
interface HeldDevice {
  key: string;
  ship: TerraformingShip;
  device: TerraformingDevice;
}

/**
 * The terraforming (task 00014, sections 9 and 11): a ship turns a one-shot device from its own
 * hold on by a free body that may be settled. It is no process of the queue of a building, so it
 * is an action of its own: the player picks the free body, then the ship and the device in its
 * hold, and confirms the start.
 *
 * The free bodies, the ships by them, the devices in their holds, the range of the multiplier and
 * the reasons come from the server; the check of the choice is only a hint, the server decides.
 */
export function TerraformingPanel() {
  const { t, language } = useI18n();
  const { processes } = useApi();
  const { text } = useTexts();
  const { state, failed, apply, reload } = useTerraforming();
  const [targetId, setTargetId] = useState('');
  const [heldKey, setHeldKey] = useState('');
  const [confirming, setConfirming] = useState(false);
  const [notice, setNotice] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  if (!state) {
    return (
      <section className="panel" aria-labelledby="terraforming-title">
        <h2 className="panel__title" id="terraforming-title">
          {t('terraforming.title')}
        </h2>
        {failed ? (
          <Alert kind="error">{t('terraforming.loadFailed')}</Alert>
        ) : (
          <p className="lead" role="status">
            {t('terraforming.loading')}
          </p>
        )}
      </section>
    );
  }

  const deviceName = (device: TerraformingDevice) => text(device.nameKey) ?? device.nameKey;
  const target = state.targets.find((entry) => String(entry.planetId) === targetId) ?? null;

  // The devices the ships by the chosen body carry in their holds, one entry for every device
  // of every ship.
  const held: HeldDevice[] = (target?.shipIds ?? []).flatMap((ship) =>
    ship.devices.map((device) => ({ key: `${ship.shipId}:${device.partTypeId}`, ship, device })),
  );
  const chosen = held.find((entry) => entry.key === heldKey) ?? null;

  // The hint of the rules: the server checks them again when the device is turned on.
  const reasons: string[] = [];

  if (state.targets.length === 0) {
    reasons.push(t('terraforming.noTargets'));
  } else if (target?.isTerraformed) {
    reasons.push(lookup(language, 'reason.processes_target_terraformed', 'processes_target_terraformed'));
  } else if (target !== null && held.length === 0) {
    reasons.push(t('terraforming.noDevices'));
  }

  const canStart = chosen !== null && target !== null && reasons.length === 0;
  const reasonsId = 'terraforming-reasons';

  const start = async (chosenTarget: TerraformingTarget, chosenHeld: HeldDevice) => {
    setBusy(true);
    setNotice(null);

    try {
      const result = await processes.terraform(chosenTarget.planetId, chosenHeld.ship.shipId, chosenHeld.device.partTypeId);
      apply(result.terraforming);
      setTargetId('');
      setHeldKey('');

      const coefficients = result.depositIds.map((deposit) => deposit.newCoefficient);
      setNotice({
        kind: 'success',
        text:
          coefficients.length === 0
            ? t('terraforming.doneNone', { target: chosenTarget.name })
            : t('terraforming.done', {
                target: chosenTarget.name,
                count: formatNumber(coefficients.length, language),
                min: formatNumber(Math.min(...coefficients), language),
                max: formatNumber(Math.max(...coefficients), language),
              }),
      });
    } catch (error) {
      const code = error instanceof ApiError ? error.code : 'internal_error';
      setNotice({
        kind: 'error',
        text: reasonPrefixes.some((prefix) => code.startsWith(prefix)) ? lookup(language, `reason.${code}`, code) : t('error.generic.text'),
      });
      await reload();
    } finally {
      setConfirming(false);
      setBusy(false);
    }
  };

  return (
    <section className="panel" aria-labelledby="terraforming-title">
      <h2 className="panel__title" id="terraforming-title">
        {t('terraforming.title')}
      </h2>
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      <p className="lead">{t('terraforming.rules')}</p>
      <p className="lead num">
        {t('terraforming.range', {
          min: formatNumber(state.multiplierMin, language),
          max: formatNumber(state.multiplierMax, language),
        })}
      </p>
      <div className="building-card__next">
        <label className="field">
          <span className="field__label">{t('terraforming.target')}</span>
          <select
            className="input"
            value={targetId}
            disabled={busy || confirming || state.targets.length === 0}
            onChange={(event) => {
              setTargetId(event.target.value);
              setHeldKey('');
            }}
          >
            <option value="">{state.targets.length === 0 ? t('terraforming.noTargets') : t('terraforming.chooseTarget')}</option>
            {state.targets.map((entry) => (
              <option key={entry.planetId} value={entry.planetId} disabled={entry.isTerraformed}>
                {entry.isTerraformed
                  ? t('terraforming.targetTerraformed', { name: entry.name })
                  : entry.orbit === null
                    ? entry.name
                    : t('terraforming.targetChoice', { name: entry.name, orbit: entry.orbit })}
              </option>
            ))}
          </select>
        </label>
        <label className="field">
          <span className="field__label">{t('terraforming.device')}</span>
          <select
            className="input"
            value={heldKey}
            disabled={busy || confirming || target === null || held.length === 0}
            onChange={(event) => setHeldKey(event.target.value)}
          >
            <option value="">
              {target === null ? t('terraforming.chooseTargetFirst') : held.length === 0 ? t('terraforming.noDevices') : t('terraforming.chooseDevice')}
            </option>
            {held.map((entry) => (
              <option key={entry.key} value={entry.key}>
                {t('terraforming.deviceChoice', {
                  ship: entry.ship.shipId,
                  name: deviceName(entry.device),
                  quantity: formatNumber(entry.device.quantity, language),
                })}
              </option>
            ))}
          </select>
        </label>
        {confirming && chosen !== null && target !== null ? (
          <>
            <Alert kind="info" title={t('terraforming.confirmTitle')}>
              {t('terraforming.confirmText', { device: deviceName(chosen.device), ship: chosen.ship.shipId, target: target.name })}
            </Alert>
            <button className="btn btn--primary" type="button" disabled={busy} aria-busy={busy} onClick={() => void start(target, chosen)}>
              {t('terraforming.confirm')}
            </button>
            <button className="btn btn--secondary" type="button" disabled={busy} onClick={() => setConfirming(false)}>
              {t('terraforming.back')}
            </button>
          </>
        ) : (
          <button
            className="btn btn--secondary"
            type="button"
            disabled={busy || !canStart}
            title={reasons.length > 0 ? reasons.join('. ') : undefined}
            aria-describedby={reasons.length > 0 ? reasonsId : undefined}
            onClick={() => setConfirming(true)}
          >
            {t('terraforming.start')}
          </button>
        )}
      </div>
      {reasons.length > 0 ? (
        <ul className="reason-list" id={reasonsId}>
          {reasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

/**
 * Reads the terraforming of the player and takes the answer of the command.
 */
function useTerraforming(): {
  state: TerraformingState | null;
  failed: boolean;
  apply: (next: TerraformingState) => void;
  reload: () => Promise<void>;
} {
  const { processes } = useApi();
  const [state, setState] = useState<TerraformingState | null>(null);
  const [failed, setFailed] = useState(false);
  const active = useRef(true);

  const apply = useCallback((next: TerraformingState) => {
    if (active.current) {
      setState(next);
      setFailed(false);
    }
  }, []);

  const reload = useCallback(async () => {
    try {
      apply(await processes.terraforming());
    } catch {
      if (active.current) {
        setFailed(true);
      }
    }
  }, [processes, apply]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  return { state, failed, apply, reload };
}
