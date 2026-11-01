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
import { galaxyPlace } from './planetText';
import { allRecipes, availabilityFilters, factoryOf, filterRecipes, groupRecipes, resourceGroup } from './productionRecipes';
import { noticeFromError, type Notice } from './screenNotice';
import { useProductionOrders } from './useActiveOrders';
import { usePlanet } from './usePlanet';
import { useRecipes } from './useRecipes';
import { useCurrentPlanetId, useShellActions } from './useShell';

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
  const { planet } = usePlanet(planetId);
  const { list, failed, reload } = useRecipes(planetId);
  const orders = useProductionOrders(planetId, () => void reload());
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);
  const [query, setQuery] = useState('');
  const [availability, setAvailability] = useState(allRecipes);
  const [factory, setFactory] = useState(allRecipes);
  const [group, setGroup] = useState(allRecipes);

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
        await Promise.all([orders.reload(), reload()]);
        refreshAfterCommand(planetId);
        setBusy(false);
      }
    },
    [factories, planetId, production, t, language, orders, reload, refreshAfterCommand],
  );
  const produceRef = useRef(produce);
  useEffect(() => {
    produceRef.current = produce;
  });
  const onProduce = useCallback((recipe: ProductionRecipe, quantity: number) => void produceRef.current(recipe, quantity), []);

  const recipes = list?.recipes;
  const visible = useMemo(
    () =>
      recipes
        ? filterRecipes(recipes, { query, availability, factory, group }, (recipe) => `${text(recipe.nameKey) ?? recipe.nameKey} ${text(recipe.descriptionKey) ?? ''}`)
        : [],
    [recipes, query, availability, factory, group, text],
  );
  const groups = useMemo(() => (recipes ? groupRecipes(recipes) : []), [recipes]);
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
            <label className="field research-planet">
              <span className="field__label">{t('production.group')}</span>
              <select className="input" value={group} onChange={(event) => setGroup(event.target.value)}>
                <option value={allRecipes}>{t('production.allGroups')}</option>
                {groups.map((entry) => (
                  <option key={entry.key} value={entry.key}>
                    {entry.key === resourceGroup ? t('production.groupResources') : (text(entry.nameKey ?? '') ?? entry.nameKey ?? entry.key)}
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
                <h2 className="build-category__title" id={`production-${entry.key}`}>
                  {entry.key === resourceGroup ? t('production.groupResources') : (text(entry.nameKey ?? '') ?? entry.nameKey ?? entry.key)}
                </h2>
                <ul className="building-grid">
                  {entry.recipes.map((recipe) => (
                    <RecipeCard busy={busy} factoryName={factoryNameOf(recipe.buildingTypeId)} key={recipe.recipeId} onProduce={onProduce} recipe={recipe} />
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
