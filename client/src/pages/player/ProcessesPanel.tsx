import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError } from '../../api/http';
import type { NamedItem } from '../../api/planetApi';
import type {
  ProcessBuilding,
  ProcessIngredient,
  ProcessOrder,
  ProcessOrderRequest,
  ProcessReason,
  ProcessRecipe,
} from '../../api/processesApi';
import type { ProductionStack, ProductionStock } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { TextField } from '../../components/FormField';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import type { EntityText } from '../../texts/context';
import { useTexts } from '../../texts/useTexts';
import { formatDuration, formatNumber, lookup } from './planetText';
import { useNow, usePlanet } from './usePlanet';
import { useProcesses } from './useProcesses';

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['processes_'];

/**
 * The processes of a body (task 00014): the buildings that run them, the queue of the
 * building that is open, its recipes and the storage of the parts when the processes work
 * with them.
 *
 * The panel is the same for every kind of process: the server sends the recipes, the
 * ingredients and the reasons, so the code of the client knows no kind of process.
 */
export function ProcessesPanel({ planetId }: { planetId: number }) {
  const { t, language } = useI18n();
  const { processes } = useApi();
  const { text } = useTexts();
  const { buildings, building, receivedAt, loaded, failed, reload, applyBuilding, selectBuilding } = useProcesses(planetId);
  const { planet } = usePlanet(planetId);
  const [notice, setNotice] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const now = useNow();

  // The recipes name the resources and the groups by their id, so the names come from the
  // body and from the texts of the game data.
  const resources = useMemo(() => planet?.resourceTypes ?? [], [planet]);
  const usesParts = useMemo(
    () =>
      building?.recipes.some(
        (recipe) => recipe.ingredients.some((ingredient) => ingredient.partTypeId !== null) || recipe.shipStub !== null,
      ) ?? false,
    [building],
  );
  const stock = usePartStorage(planetId, usesParts);
  const stacks = useMemo(() => stock?.stacks ?? [], [stock]);

  const resourceName = useCallback(
    (resourceTypeId: number) => {
      const key = resources.find((resource) => resource.id === resourceTypeId)?.nameKey;

      return (key ? text(key) : undefined) ?? t('processes.resource', { id: resourceTypeId });
    },
    [resources, text, t],
  );

  const groupName = useCallback(
    (resourceGroupId: number) => {
      const key = planet?.resourceGroups.find((group) => group.id === resourceGroupId)?.nameKey;

      return (key ? text(key) : undefined) ?? t('processes.group', { id: resourceGroupId });
    },
    [planet, text, t],
  );

  const partName = useCallback(
    (partTypeId: number) => {
      const key = stacks.find((stack) => stack.partTypeId === partTypeId)?.nameKey;

      return (key ? text(key) : undefined) ?? t('processes.part', { id: partTypeId });
    },
    [stacks, text, t],
  );

  const run = async (action: () => Promise<ProcessBuilding>, success: string) => {
    setBusy(true);
    setNotice(null);

    try {
      applyBuilding(await action());
      setNotice({ kind: 'success', text: success });
    } catch (error) {
      const code = error instanceof ApiError ? error.code : 'internal_error';
      setNotice({
        kind: 'error',
        text: reasonPrefixes.some((prefix) => code.startsWith(prefix)) ? lookup(language, `reason.${code}`, code) : t('error.generic.text'),
      });
      await reload();
    } finally {
      setBusy(false);
    }
  };

  if (buildings.length === 0) {
    return (
      <section className="panel" aria-labelledby="processes-title">
        <div className="panel__head">
          <h2 className="panel__title" id="processes-title">
            {t('processes.title')}
          </h2>
        </div>
        {failed ? (
          <Alert kind="error">{t('processes.loadFailed')}</Alert>
        ) : loaded ? (
          <p className="lead">{t('processes.noBuildings')}</p>
        ) : (
          <p className="lead" role="status">
            {t('processes.loading')}
          </p>
        )}
      </section>
    );
  }

  return (
    <section className="panel" aria-labelledby="processes-title">
      <div className="panel__head">
        <h2 className="panel__title" id="processes-title">
          {t('processes.title')}
        </h2>
        {building ? <span className="meta">{t('processes.buildingLevel', { level: building.buildingLevel })}</span> : null}
      </div>
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      {buildings.length > 1 ? (
        <label className="field">
          <span className="field__label">{t('processes.building')}</span>
          <select
            className="input"
            value={building?.planetBuildingId ?? ''}
            disabled={busy}
            onChange={(event) => selectBuilding(Number(event.target.value))}
          >
            {buildings.map((summary) => (
              <option key={summary.planetBuildingId} value={summary.planetBuildingId}>
                {text(summary.nameKey) ?? summary.nameKey}
              </option>
            ))}
          </select>
        </label>
      ) : null}
      {!building ? (
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      ) : (
        <>
          <ProcessQueue
            orders={building.queue}
            recipes={building.recipes}
            language={language}
            gameNow={building.gameNow}
            receivedAt={receivedAt}
            now={now}
            busy={busy}
            onCancel={(order) =>
              void run(
                () => processes.cancel(planetId, building.planetBuildingId, order.orderId),
                t('processes.cancelled'),
              )
            }
          />
          <ProcessRecipes
            recipes={building.recipes}
            language={language}
            resources={resources}
            stacks={stacks}
            resourceName={resourceName}
            groupName={groupName}
            partName={partName}
            busy={busy}
            onStart={(recipe, request, quantity) =>
              void run(
                () => processes.enqueue(planetId, building.planetBuildingId, recipe.recipeId, quantity, request),
                t('processes.queued'),
              )
            }
          />
        </>
      )}
      {usesParts ? <ProcessStorage stock={stock} /> : null}
    </section>
  );
}

