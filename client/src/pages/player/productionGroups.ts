import type { ProductionPartGroup, ProductionRecipe } from '../../api/productionApi';

/**
 * The value of a filter that keeps everything.
 */
export const allRecipes = 'all';

/**
 * The key of the group of the recipes that make a resource; those recipes have no group of parts.
 */
export const resourcesGroupKey = 'resources';

/**
 * A group of the parts in the tree the server gives (task 00043). A group of the top level is a
 * section of the production screen, a group inside it is a tab of the section, and a group inside
 * a tab is a tab of the next row, such as the kinetic and the energy weapons inside the weapons.
 */
export interface ProductionGroupNode {
  /** The stable code of the group. */
  code: string;
  /** The localisation key of the name of the group, or `null` when only a recipe named it. */
  nameKey: string | null;
  /** The groups inside this one, in their order. */
  children: ProductionGroupNode[];
  /** The codes of this group and of every group inside it, so a tab keeps the recipes of all of them. */
  codes: readonly string[];
}

/**
 * The group of a recipe: the code of the group of the part it makes, or {@link resourcesGroupKey}
 * for a recipe that makes a resource.
 *
 * @param recipe The recipe.
 */
export function groupKeyOf(recipe: ProductionRecipe): string {
  return recipe.partGroupCode ?? resourcesGroupKey;
}

/**
 * Builds the tree of the groups that hold recipes: the sections of the screen with their tabs.
 * A group that holds no recipe, by itself or through a group inside it, is left out. A recipe
 * whose group the tree does not know gets a section of its own, so no recipe is lost.
 *
 * @param groups The groups of the parts with their parents, as the server gives them.
 * @param recipes All the recipes of the screen.
 */
export function buildGroupTree(groups: readonly ProductionPartGroup[], recipes: readonly ProductionRecipe[]): ProductionGroupNode[] {
  const usedCodes = new Set(recipes.map(groupKeyOf));
  const knownIds = new Set(groups.map((group) => group.partGroupId));
  const childrenOf = new Map<number | null, ProductionPartGroup[]>();

  for (const group of groups) {
    // The server leaves out an empty field, and a parent the list does not hold makes the group
    // one of the top level.
    const parentId = group.parentGroupId != null && knownIds.has(group.parentGroupId) ? group.parentGroupId : null;
    const siblings = childrenOf.get(parentId) ?? [];

    siblings.push(group);
    childrenOf.set(parentId, siblings);
  }

  const visited = new Set<number>();
  const build = (parentId: number | null): ProductionGroupNode[] => {
    const nodes: ProductionGroupNode[] = [];
    const siblings = [...(childrenOf.get(parentId) ?? [])].sort((left, right) => left.sortOrder - right.sortOrder || left.partGroupId - right.partGroupId);

    for (const group of siblings) {
      // The server refuses a loop of the parents; the guard keeps the screen safe anyway.
      if (visited.has(group.partGroupId)) {
        continue;
      }

      visited.add(group.partGroupId);
      const children = build(group.partGroupId);
      const codes = [group.code, ...children.flatMap((child) => child.codes)];

      if (codes.some((code) => usedCodes.has(code))) {
        nodes.push({ code: group.code, nameKey: group.nameKey, children, codes });
      }
    }

    return nodes;
  };

  const roots = build(null);
  const placedCodes = new Set(roots.flatMap((root) => root.codes));

  for (const recipe of recipes) {
    // A recipe of a resource has no group: the server leaves the code out, and an empty code is
    // never a section.
    const code = recipe.partGroupCode;

    if (code && !placedCodes.has(code)) {
      placedCodes.add(code);
      roots.push({ code, nameKey: recipe.partGroupNameKey, children: [], codes: [code] });
    }
  }

  return roots;
}
