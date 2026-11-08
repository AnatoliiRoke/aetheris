import type { ShipAmmunition, ShipDesign, ShipDesignFact, ShipDesignSummary, ShipHull, ShipModule, ShipModuleFact } from '../../../api/fleetApi';
import type { EntityText } from '../../../texts/context';
import type { Language, MessageParams } from '../../../i18n/context';
import type { MessageKey } from '../../../i18n/uk';
import { formatAmount } from '../planetText';

/**
 * A class of hull: the group of the hull, with the name of the group and how many designs of
 * the player use it (task 00038, the filter of the design screen).
 */
export interface HullClass {
  /** The neutral code of the group of the hulls. */
  code: string;
  /** The localisation key of the name of the group. */
  nameKey: string;
  /** How many designs of the player use the hulls of the group. */
  count: number;
}

/**
 * A module of a project with the number of the slots it takes (the card of a design).
 */
export interface InstalledModule {
  module: ShipModule;
  /** The mark level of the module of the first slot. */
  markLevel: number;
  /** How many slots the module takes. */
  count: number;
}

/**
 * The reason why a module does not go into the hull, or `null` when it does.
 */
export type ModuleReason = 'noHull' | 'slotsFull';

/**
 * Groups the classes of the hulls of the catalog with the number of the designs of every class.
 *
 * @param hulls The hulls of the catalog.
 * @param designs The designs of the player.
 * @returns The classes, in the order of the hulls.
 */
export function hullClasses(hulls: ShipHull[], designs: ShipDesign[]): HullClass[] {
  const classes: HullClass[] = [];
  const byCode = new Map<string, HullClass>();

  for (const hull of hulls) {
    if (byCode.has(hull.partGroupCode)) {
      continue;
    }

    const entry: HullClass = { code: hull.partGroupCode, nameKey: hull.partGroupNameKey, count: 0 };
    byCode.set(hull.partGroupCode, entry);
    classes.push(entry);
  }

  for (const design of designs) {
    const hull = hulls.find((entry) => entry.id === design.hullPartTypeId);

    if (!hull) {
      continue;
    }

    const entry = byCode.get(hull.partGroupCode);

    if (entry) {
      entry.count += 1;
    }
  }

  return classes;
}

/**
 * Groups the modules of the slots of a project by the module, so the card shows the module
 * once with the number of the slots it takes.
 *
 * @param slots The slots of the project.
 * @param modules The modules of the catalog.
 * @returns The modules of the project, in the order of the slots.
 */
export function installedModules(slots: ShipDesign['slots'], modules: ShipModule[]): InstalledModule[] {
  const byId = new Map(modules.map((module) => [module.id, module]));
  const grouped = new Map<number, InstalledModule>();

  for (const slot of [...slots].sort((left, right) => left.slotIndex - right.slotIndex)) {
    const module = byId.get(slot.partTypeId);

    if (!module) {
      continue;
    }

    const known = grouped.get(module.id);

    if (known) {
      known.count += 1;
      continue;
    }

    grouped.set(module.id, { module, markLevel: slot.markLevel, count: 1 });
  }

  return [...grouped.values()];
}

/**
 * Tells why the module does not go into the hull, or `null` when it does.
 *
 * @param hull The hull of the project, or `null` when the player has not chosen one.
 * @param used How many slots are taken.
 * @param total How many slots the hull has.
 * @returns The reason, or `null`.
 */
export function moduleReason(hull: ShipHull | null, used: number, total: number): ModuleReason | null {
  if (!hull) {
    return 'noHull';
  }

  if (used >= total) {
    return 'slotsFull';
  }

  // The modules that do not go into the hull are not shown at all, so the only reason left is
  // the one that changes while the player looks: the slots of the ship are over.
  return null;
}

/**
 * Tells whether the module goes into the hull.
 *
 * @param module The module.
 * @param hull The hull of the project, or `null` when the player has not chosen one.
 * @returns <see langword="true"/> when the module goes into the hull.
 */
export function moduleFits(module: ShipModule, hull: ShipHull | null): boolean {
  if (!hull) {
    return false;
  }

  // The ammunition of a weapon and the capsules of the inhabitants are not modules of a ship:
  // they are loaded into the weapon or used on a body, never fitted to a hull.
  if (module.isAmmunition || module.isCapsule) {
    return false;
  }

  // The belonging is the main rule: the module goes only on the hulls it is fit for, whatever
  // its role is (task 00042). A module without a list of hulls goes on any hull.
  if (module.compatibleHullIds.length > 0 && !module.compatibleHullIds.includes(hull.id)) {
    return false;
  }

  if (module.role === 'universal') {
    return true;
  }

  if (module.role === hull.role) {
    return true;
  }

  // The transport hull is a peaceful hull: the transports of the author are the civil hulls,
  // so a peaceful module — the drilling rig, the geological survey — goes on them.
  return module.role === 'civil' && hull.role === 'transport';
}