/**
 * The orders of the queue of the building with the progress of the units.
 */
function ProcessQueue({
  orders,
  recipes,
  language,
  gameNow,
  receivedAt,
  now,
  busy,
  onCancel,
}: {
  orders: ProcessOrder[];
  recipes: ProcessRecipe[];
  language: Language;
  gameNow: string;
  receivedAt: number;
  now: number;
  busy: boolean;
  onCancel: (order: ProcessOrder) => void;
}) {
  const { t } = useI18n();
  const { text } = useTexts();
  const nameOf = useMemo(() => {
    const byId = new Map(recipes.map((recipe) => [recipe.recipeId, recipe]));

    return (recipeId: number) => {
      const recipe = byId.get(recipeId);
      const key = recipe?.nameKey;

      return key ? (text(key) ?? key) : String(recipeId);
    };
  }, [recipes, text]);

  if (orders.length === 0) {
    return (
      <div className="processes-block">
        <h3 className="panel__title">{t('processes.queue')}</h3>
        <p className="lead">{t('processes.queueEmpty')}</p>
      </div>
    );
  }

  return (
    <div className="processes-block">
      <h3 className="panel__title">{t('processes.queue')}</h3>
      <ol className="queue-list">
        {orders.map((order) => (
          <li key={order.orderId} className="queue-item">
            <div className="queue-item__body">
              <strong>{nameOf(order.recipeId)}</strong>
              <span className="meta num">
                {t('processes.batch', { made: order.producedQuantity, total: order.quantity })}
              </span>
              {order.isBlocked ? <span className="meta">{t('processes.blocked')}</span> : null}
              {order.nextUnitAt ? (
                <span className="meta num">
                  {t('processes.nextUnit', {
                    time: formatDuration(language, Math.max(0, secondsLeft(order.nextUnitAt, gameNow, 1, receivedAt, now))),
                  })}
                </span>
              ) : null}
            </div>
            <button className="btn btn--secondary" type="button" disabled={busy} onClick={() => onCancel(order)}>
              {t('processes.cancel')}
            </button>
          </li>
        ))}
      </ol>
    </div>
  );
}

/**
 * What the player chose for a recipe: the number of units, the resource the process takes
 * and gives and the part it works on.
 */
interface RecipeForm {
  quantity: string;
  sourceResourceTypeId: string;
  targetResourceTypeId: string;
  partTypeId: string;
}

const emptyForm: RecipeForm = { quantity: '1', sourceResourceTypeId: '', targetResourceTypeId: '', partTypeId: '' };

/**
 * The recipes of the building: the open ones can be ordered, the closed ones say why. The
 * form of a recipe shows only what its data asks for.
 */
