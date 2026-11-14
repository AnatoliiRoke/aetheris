import { useMemo, useState } from 'react';
import type { ShipAmmunition, ShipDesign, ShipDesignSlotInput, ShipHull, ShipModule } from '../../../api/fleetApi';
import { CardArt } from '../../../components/CardArt';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatAmount } from '../planetText';
import { ShipyardDialog } from './ShipyardDialog';
import { factLabel, fittedCombat, fittedFacts, formatCombatDamage, formatFactAmount, formatShield, ftlSlotsWithoutNormalDrive, isNormalDrive, mainTrait } from './shipyardParts';

/**
 * The second step of a design: the window with the slots of the hull, the name of the design,
 * the summary of what the modules give and the checks of the constructor (task 00038, the
 * mock-up of the shipyard).
 *
 * @param design The design being edited, or `null` when it is new.
 * @param hull The hull of the design.
 * @param name The name of the design.
 * @param hasRaceBonusSlot Whether the design takes the extra slot of the race.
 * @param slots The slots of the design as they stand.
 * @param modules The modules of the catalog.
 * @param ammunition The ammunition a kinetic weapon may fire.
 * @param busy Whether a command is running.
 * @param onName What to do when the player changes the name.
 * @param onChangeHull What to do when the player wants another hull.
 * @param onPick What to do when the player picks the module of a slot.
 * @param onRemove What to do when the player takes a module out of a slot.
 * @param onReorder What to do when the player changes the shutdown order. The slots keep their indexes.
 * @param onSave What to do when the player saves the design.
 * @param onClose What to do when the player closes the window.
 */
