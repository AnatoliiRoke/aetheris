import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ProductionRecipe } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { GameIcon } from '../../components/GameIcon';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { ProductionQueue } from './ProductionQueue';
import { RecipeCard } from './RecipeCard';
import { ScreenHead } from './ScreenHead';
import { Tabs, type TabItem } from './Tabs';
import { galaxyPlace } from './planetText';
import { allRecipes, availabilityFilters, factoryOf, filterRecipes, groupRecipes, resourcesGroupKey } from './productionRecipes';
import { buildGroupTree, groupKeyOf, type ProductionGroupNode } from './productionGroups';
import { noticeFromError, type Notice } from './screenNotice';
import { useProductionOrders } from './useActiveOrders';
import { usePlanet } from './usePlanet';
import { useRecipes } from './useRecipes';
import { useResourceGroups } from './useResourceGroups';
import { useCurrentPlanetId, useShellActions } from './useShell';
import { useEconomy } from './useEconomy';
import { useProduction } from './useProduction';
import { useWorldReference } from './useWorldReference';

/**
 * The prefixes of the error codes of the production that have their own text for the player.
 */
const reasonPrefixes = ['production_', 'planets_', 'requirements_'];

/**
 * The production screen (task 00032), the same as the screens of the construction and the
 * research: the queue of the batches of all the factories of the body, the search and the
 * filters, and the tiles of every recipe of every factory, each with its cost, its time, the
 * reasons it is closed and the number of units to make. The server checks every rule again.
 */