function ProcessRecipes({
  recipes,
  language,
  resources,
  stacks,
  resourceName,
  groupName,
  partName,
  busy,
  onStart,
}: {
  recipes: ProcessRecipe[];
  language: Language;
  resources: NamedItem[];
  stacks: ProductionStack[];
  resourceName: (resourceTypeId: number) => string;
  groupName: (resourceGroupId: number) => string;
  partName: (partTypeId: number) => string;
  busy: boolean;
  onStart: (recipe: ProcessRecipe, request: ProcessOrderRequest, quantity: number) => void;
}) {
  const { t } = useI18n();
  const { text } = useTexts();
  const [forms, setForms] = useState<Record<number, RecipeForm>>({});

  const change = (recipeId: number, field: keyof RecipeForm, value: string) => {
    setForms((current) => ({ ...current, [recipeId]: { ...(current[recipeId] ?? emptyForm), [field]: value } }));
  };

  const ingredientLabel = (ingredient: ProcessIngredient): string => {
    if (ingredient.resourceTypeId !== null) {
      return resourceName(ingredient.resourceTypeId);
    }

    if (ingredient.partTypeId !== null) {
      return partName(ingredient.partTypeId);
    }

    return t('processes.ingredient');
  };

  if (recipes.length === 0) {
    return (
      <div className="processes-block">
        <h3 className="panel__title">{t('processes.recipes')}</h3>
        <p className="lead">{t('processes.noRecipes')}</p>
      </div>
    );
  }

  return (
    <div className="processes-block">
      <h3 className="panel__title">{t('processes.recipes')}</h3>
      <ul className="building-grid">
        {recipes.map((recipe) => {
          const form = forms[recipe.recipeId] ?? emptyForm;
          const quantity = Number(form.quantity);
          const partTypeIds = recipe.ingredients
            .filter((ingredient) => ingredient.partTypeId !== null)
            .map((ingredient) => ingredient.partTypeId);
          const sourceOptions = recipe.sourceGroupId === null ? [] : resources.filter((resource) => resource.resourceGroupId === recipe.sourceGroupId);
          const targetOptions = recipe.targetGroupId === null ? [] : resources.filter((resource) => resource.resourceGroupId === recipe.targetGroupId);
          const partOptions = stacks.filter((stack) => partTypeIds.includes(stack.partTypeId));
          const complete =
            Number.isInteger(quantity) &&
            quantity > 0 &&
            (recipe.sourceGroupId === null || form.sourceResourceTypeId !== '') &&
            (recipe.targetGroupId === null || form.targetResourceTypeId !== '') &&
            (partTypeIds.length === 0 || form.partTypeId !== '');

          return (
            <li key={recipe.recipeId} className="building-card">
              <div className="building-card__head">
                <span className="building-card__name">{text(recipe.nameKey) ?? recipe.nameKey}</span>
                {recipe.isAvailable ? (
                  <span className="badge">{t('processes.available')}</span>
                ) : (
                  <span className="badge badge--warning">{t('processes.closed')}</span>
                )}
              </div>
              <span className="building-card__text">
                {t('processes.processType', { name: text(recipe.processTypeNameKey) ?? recipe.processTypeNameKey })}
              </span>
              <span className="building-card__text">{text(recipe.descriptionKey) ?? recipe.descriptionKey}</span>
              <span className="building-card__text num">{t('processes.duration', { time: formatDuration(language, recipe.durationSeconds) })}</span>
              {recipe.sourceGroupId !== null || recipe.targetGroupId !== null ? (
                <span className="building-card__text">
                  {t('processes.groups', {
                    source: recipe.sourceGroupId === null ? '—' : groupName(recipe.sourceGroupId),
                    target: recipe.targetGroupId === null ? '—' : groupName(recipe.targetGroupId),
                  })}
                </span>
              ) : null}
              {recipe.ingredients.length > 0 ? (
                <span className="building-card__text num">
                  {t('processes.ingredients', {
                    list: recipe.ingredients
                      .map((ingredient) => `${ingredientLabel(ingredient)} ${formatNumber(ingredient.amount, language)}`)
                      .join(', '),
                  })}
                </span>
              ) : null}
              {!recipe.isAvailable ? (
                <ul className="reason-list">
                  {recipe.reasons.map((reason) => (
                    <li key={`${reason.code}-${reason.targetId}`}>{reasonText(language, reason, text)}</li>
                  ))}
                </ul>
              ) : null}
              {recipe.isAvailable ? (
                <div className="building-card__next">
                  {sourceOptions.length > 0 ? (
                    <label className="field">
                      <span className="field__label">{t('processes.source')}</span>
                      <select
                        className="input"
                        value={form.sourceResourceTypeId}
                        disabled={busy}
                        onChange={(event) => change(recipe.recipeId, 'sourceResourceTypeId', event.target.value)}
                      >
                        <option value="">{t('processes.chooseResource')}</option>
                        {sourceOptions.map((resource) => (
                          <option key={resource.id} value={resource.id}>
                            {resourceName(resource.id)}
                          </option>
                        ))}
                      </select>
                    </label>
                  ) : null}
                  {targetOptions.length > 0 ? (
                    <label className="field">
                      <span className="field__label">{t('processes.target')}</span>
                      <select
                        className="input"
                        value={form.targetResourceTypeId}
                        disabled={busy}
                        onChange={(event) => change(recipe.recipeId, 'targetResourceTypeId', event.target.value)}
                      >
                        <option value="">{t('processes.chooseResource')}</option>
                        {targetOptions.map((resource) => (
                          <option key={resource.id} value={resource.id}>
                            {resourceName(resource.id)}
                          </option>
                        ))}
                      </select>
                    </label>
                  ) : null}
                  {partOptions.length > 0 ? (
                    <label className="field">
                      <span className="field__label">{t('processes.partLabel')}</span>
                      <select
                        className="input"
                        value={form.partTypeId}
                        disabled={busy}
                        onChange={(event) => change(recipe.recipeId, 'partTypeId', event.target.value)}
                      >
                        <option value="">{t('processes.choosePart')}</option>
                        {partOptions.map((stack) => (
                          <option key={stack.partTypeId} value={stack.partTypeId}>
                            {partName(stack.partTypeId)}
                          </option>
                        ))}
                      </select>
                    </label>
                  ) : null}
                  <TextField
                    label={t('processes.quantity')}
                    type="number"
                    min={1}
                    step={1}
                    value={form.quantity}
                    disabled={busy}
                    onChange={(event) => change(recipe.recipeId, 'quantity', event.target.value)}
                  />
                  <button
                    className="btn btn--secondary"
                    type="button"
                    disabled={busy || !complete}
                    onClick={() =>
                      onStart(
                        recipe,
                        {
                          sourceResourceTypeId: recipe.sourceGroupId === null ? null : Number(form.sourceResourceTypeId),
                          targetResourceTypeId: recipe.targetGroupId === null ? null : Number(form.targetResourceTypeId),
                          partTypeId: partTypeIds.length === 0 ? null : Number(form.partTypeId),
                          shipStub: recipe.shipStub,
                        },
                        quantity,
                      )
                    }
                  >
                    {t('processes.produce')}
                  </button>
                </div>
              ) : null}
            </li>
          );
        })}
      </ul>
    </div>
  );
}

