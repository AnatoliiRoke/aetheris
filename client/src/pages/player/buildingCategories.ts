import type { Building, BuildingCategory, Planet } from '../../api/planetApi';

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
