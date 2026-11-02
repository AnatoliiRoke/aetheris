import { memo, useMemo } from 'react';
import type { Building, Planet } from '../../api/planetApi';
import { useI18n } from '../../i18n/useI18n';
import type { Language } from '../../i18n/context';
import { BuildingCard } from './BuildingCard';
import { Tabs } from './Tabs';
import { allBuildings, buildingStateFilters, filterBuildingGroups, groupBuildings, type BuildingGroup } from './buildingCategories';
import { lookup, type namer } from './planetText';

type Names = ReturnType<typeof namer>;

/**
 * The name of a group of buildings: the name of its category from the data of the server, and a
 * plain "other" for the buildings the data gave no known category.
 *
 * @param group The group.
 * @param language The language of the page.
 * @param names The names of the entities of the body.
 * @param other The text of the group without a category.
 */
function groupName(group: BuildingGroup, language: Language, names: Names, other: string): string {
  return group.category ? lookup(language, group.category.nameKey, group.category.code, undefined, names.entities) : other;
}

/**
 * The catalog of the buildings of the construction screen: the tabs of the categories the data gives,
 * the chips of the state and the tiles of the buildings. It is made like the technologies of the research
 * screen (the same tab bar, the same chips), so the two screens work alike. The tab "all" shows every
 * category with its heading. Only the tiles whose own data changed are drawn again.
 *
 * @param planet The body with its buildings, the categories and the construction queue.
 * @param names The names of the entities of the body.
 * @param language The language of the page.
 * @param categoryKey The chosen tab: the key of a group or {@link allBuildings}.
 * @param stateFilter The chosen chip: a state or {@link allBuildings}.
 * @param busy Whether a command of the screen is under way.
 * @param onCategory What to do when the player picks a tab.
 * @param onState What to do when the player picks a chip.
 * @param onEnqueue What to do when the player queues a building.
 */
export const BuildCatalog = memo(function BuildCatalog({
  planet,
  names,
  language,
  categoryKey,
  stateFilter,
  busy,
  onCategory,
  onState,
  onEnqueue,
}: {
  planet: Planet;
  names: Names;
  language: Language;
  categoryKey: string;
  stateFilter: string;
  busy: boolean;
  onCategory: (key: string) => void;
  onState: (state: string) => void;
  onEnqueue: (building: Building) => void;
}) {
  const { t } = useI18n();
  const groups = useMemo(() => groupBuildings(planet), [planet]);
  // A tab that no longer exists (a category archived while the screen was open) shows everything.
  const activeKey = groups.some((group) => group.key === categoryKey) ? categoryKey : allBuildings;
  const visible = useMemo(() => filterBuildingGroups(groups, activeKey, stateFilter, planet.queue.items), [groups, activeKey, stateFilter, planet.queue.items]);
  const other = t('planet.category.other');
  const tabs = useMemo(
    () => [{ id: allBuildings, label: t('build.categories.all') }, ...groups.map((group) => ({ id: group.key, label: groupName(group, language, names, other) }))],
    [groups, language, names, other, t],
  );
  const showHeadings = activeKey === allBuildings;

  return (
    <section aria-label={t('build.catalog')} className="panel">
      <Tabs active={activeKey} label={t('build.categories')} onChange={onCategory} tabs={tabs} />
      <div className="tech-tools">
        <div aria-label={t('build.filter')} className="filter-chips" role="group">
          {[{ id: allBuildings }, ...buildingStateFilters].map((entry) => (
            <button aria-pressed={stateFilter === entry.id} className="chip" key={entry.id} type="button" onClick={() => onState(entry.id)}>
              {t(`build.filter.${entry.id}` as never)}
            </button>
          ))}
        </div>
      </div>
      {visible.length === 0 ? (
        <div className="civ-empty">
          <p>{t('build.noMatches')}</p>
        </div>
      ) : (
        visible.map((group) => (
          <section aria-labelledby={`build-${group.key}`} className="build-category" key={group.key}>
            {showHeadings ? (
              <h2 className="build-category__title" id={`build-${group.key}`}>
                {groupName(group, language, names, other)}
              </h2>
            ) : (
              <h2 className="visually-hidden" id={`build-${group.key}`}>
                {groupName(group, language, names, other)}
              </h2>
            )}
            <ul className="building-grid">
              {group.buildings.map((building) => (
                <BuildingCard building={building} busy={busy} key={building.buildingTypeId} names={names} onEnqueue={onEnqueue} planet={planet} />
              ))}
            </ul>
          </section>
        ))
      )}
    </section>
  );
});