/**
 * The characteristic of the module that the tile of the module shows: the first one it has, so
 * the tile says what the module gives (the damage of a weapon, the push of a drive).
 *
 * @param module The module.
 * @returns The value and the name of the characteristic, or `null` when the module has none.
 */
export function mainTrait(module: ShipModule): { value: string; label: 'damage' | 'drivePush' | 'shield' | 'power' | 'energy' } | null {
  if (module.damage > 0) {
    return { value: String(module.damage), label: 'damage' };
  }

  if (module.drivePush > 0) {
    return { value: String(module.drivePush), label: 'drivePush' };
  }

  if (module.shieldCapacity > 0) {
    return { value: String(module.shieldCapacity), label: 'shield' };
  }

  if (module.power > 0) {
    return { value: String(module.power), label: 'power' };
  }

  if (module.energyUse > 0) {
    return { value: String(module.energyUse), label: 'energy' };
  }

  return null;
}

/**
 * The shots a weapon makes in one round. A weapon without a rate still fires once.
 *
 * @param rate The rate of fire of the weapon.
 * @returns The shots in one round.
 */
export function shotsPerRound(rate: number | undefined): number {
  return rate !== undefined && rate > 0 ? rate : 1;
}

/**
 * The least and the greatest kinetic damage one weapon makes in a round, from the ammunition
 * it accepts. A launcher takes the kinetic rounds of its size. A weapon without a size takes
 * the kinetic rounds that have no size.
 *
 * @param weapon The fitted kinetic weapon.
 * @param ammunition The ammunition of the catalog.
 * @returns The range, or `null` when no kinetic round fits the weapon.
 */
export function kineticRound(
  weapon: ShipModule,
  ammunition: ShipAmmunition[],
): { least: number; greatest: number } | null {
  const launcherSize = weapon.launcherSize ?? 0;
  const fitting = ammunition.filter((round) => {
    if (round.damageKind !== 'kinetic' || round.damage <= 0) {
      return false;
    }

    return launcherSize > 0 ? round.launcherSize === launcherSize : round.launcherSize === 0;
  });

  if (fitting.length === 0) {
    return null;
  }

  const shots = shotsPerRound(weapon.rate);
  const amounts = fitting.map((round) => round.damage * shots);

  return { least: Math.min(...amounts), greatest: Math.max(...amounts) };
}

/**
 * What the fitted modules add to the hold, the armor, the shield and the damage of a project.
 *
 * @param slots The slots of the project.
 * @param modules The modules of the catalog, by their identifier.
 * @param ammunition The ammunition of the catalog.
 * @returns The numbers the plaque of the project shows.
 */
export function fittedCombat(
  slots: { partTypeId: number }[],
  modules: Map<number, ShipModule>,
  ammunition: ShipAmmunition[],
): { cargo: number; armor: number; shieldRegen: number; energyDamage: number; kineticMin: number; kineticMax: number } {
  let cargo = 0;
  let armor = 0;
  let shieldRegen = 0;
  let energyDamage = 0;
  let kineticMin = 0;
  let kineticMax = 0;

  for (const slot of slots) {
    const module = modules.get(slot.partTypeId);

    if (!module) {
      continue;
    }

    cargo += module.cargoCapacity ?? 0;
    armor += module.armor ?? 0;
    shieldRegen += (module.shieldRegen ?? 0) + (module.shieldRestore ?? 0);

    if (module.damageKind === 'energy' && module.damage > 0) {
      energyDamage += module.damage * shotsPerRound(module.rate);
    } else if (module.damageKind === 'kinetic') {
      const range = kineticRound(module, ammunition);

      if (range) {
        kineticMin += range.least;
        kineticMax += range.greatest;
      }
    }
  }

  return { cargo, armor, shieldRegen, energyDamage, kineticMin, kineticMax };
}

/**
 * The characteristics of the fitted modules that the plaque adds up. A characteristic the
 * plaque already shows on its own row is not here, and a zero amount is left out.
 *
 * @param slots The slots of the project.
 * @param modules The modules of the catalog, by their identifier.
 * @returns The sums, in the order of the characteristics.
 */
