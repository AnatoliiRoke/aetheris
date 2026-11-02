import type { Building, BuildingCategory, Planet, QueueItem } from '../../api/planetApi';

/**
 * A group of the tiles of the building page: the category from the data of the server and the
 * buildings that have it. The category is `null` only for buildings the data gave no known
 * category (it should not happen); they form the last group, "other".
 */
export interface BuildingGroup {
  key: string;
  category: BuildingCategory | null;
  buildings: Building[];
}

/**
 * The buildings grouped by the category the server gives them, in the order of the categories
 * (`sortOrder`, then the identifier). A category without a building makes no group. The client
 * guesses nothing from the class or the effects of a building: a building whose category is
 * missing from the data lands in the last group, which has no category.
 *
 * @param planet The body with its buildings and the categories of the data.
 */
export function groupBuildings(planet: Pick<Planet, 'buildings' | 'buildingCategories'>): BuildingGroup[] {
  const categories = [...planet.buildingCategories].sort((first, second) => first.sortOrder - second.sortOrder || first.id - second.id);
  const known = new Set(categories.map((category) => category.id));
  const groups: BuildingGroup[] = categories
    .map((category) => ({
      key: `category-${category.id}`,
      category,
      buildings: planet.buildings.filter((building) => building.categoryId === category.id),
    }))
    .filter((group) => group.buildings.length > 0);
  const others = planet.buildings.filter((building) => !known.has(building.categoryId));

  return others.length > 0 ? [...groups, { key: 'other', category: null, buildings: others }] : groups;
}

/**
 * The value of a filter that keeps every building and every category.
 */
export const allBuildings = 'all';

/**
 * The chips of the filter by state, in the same order as the research screen. A building has one
 * state: the top level, then the record that is being built, then a record that waits, then one
 * the server would accept, and the rest are closed.
 */
export const buildingStateFilters = [
  { id: 'available' },
  { id: 'queued' },
  { id: 'building' },
  { id: 'top' },
  { id: 'closed' },
] as const;

/**
 * The state of a building for the filter chips.
 *
 * @param building The building of the body.
 * @param queueItems The construction queue of the same body.
 */
export function buildingFilterState(building: Building, queueItems: readonly QueueItem[]): string {
  if (building.level >= building.maxLevel) {
    return 'top';
  }

  const queued = queueItems.filter((item) => item.buildingTypeId === building.buildingTypeId);

  if (queued.some((item) => item.startedAt !== null)) {
    return 'building';
  }

  if (queued.length > 0) {
    return 'queued';
  }

  return building.next.canEnqueue ? 'available' : 'closed';
}

/**
 * Keeps the groups of the chosen category and, inside them, the buildings of the chosen state.
 * An empty group is dropped, so a heading is not left without tiles.
 *
 * @param groups The groups of the body, already ordered.
 * @param categoryKey The key of a group, or {@link allBuildings}.
 * @param state The identifier of a chip, or {@link allBuildings}.
 * @param queueItems The construction queue, used to tell a waiting record from one being built.
 */
export function filterBuildingGroups(
  groups: readonly BuildingGroup[],
  categoryKey: string,
  state: string,
  queueItems: readonly QueueItem[],
): BuildingGroup[] {
  const chosen = categoryKey === allBuildings ? groups : groups.filter((group) => group.key === categoryKey);

  if (state === allBuildings) {
    return [...chosen];
  }

  return chosen
    .map((group) => ({
      ...group,
      buildings: group.buildings.filter((building) => buildingFilterState(building, queueItems) === state),
    }))
    .filter((group) => group.buildings.length > 0);
}
