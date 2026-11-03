import type { ProductionRecipe } from '../../api/productionApi';

/**
 * The value of a filter that keeps everything.
 */
export const allRecipes = 'all';

/**
 * The key of the group of the recipes that make a resource; those recipes have no group of parts.
 */
export const resourcesGroupKey = 'resources';

/**
 * The codes of the groups of the parts, as the reference seed gives them. The client groups the
 * recipes by these codes, never by the identifiers of the groups, which change with the data.
 */
export const partGroupCodes = {
  engines: 'part_group_1',
  reactors: 'part_group_2',
  energyWeapons: 'part_group_3',
  kineticWeapons: 'part_group_4',
  armour: 'part_group_5',
  shields: 'part_group_6',
  ammunition: 'part_group_7',
  extraction: 'part_group_8',
  reconnaissance: 'part_group_9',
  otherShip: 'part_group_10',
  baseModules: 'part_group_11',
  lightHulls: 'part_group_12',
  drones: 'part_group_13',
  mediumHulls: 'part_group_14',
  heavyHulls: 'part_group_15',
  superHeavyHulls: 'part_group_16',
  civilianHulls: 'part_group_17',
  structureHulls: 'part_group_18',
} as const;

/**
 * A tab of the first level of the production screen: the resources, the modules of the ships, the
 * hulls of the ships or the modules of the base.
 */
export type ProductionSectionKey = 'resources' | 'shipModules' | 'shipHulls' | 'baseModules';

/**
 * A tab of the second level: the group of the parts it shows, or {@link allRecipes} for all of them.
 */
export type ProductionGroupKey = string;

/**
 * The sections of the production screen with their groups.
 *
 * The weapons are in a tab of their own inside the modules of the ships, so they keep their three
 * kinds apart: the energy, the kinetic and the ammunition.
 */
export interface ProductionSection {
  /** The key of the section. */
  key: ProductionSectionKey;
  /** The groups of the parts the section shows, in their order. */
  groups: readonly ProductionGroupKey[];
}

/**
 * The sections of the production screen in their order.
 */
export const productionSections: readonly ProductionSection[] = [
  { key: 'resources', groups: [] },
  {
    key: 'shipModules',
    groups: [
      partGroupCodes.extraction,
      partGroupCodes.engines,
      partGroupCodes.reactors,
      'weapons',
      partGroupCodes.armour,
      partGroupCodes.shields,
      partGroupCodes.reconnaissance,
      partGroupCodes.otherShip,
    ],
  },
  {
    key: 'shipHulls',
    groups: [
      partGroupCodes.civilianHulls,
      partGroupCodes.lightHulls,
      partGroupCodes.mediumHulls,
      partGroupCodes.heavyHulls,
      partGroupCodes.superHeavyHulls,
      partGroupCodes.drones,
    ],
  },
  { key: 'baseModules', groups: [partGroupCodes.baseModules, partGroupCodes.structureHulls] },
];

/**
 * The groups of the weapons: the energy, the kinetic and the ammunition.
 */
export const weaponGroupCodes: readonly string[] = [partGroupCodes.energyWeapons, partGroupCodes.kineticWeapons, partGroupCodes.ammunition];

/**
 * The group of a recipe: the code of the group of the part it makes, or {@link resourcesGroupKey}
 * for a recipe that makes a resource.
 *
 * @param recipe The recipe.
 */
export function groupKeyOf(recipe: ProductionRecipe): string {
  return recipe.partGroupCode ?? resourcesGroupKey;
}