export function ProductionPage() {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const { production } = useApi();
  const { refreshAfterCommand } = useShellActions();
  useDocumentTitle(t('meta.production.title'));

  const planetId = useCurrentPlanetId();
  const resourceGroupCode = useResourceGroups(useWorldReference());
  const { planet } = usePlanet(planetId);
  const { list, failed, reload } = useRecipes(planetId);
  // What the body has for the ingredients of a recipe: the balances of the civilization, the
  // resources of the body and the stock of its parts.
  const { balances } = useEconomy();
  const { stock, reload: reloadStock } = useProduction(planetId);
  const orders = useProductionOrders(planetId, () => void reload());
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);
  const [query, setQuery] = useState('');
  const [availability, setAvailability] = useState(allRecipes);
  const [factory, setFactory] = useState(allRecipes);
  const [section, setSection] = useState(resourcesGroupKey);
  const [group, setGroup] = useState(allRecipes);
  const [subgroup, setSubgroup] = useState(allRecipes);

  // The tiles are drawn again only when their own data changes: the callback given to them keeps
  // the same identity between the draws.
  const factories = list?.factories;
  const produce = useCallback(
    async (recipe: ProductionRecipe, quantity: number) => {
      const target = factories ? factoryOf(factories, recipe) : null;

      if (planetId === null || target === null) {
        return;
      }

      setBusy(true);
      setNotice(null);

      try {
        await production.enqueue(planetId, target.planetBuildingId, recipe.recipeId, quantity);
        setNotice({ kind: 'success', text: t('production.queued') });
      } catch (error) {
        setNotice(noticeFromError(error, language, t('error.generic.text'), reasonPrefixes));
      } finally {
        await Promise.all([orders.reload(), reload(), reloadStock()]);
        refreshAfterCommand(planetId);
        setBusy(false);
      }
    },
    [factories, planetId, production, t, language, orders, reload, reloadStock, refreshAfterCommand],
  );
  const produceRef = useRef(produce);
  useEffect(() => {
    produceRef.current = produce;
  });
  const onProduce = useCallback((recipe: ProductionRecipe, quantity: number) => void produceRef.current(recipe, quantity), []);

  // What the body has for the ingredients of a recipe. The object keeps its identity while the data
  // does not change, so the tiles of the recipes are not drawn again for nothing.
  const stockOfBody = useMemo(() => ({ balances, planet, stock }), [balances, planet, stock]);

  const recipes = list?.recipes;
  const partGroups = list?.groups;
  // The sections and the tabs come from the tree of the groups of the parts the server gives: a
  // group of the top level is a section, a group inside it is a tab, and a group inside a tab is
  // a tab of the next row (task 00043). A group with no recipes is left out.
  const tree = useMemo(() => (recipes ? buildGroupTree(partGroups ?? [], recipes) : []), [partGroups, recipes]);
  const hasResources = useMemo(() => (recipes ?? []).some((recipe) => groupKeyOf(recipe) === resourcesGroupKey), [recipes]);
  const groupName = useCallback(
    (entry: { key: string; nameKey: string | null }) => (entry.key === resourcesGroupKey ? t('production.groupResources') : (text(entry.nameKey ?? '') ?? entry.nameKey ?? entry.key)),
    [t, text],
  );
  const nodeName = useCallback((node: ProductionGroupNode) => groupName({ key: node.code, nameKey: node.nameKey }), [groupName]);
  const sectionTabs = useMemo(
    () => [...(hasResources ? [{ id: resourcesGroupKey, label: t('production.section.resources') }] : []), ...tree.map((node) => ({ id: node.code, label: nodeName(node) }))],
    [hasResources, tree, nodeName, t],
  );
  // A tab that no longer exists gives way to the first one, or to everything of its parent.
  const activeSectionId = sectionTabs.some((entry) => entry.id === section) ? section : (sectionTabs[0]?.id ?? '');
  const activeSection = tree.find((node) => node.code === activeSectionId) ?? null;
  const activeGroupNode = activeSection?.children.find((node) => node.code === group) ?? null;
  const activeSubgroupNode = activeGroupNode?.children.find((node) => node.code === subgroup) ?? null;
  const tabsOf = useCallback(
    (node: ProductionGroupNode | null): TabItem[] =>
      node && node.children.length > 0 ? [{ id: allRecipes, label: t('production.allGroups') }, ...node.children.map((child) => ({ id: child.code, label: nodeName(child) }))] : [],
    [nodeName, t],
  );
  const groupTabs = useMemo(() => tabsOf(activeSection), [tabsOf, activeSection]);
  const subgroupTabs = useMemo(() => tabsOf(activeGroupNode), [tabsOf, activeGroupNode]);
  // The deepest chosen group: the recipes of it and of every group inside it are shown.
  const shownNode = activeSubgroupNode ?? activeGroupNode ?? activeSection;
  const shownGroups = useMemo(() => (activeSectionId === resourcesGroupKey ? [resourcesGroupKey] : (shownNode?.codes ?? [])), [activeSectionId, shownNode]);
  // The titles of the groups are shown while the tab holds more than one group.
  const showGroupTitles = shownNode === null || shownNode.children.length > 0;
  const visible = useMemo(
    () =>
      recipes
        ? filterRecipes(recipes, { query, availability, factory, groups: shownGroups }, (recipe) => text(recipe.nameKey) ?? recipe.nameKey)
        : [],
    [recipes, query, availability, factory, shownGroups, text],
  );
  const factoryTypes = useMemo(() => [...new Set((recipes ?? []).map((recipe) => recipe.buildingTypeId).filter((typeId): typeId is number => typeId !== null))], [recipes]);
  // The name of a factory comes from the factories of the body, and for a factory the body has not
  // built from the reason of a closed recipe, which names it.
  const factoryNameKeys = useMemo(() => {
    const keys = new Map<number, string>();

    for (const recipe of recipes ?? []) {
      for (const reason of recipe.reasons) {
        if (reason.code === 'production_factory_not_built' && reason.targetNameKey) {
          keys.set(reason.targetId, reason.targetNameKey);
        }
      }
    }

    for (const summary of list?.factories ?? []) {
      keys.set(summary.buildingTypeId, summary.nameKey);
    }

    return keys;
  }, [list, recipes]);
  const factoryNameOf = useCallback(
    (buildingTypeId: number | null) => {
      const key = buildingTypeId === null ? undefined : factoryNameKeys.get(buildingTypeId);

      return key ? (text(key) ?? key) : '';
    },
    [factoryNameKeys, text],
  );

  if (planetId === null) {
    return null;
  }

  const timeMultiplier = planet?.timeMultiplier ?? 1;

  return (
    <div className="civ-main__in">
      <ScreenHead
        actions={
          <Link className="btn btn--secondary" to={routes.planet}>
            {t('screen.backToPlanet')}
          </Link>
        }
        breadcrumbs={[{ label: t('civ.nav.planet'), to: routes.planet }, { label: planet?.name ?? t('civ.untitled') }]}
        subtitle={planet?.coordinates ? galaxyPlace(t, planet.coordinates) : undefined}
        title={t('civ.nav.production')}
        titleNote={planet?.name ?? ''}
      />
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      {orders.notice ? <Alert kind={orders.notice.kind}>{orders.notice.text}</Alert> : null}
      {orders.orders ? (
        <ProductionQueue
          busy={busy || orders.busy}
          footnote={t('production.queueNote')}
          onCancel={(order) => void orders.cancel(order)}
          onFinished={() => {
            void orders.reload();
            void reload();
          }}
          orders={orders.orders}
          receivedAt={orders.receivedAt}
          timeMultiplier={timeMultiplier}
        />
      ) : (
        <section aria-label={t('production.queue')} className="panel">
          <h2 className="panel__title">{t('production.queue')}</h2>
          <p className="lead" role="status">
            {orders.failed ? t('production.loadFailed') : t('production.loading')}
          </p>
        </section>
      )}
      {!list ? (
        <div className="panel">
          {failed ? (
            <>
              <Alert kind="error">{t('production.loadFailed')}</Alert>
              <button className="btn btn--secondary" type="button" onClick={() => void reload()}>
                {t('planet.retry')}
              </button>
            </>
          ) : (
            <p className="lead" role="status">
              {t('production.loading')}
            </p>
          )}
        </div>
      ) : (
        <section aria-label={t('production.recipes')} className="panel">
          <Tabs active={activeSectionId} label={t('production.sections')} onChange={(id) => { setSection(id); setGroup(allRecipes); setSubgroup(allRecipes); }} tabs={sectionTabs} />
          {groupTabs.length > 1 ? (
            <Tabs active={activeGroupNode?.code ?? allRecipes} label={t('production.groups')} onChange={(id) => { setGroup(id); setSubgroup(allRecipes); }} tabs={groupTabs} />
          ) : null}
          {subgroupTabs.length > 1 ? (
            <Tabs active={activeSubgroupNode?.code ?? allRecipes} label={t('production.subgroups')} onChange={setSubgroup} tabs={subgroupTabs} />
          ) : null}
          <div className="tech-tools">
            <label className="field tech-search">
              <span className="visually-hidden">{t('production.search')}</span>
              <GameIcon className="tech-search__icon" name="search" size={20} />
              <input className="input" placeholder={t('production.search')} type="search" value={query} onChange={(event) => setQuery(event.target.value)} />
            </label>
            <div aria-label={t('production.filter')} className="filter-chips" role="group">
              {[{ id: allRecipes }, ...availabilityFilters].map((entry) => (
                <button aria-pressed={availability === entry.id} className="chip" key={entry.id} type="button" onClick={() => setAvailability(entry.id)}>
                  {t(`production.filter.${entry.id}` as never)}
                </button>
              ))}
            </div>
            <label className="field research-planet">
              <span className="field__label">{t('production.factory')}</span>
              <select className="input" value={factory} onChange={(event) => setFactory(event.target.value)}>
                <option value={allRecipes}>{t('production.allFactories')}</option>
                {factoryTypes.map((typeId) => (
                  <option key={typeId} value={String(typeId)}>
                    {factoryNameOf(typeId) || t('production.factoryNumber', { id: typeId })}
                  </option>
                ))}
              </select>
            </label>
          </div>
          {list.recipes.length === 0 ? (
            <div className="civ-empty">
              <p>{t('production.noRecipes')}</p>
            </div>
          ) : visible.length === 0 ? (
            <div className="civ-empty">
              <p>{t('production.noMatches')}</p>
            </div>
          ) : (
            groupRecipes(visible).map((entry) => (
              <section aria-labelledby={`production-${entry.key}`} className="build-category" key={entry.key}>
                <h2 className={showGroupTitles ? 'build-category__title' : 'visually-hidden'} id={`production-${entry.key}`}>
                  {groupName(entry)}
                </h2>
                <ul className="building-grid">
                  {entry.recipes.map((recipe) => (
                    <RecipeCard
                      availability={stockOfBody}
                      busy={busy}
                      factoryName={factoryNameOf(recipe.buildingTypeId)}
                      key={recipe.recipeId}
                      onProduce={onProduce}
                      recipe={recipe}
                      resourceGroupCode={resourceGroupCode}
                    />
                  ))}
                </ul>
              </section>
            ))
          )}
        </section>
      )}
    </div>
  );
}