export function DesignWindow({
  design,
  hull,
  name,
  hasRaceBonusSlot,
  slots,
  modules,
  ammunition = [],
  busy,
  onName,
  onChangeHull,
  onPick,
  onRemove,
  onReorder,
  onSave,
  onClose,
}: {
  design: ShipDesign | null;
  hull: ShipHull;
  name: string;
  hasRaceBonusSlot: boolean;
  slots: ShipDesignSlotInput[];
  modules: ShipModule[];
  ammunition?: ShipAmmunition[];
  busy: boolean;
  onName: (name: string) => void;
  onChangeHull: () => void;
  onPick: (slotIndex: number) => void;
  onRemove: (slotIndex: number) => void;
  onReorder: (slots: ShipDesignSlotInput[]) => void;
  onSave: () => void;
  onClose: () => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const [dragFrom, setDragFrom] = useState<number | null>(null);

  const byId = useMemo(() => new Map(modules.map((module) => [module.id, module])), [modules]);
  const total = hull.slots + (hasRaceBonusSlot ? hull.raceBonusSlots : 0);

  const energy = useMemo(() => {
    let use = 0;
    let power = 0;
    let crew = hull.crew ?? 0;
    let weight = hull.weight;
    let drivePush = 0;
    let shield = 0;
    let hasDrive = false;
    let hasReactor = false;

    for (const slot of slots) {
      const module = byId.get(slot.partTypeId);

      if (!module) {
        continue;
      }

      use += module.energyUse;
      power += module.power;
      crew += module.crew;
      weight += module.weight;
      // The push of a design is the push inside a system: the hyperdrives and the jump drives
      // fly between the systems only (task 00072).
      drivePush += isNormalDrive(module) ? module.drivePush : 0;
      shield += module.shieldCapacity;
      hasDrive ||= module.component === 'drive';
      hasReactor ||= module.component === 'reactor';
    }

    const combat = fittedCombat(slots, byId, ammunition);

    return {
      use,
      power,
      crew,
      weight,
      drivePush,
      shield,
      hasDrive,
      hasReactor,
      cargo: combat.cargo,
      armor: combat.armor,
      shieldRegen: combat.shieldRegen,
      energyDamage: combat.energyDamage,
      kineticMin: combat.kineticMin,
      kineticMax: combat.kineticMax,
    };
  }, [ammunition, byId, hull.crew, hull.weight, slots]);

  const checks = [
    { ok: energy.hasDrive, text: t('shipyard.checkNoDrive') },
    { ok: energy.hasReactor, text: t('shipyard.checkNoReactor') },
    { ok: energy.use <= energy.power, text: t('shipyard.checkNotEnoughPower', { use: formatAmount(energy.use, language), power: formatAmount(energy.power, language) }) },
  ];

  const ready = checks.every((check) => check.ok);

  // A hyperdrive or a jump drive without an ordinary drive does not stop the save (task 00072):
  // the constructor warns and marks the slots of those drives.
  const ftlSlots = useMemo(() => ftlSlotsWithoutNormalDrive(slots, byId), [byId, slots]);
  const facts = useMemo(() => fittedFacts(slots, byId), [byId, slots]);
  const shutdownOrder = useMemo(() => {
    const priorityOf = (slot: ShipDesignSlotInput) => {
      if (typeof slot.powerPriority === 'number') {
        return slot.powerPriority;
      }

      return byId.get(slot.partTypeId)?.defaultPowerPriority ?? 0;
    };

    return [...slots].sort((left, right) => {
      const difference = priorityOf(right) - priorityOf(left);

      return difference !== 0 ? difference : left.slotIndex - right.slotIndex;
    });
  }, [byId, slots]);

  const moveShutdown = (from: number, to: number) => {
    if (from === to || to < 0 || to >= shutdownOrder.length) {
      return;
    }

    const next = [...shutdownOrder];
    const [moved] = next.splice(from, 1);
    next.splice(to, 0, moved);
    const last = next.length - 1;
    onReorder(next.map((slot, index) => ({ ...slot, powerPriority: last - index })));
  };
  const damage = formatCombatDamage(energy.energyDamage, energy.kineticMin, energy.kineticMax, t, language);

  return (
    <ShipyardDialog
      actions={
        <>
          <button className="btn btn--secondary" disabled={busy} onClick={onClose} type="button">
            {t('shipyard.cancel')}
          </button>
          <button className="btn btn--primary" disabled={busy || name.trim().length === 0} onClick={onSave} type="button">
            {t('shipyard.saveDesign')}
          </button>
        </>
      }
      onClose={onClose}
      title={design ? t('shipyard.editDesign') : t('shipyard.newDesign')}
    >
      <div className="sh-window__head">
        <CardArt imageKey={hull.imageKey} label={text(hull.nameKey) ?? hull.nameKey} />
        <div className="sh-window__hull">
          <p className="sh-window__hull-name">{text(hull.nameKey) ?? hull.nameKey}</p>
          <p className="sh-window__hull-class">{text(hull.partGroupNameKey) ?? hull.partGroupNameKey}</p>
        </div>
        <dl className="sh-facts sh-facts--tight">
          <div>
            <dt className="sh-facts__term">{t('shipyard.slots')}</dt>
            <dd className="sh-facts__value">{total}</dd>
          </div>
          <div>
            <dt className="sh-facts__term">{t('shipyard.hold')}</dt>
            <dd className="sh-facts__value">{formatAmount(hull.holdCapacity, language)} т</dd>
          </div>
          <div>
            <dt className="sh-facts__term">{t('shipyard.integrity')}</dt>
            <dd className="sh-facts__value">{formatAmount(hull.integrity, language)}</dd>
          </div>
        </dl>
        <button className="btn btn--secondary" onClick={onChangeHull} type="button">
          {t('shipyard.changeHull')}
        </button>
      </div>

      <label className="field">
        <span className="field__label">{t('shipyard.designName')}</span>
        <input maxLength={128} onChange={(event) => onName(event.target.value)} value={name} />
      </label>

      <section aria-labelledby="sh-slots-title">
        <div className="sh-overline">
          <span className="civ-label" id="sh-slots-title">
            {t('shipyard.slotsOfShip')}
          </span>
          <p className="sh-overline__counter">
            {t('shipyard.slotsCounter', { used: slots.length, total })}
          </p>
        </div>

        <div className="sh-slots">
          {Array.from({ length: total }, (_, index) => {
            const slot = slots.find((entry) => entry.slotIndex === index);
            const module = slot ? byId.get(slot.partTypeId) : undefined;
            const trait = module ? mainTrait(module) : null;

            if (!module || !slot) {
              return (
                <button className="sh-slot" key={index} onClick={() => onPick(index)} type="button">
                  <span className="sh-slot__index">{t('shipyard.slot', { index: index + 1 })}</span>
                  <span className="sh-slot__plus">+</span>
                </button>
              );
            }

            return (
              <div className={ftlSlots.includes(index) ? 'sh-slot sh-slot--filled sh-slot--warn' : 'sh-slot sh-slot--filled'} key={index}>
                <span className="sh-slot__index">{t('shipyard.slot', { index: index + 1 })}</span>
                <span className="sh-slot__name">{text(module.nameKey) ?? module.nameKey}</span>
                {slot.markLevel > 1 ? <span className="sh-slot__mark">{t('shipyard.mark', { mark: slot.markLevel })}</span> : null}
                {trait ? <span className="sh-slot__fact">{t(`shipyard.trait.${trait.label}` as never)}: {trait.value}</span> : null}
                <button className="sh-slot__remove" onClick={() => onRemove(index)} type="button">
                  {t('shipyard.remove')}
                </button>
              </div>
            );
          })}
        </div>

        <p className="sh-note">{t('shipyard.dronesNote')}</p>

        <div className="sh-overline">
          <span className="civ-label" id="sh-power-title">
            {t('shipyard.powerOrder')}
          </span>
        </div>
        <p className="sh-note">{t('shipyard.powerOrderNote')}</p>
        {shutdownOrder.length === 0 ? (
          <p className="sh-note">{t('shipyard.powerOrderEmpty')}</p>
        ) : (
          <ol aria-labelledby="sh-power-title" className="sh-order">
            {shutdownOrder.map((slot, index) => {
              const module = byId.get(slot.partTypeId);

              return (
                <li
                  className="sh-order__row"
                  draggable
                  key={slot.slotIndex}
                  onDragOver={(event) => event.preventDefault()}
                  onDragStart={() => setDragFrom(index)}
                  onDrop={() => {
                    if (dragFrom !== null) {
                      moveShutdown(dragFrom, index);
                    }

                    setDragFrom(null);
                  }}
                >
                  <p className="sh-order__name">{module ? text(module.nameKey) ?? module.nameKey : String(slot.partTypeId)}</p>
                  <p className="sh-order__place">{t('shipyard.powerOrderPlace', { place: index + 1 })}</p>
                  <div className="sh-order__move">
                    <button className="btn btn--secondary" disabled={index === 0} onClick={() => moveShutdown(index, index - 1)} type="button">
                      {t('shipyard.powerUp')}
                    </button>
                    <button
                      className="btn btn--secondary"
                      disabled={index === shutdownOrder.length - 1}
                      onClick={() => moveShutdown(index, index + 1)}
                      type="button"
                    >
                      {t('shipyard.powerDown')}
                    </button>
                  </div>
                </li>
              );
            })}
          </ol>
        )}
      </section>

      <section aria-labelledby="sh-summary-title">
        <span className="civ-label" id="sh-summary-title">
          {t('shipyard.designSummary')}
        </span>
        <dl className="sh-facts">
          <div>
            <dt className="sh-facts__term">{t('shipyard.mass')}</dt>
            <dd className="sh-facts__value">{formatAmount(energy.weight, language)} т</dd>
          </div>
          <div>
            <dt className="sh-facts__term">{t('shipyard.slotsUsed')}</dt>
            <dd className="sh-facts__value">
              {slots.length} {t('shipyard.of')} {total}
            </dd>
          </div>
          <div>
            <dt className="sh-facts__term">{t('shipyard.crew')}</dt>
            <dd className="sh-facts__value">{formatAmount(energy.crew, language)}</dd>
          </div>
          <div>
            <dt className="sh-facts__term">{t('shipyard.drivePush')}</dt>
            <dd className="sh-facts__value">{formatAmount(energy.drivePush, language)} кН</dd>
          </div>
          {damage ? (
            <div>
              <dt className="sh-facts__term">{t('shipyard.damage')}</dt>
              <dd className="sh-facts__value">{damage}</dd>
            </div>
          ) : null}
          <div>
            <dt className="sh-facts__term">{t('shipyard.integrity')}</dt>
            <dd className="sh-facts__value">{formatAmount(hull.integrity, language)}</dd>
          </div>
          {energy.armor > 0 ? (
            <div>
              <dt className="sh-facts__term">{t('shipyard.armor')}</dt>
              <dd className="sh-facts__value">{formatAmount(energy.armor, language)}</dd>
            </div>
          ) : null}
          {energy.shield > 0 || energy.shieldRegen > 0 ? (
            <div>
              <dt className="sh-facts__term">{t('shipyard.shield')}</dt>
              <dd className="sh-facts__value">{formatShield(energy.shield, energy.shieldRegen, t, language)}</dd>
            </div>
          ) : null}
          <div>
            <dt className="sh-facts__term">{t('shipyard.energy')}</dt>
            <dd className="sh-facts__value">
              {formatAmount(energy.use, language)} / {formatAmount(energy.power, language)} МВт
            </dd>
          </div>
          <div>
            <dt className="sh-facts__term">{t('shipyard.hold')}</dt>
            <dd className="sh-facts__value">{formatAmount(hull.holdCapacity + energy.cargo, language)} т</dd>
          </div>
          {facts.map((fact) => (
            <div key={fact.kind}>
              <dt className="sh-facts__term">{factLabel(fact, t, text)}</dt>
              <dd className="sh-facts__value">{formatFactAmount(fact, text, language)}</dd>
            </div>
          ))}
        </dl>
      </section>

      <section aria-labelledby="sh-check-title">
        <span className="civ-label" id="sh-check-title">
          {t('shipyard.check')}
        </span>
        <ul className="sh-check">
          {checks.map((check) => (
            <li className={check.ok ? 'sh-check__item sh-check__item--ok' : 'sh-check__item sh-check__item--warn'} key={check.text}>
              <span aria-hidden="true">{check.ok ? '✓' : '!'}</span>
              <span>{check.text}</span>
            </li>
          ))}
          {ftlSlots.length > 0 ? (
            <li className="sh-check__item sh-check__item--warn">
              <span aria-hidden="true">!</span>
              <span>{t('shipyard.ftlNeedsNormalDrive')}</span>
            </li>
          ) : null}
        </ul>
        {ready ? <p className="sh-note">{t('shipyard.readyToAssembly')}</p> : null}
      </section>
    </ShipyardDialog>
  );
}