/**
 * The storage of the parts of the body with the mark of the race.
 */
function ProcessStorage({ stock }: { stock: ProductionStock | null }) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const capacity = stock?.storage.capacity ?? 0;
  const stored = stock?.storage.stored ?? 0;
  const stacks = stock?.stacks ?? [];

  return (
    <div className="processes-block">
      <h3 className="panel__title">{t('processes.storage')}</h3>
      <p className="lead num">
        {t('processes.storageCapacity', { stored: formatNumber(stored, language), capacity: formatNumber(capacity, language) })}
      </p>
      {capacity > 0 && stored >= capacity ? (
        <Alert kind="error" title={t('processes.storageFull.title')}>
          {t('processes.storageFull.text')}
        </Alert>
      ) : null}
      {stacks.length === 0 ? (
        <p className="lead">{t('processes.storageEmpty')}</p>
      ) : (
        <ul className="storage-list">
          {stacks.map((stack) => (
            <li key={`${stack.partTypeId}-${stack.markLevel}-${stack.hasRaceBonus}-${stack.conditionPercent}`} className="storage-row">
              <span>{text(stack.nameKey) ?? t('processes.part', { id: stack.partTypeId })}</span>
              <span className="num">
                {t('processes.mark', { level: markName(stack.markLevel) })}
              </span>
              {stack.hasRaceBonus ? <span className="badge">{t('processes.raceBonus')}</span> : null}
              <span className="num">{formatNumber(stack.quantity, language)}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/**
 * The storage of the parts of the body. It belongs to the production module, so the panel
 * reads it from there, and only when the processes of the building work with the parts.
 */
function usePartStorage(planetId: number, relevant: boolean): ProductionStock | null {
  const { production } = useApi();
  const [stock, setStock] = useState<ProductionStock | null>(null);
  const active = useRef(true);

  const applyStock = useCallback((next: ProductionStock | null) => {
    if (active.current) {
      setStock(next);
    }
  }, []);

  const reload = useCallback(async () => {
    if (!relevant) {
      return;
    }

    try {
      applyStock(await production.stock(planetId));
    } catch {
      // The storage of the parts is extra information: the panel goes on without it.
      applyStock(null);
    }
  }, [planetId, production, relevant, applyStock]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  return stock;
}

/**
 * The real seconds left until a moment of game time, from the answer of the server and the
 * moment it was received.
 */
function secondsLeft(finishesAt: string, gameNow: string, multiplier: number, receivedAt: number, now: number): number {
  const millisecondsPerSecond = 1000;
  const gameSeconds = (Date.parse(finishesAt) - Date.parse(gameNow)) / millisecondsPerSecond;
  const speed = multiplier > 0 ? multiplier : 1;

  return gameSeconds / speed - (now - receivedAt) / millisecondsPerSecond;
}

/**
 * The mark level of a part as the player sees it: the first, the second, the third.
 */
function markName(level: number): string {
  const names: Record<number, string> = { 1: 'I', 2: 'II', 3: 'III' };

  return names[level] ?? String(level);
}

/**
 * The text of a reason why a recipe is closed, with the name and the level of what it needs.
 */
function reasonText(language: Language, reason: ProcessReason, text: EntityText): string {
  const name = reason.targetNameKey ? (text(reason.targetNameKey) ?? reason.targetNameKey) : String(reason.targetId);

  return lookup(language, `reason.${reason.code}`, reason.code, { name, level: reason.targetLevel });
}
