import type { ShipDesign, ShipDesignSummary, ShipHull, ShipModule } from '../../../api/fleetApi';
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
