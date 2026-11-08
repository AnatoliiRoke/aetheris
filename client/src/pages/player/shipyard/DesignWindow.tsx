import { useMemo } from 'react';
import type { ShipAmmunition, ShipDesign, ShipDesignSlotInput, ShipHull, ShipModule } from '../../../api/fleetApi';
import { CardArt } from '../../../components/CardArt';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatAmount } from '../planetText';
import { ShipyardDialog } from './ShipyardDialog';
import { factLabel, fittedCombat, fittedFacts, formatCombatDamage, formatFactAmount, formatShield, mainTrait } from './shipyardParts';

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
  onSave: () => void;
  onClose: () => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();

  const byId = useMemo(() => new Map(modules.map((module) => [module.id, module])), [modules]);
  const total = hull.slots + (hasRaceBonusSlot ? hull.raceBonusSlots : 0);

  const energy = useMemo(() => {
    let use = 0;
    let power = 0;
    let crew = 0;
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
      drivePush += module.drivePush;
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
  }, [ammunition, byId, hull.weight, slots]);

  const checks = [
    { ok: energy.hasDrive, text: t('shipyard.checkNoDrive') },
    { ok: energy.hasReactor, text: t('shipyard.checkNoReactor') },
    { ok: energy.use <= energy.power, text: t('shipyard.checkNotEnoughPower', { use: formatAmount(energy.use, language), power: formatAmount(energy.power, language) }) },
  ];

  const ready = checks.every((check) => check.ok);
  const facts = useMemo(() => fittedFacts(slots, byId), [byId, slots]);
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
              <div className="sh-slot sh-slot--filled" key={index}>
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
        </ul>
        {ready ? <p className="sh-note">{t('shipyard.readyToAssembly')}</p> : null}
      </section>
    </ShipyardDialog>
  );
}