export function fittedFacts(
  slots: { partTypeId: number }[],
  modules: Map<number, ShipModule>,
): ShipDesignFact[] {
  const totals = new Map<string, ShipModuleFact>();

  for (const slot of slots) {
    const moduleFacts = modules.get(slot.partTypeId)?.facts;

    if (!moduleFacts) {
      continue;
    }

    for (const fact of moduleFacts) {
      if (fact.amount === 0) {
        continue;
      }

      const current = totals.get(fact.kind);

      if (current) {
        totals.set(fact.kind, { ...current, amount: current.amount + fact.amount });
      } else {
        totals.set(fact.kind, { ...fact });
      }
    }
  }

  return [...totals.values()]
    .sort((left, right) => left.sortOrder - right.sortOrder || left.kind.localeCompare(right.kind))
    .map((fact) => ({
      kind: fact.kind,
      nameKey: fact.nameKey,
      unitKey: fact.unitKey,
      amount: fact.amount,
    }));
}

/**
 * The name of a summed characteristic. Extraction is named by the hour. The other names come
 * from the catalog.
 *
 * @param fact The summed characteristic.
 * @param t The function of the texts.
 * @param text The function of the texts of the catalog.
 * @returns The name.
 */
export function factLabel(
  fact: { kind: string; nameKey: string },
  t: (key: MessageKey, params?: MessageParams) => string,
  text: EntityText,
): string {
  if (fact.kind === 'yield') {
    return t('shipyard.yield');
  }

  return text(fact.nameKey) ?? fact.nameKey;
}

/**
 * The amount of a summed characteristic, with its unit when the catalog has one.
 *
 * @param fact The summed characteristic.
 * @param text The function of the texts of the catalog.
 * @param language The language of the numbers.
 * @returns The text of the amount.
 */
export function formatFactAmount(
  fact: { amount: number; unitKey: string | null },
  text: EntityText,
  language: Language,
): string {
  const amountText = formatAmount(fact.amount, language);
  const unit = fact.unitKey ? text(fact.unitKey) : null;

  return unit ? `${amountText} ${unit}` : amountText;
}

/**
 * The text of the shield: the capacity, and the recovery in parentheses when the ship has it.
 *
 * @param capacity The capacity of the shields.
 * @param regen How fast the shields recover.
 * @param t The function of the texts.
 * @param language The language of the numbers.
 * @returns The text of the shield.
 */
export function formatShield(
  capacity: number,
  regen: number,
  t: (key: MessageKey, params?: MessageParams) => string,
  language: Language,
): string {
  const capacityText = formatAmount(capacity, language);

  if (regen <= 0) {
    return capacityText;
  }

  return t('shipyard.shieldWithRegen', { capacity: capacityText, regen: formatAmount(regen, language) });
}

/**
 * The text of the damage of one round. A side that has no damage is left out, and the whole
 * line is left out when neither side has any.
 *
 * @param energyDamage The energy damage of the round.
 * @param kineticMin The least kinetic damage of the round.
 * @param kineticMax The greatest kinetic damage of the round.
 * @param t The function of the texts.
 * @param language The language of the numbers.
 * @returns The text, or `null` when there is no damage.
 */
export function formatCombatDamage(
  energyDamage: number,
  kineticMin: number,
  kineticMax: number,
  t: (key: MessageKey, params?: MessageParams) => string,
  language: Language,
): string | null {
  const parts: string[] = [];

  if (energyDamage > 0) {
    parts.push(`${formatAmount(energyDamage, language)} ${t('shipyard.energyDamage')}`);
  }

  const least = kineticMin > 0 ? kineticMin : kineticMax;
  const greatest = kineticMax > 0 ? kineticMax : kineticMin;

  if (greatest > 0) {
    const amount = least === greatest
      ? formatAmount(least, language)
      : t('shipyard.kineticRange', { min: formatAmount(least, language), max: formatAmount(greatest, language) });

    parts.push(`${amount} ${t('shipyard.kineticDamage')}`);
  }

  return parts.length === 0 ? null : parts.join(' · ');
}

/**
 * The reason why a saved project breaks a rule of the constructor, or `null` when it breaks
 * none (task 00038, the mark of the card that needs fixing).
 *
 * @param summary What the project gives, as the server counted it.
 * @param t The function of the texts.
 * @param language The language of the texts.
 * @returns The reason, or `null`.
 */
export function designProblem(
  summary: ShipDesignSummary,
  t: (key: MessageKey, params?: MessageParams) => string,
  language: Language,
): string | null {
  if (!summary.hasDrive) {
    return t('shipyard.checkNoDrive');
  }

  if (!summary.hasReactor) {
    return t('shipyard.checkNoReactor');
  }

  if (summary.energyUse > summary.energyPower) {
    return t('shipyard.checkNotEnoughPower', {
      use: formatAmount(summary.energyUse, language),
      power: formatAmount(summary.energyPower, language),
    });
  }

  return null;
}
