import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError } from '../../api/http';
import type { NamedItem } from '../../api/planetApi';
import type {
  ProcessBuilding,
  ProcessIngredient,
  ProcessOrderRequest,
  ProcessPartSubject,
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
import { usePlanet } from './usePlanet';
import { useProcesses } from './useProcesses';
import { useShellActions } from './useShell';

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['processes_', 'planets_'];

/**
 * The processes of a body (task 00014): the buildings that run them, their recipes and the
 * storage of the parts when the processes work with them. The queue of the orders is not here:
 * the page shows the orders of all the buildings in the standard queue (task 00020, section 7.5).
 *
 * The panel is the same for every kind of process: the server sends the recipes, the
 * ingredients and the reasons, so the code of the client knows no kind of process.
 *
 * @param planetId The body.
 * @param onChanged What to tell the page after a command, so the queue of the processes reads again.
 * @param revision A number the page raises when something outside this panel changed the processes, so the panel reads again.
 */
export function ProcessesPanel({
  planetId,
  onChanged,
  revision = 0,
}: {
  planetId: number;
  onChanged?: () => void;
  revision?: number;
}) {
  const { t, language } = useI18n();
  const { processes } = useApi();
  const { refreshAfterCommand } = useShellActions();
  const { text } = useTexts();
  const { buildings, building, loaded, failed, reload, applyBuilding, selectBuilding } = useProcesses(planetId);
  const { planet } = usePlanet(planetId);
  const [notice, setNotice] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (revision > 0) {
      void reload();
    }
  }, [revision, reload]);

  // The recipes name the resources and the groups by their id, so the names come from the
  // body and from the texts of the game data.
  const resources = useMemo(() => planet?.resourceTypes ?? [], [planet]);
  const balances = useMemo(
    () => new Map((planet?.resources ?? []).map((resource) => [resource.resourceTypeId, resource.amount])),
    [planet],
  );
  const usesParts = useMemo(
    () =>
      building?.recipes.some(
        (recipe) => recipe.partSubject !== 'none' || recipe.ingredients.some((ingredient) => ingredient.partTypeId !== null),
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
      onChanged?.();
      refreshAfterCommand(planetId);
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
          <ProcessRecipes
            recipes={building.recipes}
            language={language}
            resources={resources}
            balances={balances}
            stacks={stacks}
            fullCondition={building.fullConditionPercent}
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
 * What the player chose for a recipe: the number of units, the resource the process takes
 * and gives and the stack of the storage it works on.
 */
interface RecipeForm {
  quantity: string;
  sourceResourceTypeId: string;
  targetResourceTypeId: string;
  partKey: string;
}

const emptyForm: RecipeForm = { quantity: '1', sourceResourceTypeId: '', targetResourceTypeId: '', partKey: '' };

/**
 * A choice of the storage for an order: the parts of one kind in one condition.
 */
interface PartChoice {
  key: string;
  partTypeId: number;
  conditionPercent: number;
  quantity: number;
}

/**
 * The parts of the storage an order of a recipe may name, by the part subject the server sends.
 */
function partChoices(subject: ProcessPartSubject, stacks: ProductionStack[], fullCondition: number): PartChoice[] {
  if (subject === 'none') {
    return [];
  }

  const choices = new Map<string, PartChoice>();

  for (const stack of stacks) {
    const damaged = stack.conditionPercent < fullCondition;

    if ((subject === 'damaged' && !damaged) || (subject === 'new' && damaged)) {
      continue;
    }

    const key = `${stack.partTypeId}:${stack.conditionPercent}`;
    const choice = choices.get(key);
    choices.set(key, {
      key,
      partTypeId: stack.partTypeId,
      conditionPercent: stack.conditionPercent,
      quantity: (choice?.quantity ?? 0) + stack.quantity,
    });
  }

  return [...choices.values()];
}

/**
 * The recipes of the building: the open ones can be ordered, the closed ones say why and keep
 * their button inactive. The form of a recipe shows only what its data asks for, and the check
 * of the balance and of the storage is only a hint: the server decides.
 */
function ProcessRecipes({
  recipes,
  language,
  resources,
  balances,
  stacks,
  fullCondition,
  resourceName,
  groupName,
  partName,
  busy,
  onStart,
}: {
  recipes: ProcessRecipe[];
  language: Language;
  resources: NamedItem[];
  balances: Map<number, number>;
  stacks: ProductionStack[];
  fullCondition: number;
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
          const validQuantity = Number.isInteger(quantity) && quantity > 0;
          const sourceOptions = recipe.sourceGroupId === null ? [] : resources.filter((resource) => resource.resourceGroupId === recipe.sourceGroupId);
          const targetOptions = recipe.targetGroupId === null ? [] : resources.filter((resource) => resource.resourceGroupId === recipe.targetGroupId);
          const choices = partChoices(recipe.partSubject, stacks, fullCondition);
          const choice = choices.find((entry) => entry.key === form.partKey) ?? null;
          const complete =
            validQuantity &&
            (recipe.sourceGroupId === null || form.sourceResourceTypeId !== '') &&
            (recipe.targetGroupId === null || form.targetResourceTypeId !== '') &&
            (recipe.partSubject === 'none' || choice !== null);
          const reasons = recipe.reasons.map((reason) => reasonText(language, reason, text));

          // The hint of the balance: the ingredients of every unit, and one unit of the source
          // for every unit of a recipe that names a source group. The server checks it anyway.
          if (recipe.isAvailable && complete) {
            const needed = new Map<number, number>();

            for (const ingredient of recipe.ingredients) {
              if (ingredient.resourceTypeId !== null) {
                needed.set(ingredient.resourceTypeId, (needed.get(ingredient.resourceTypeId) ?? 0) + ingredient.amount * quantity);
              }
            }

            if (recipe.sourceGroupId !== null) {
              const sourceId = Number(form.sourceResourceTypeId);
              needed.set(sourceId, (needed.get(sourceId) ?? 0) + quantity);
            }

            const missing = [...needed.entries()]
              .filter(([resourceTypeId, amount]) => (balances.get(resourceTypeId) ?? 0) < amount)
              .map(([resourceTypeId, amount]) => `${resourceName(resourceTypeId)} ${formatNumber(amount - (balances.get(resourceTypeId) ?? 0), language)}`);

            for (const ingredient of recipe.ingredients) {
              if (ingredient.partTypeId === null) {
                continue;
              }

              const stored = stacks
                .filter((stack) => stack.partTypeId === ingredient.partTypeId && stack.conditionPercent >= fullCondition)
                .reduce((sum, stack) => sum + stack.quantity, 0);

              if (stored < ingredient.amount * quantity) {
                missing.push(`${partName(ingredient.partTypeId)} ${formatNumber(ingredient.amount * quantity - stored, language)}`);
              }
            }

            if (choice !== null && choice.quantity < quantity) {
              missing.push(`${partName(choice.partTypeId)} ${formatNumber(quantity - choice.quantity, language)}`);
            }

            if (missing.length > 0) {
              reasons.push(t('processes.missing', { list: missing.join(', ') }));
            }
          }

          const canStart = recipe.isAvailable && complete && reasons.length === 0;
          const reasonsId = `process-recipe-${recipe.recipeId}-reasons`;

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
              {recipe.needsShip ? <span className="building-card__text">{t('processes.needsShip')}</span> : null}
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
              <div className="building-card__next">
                {recipe.isAvailable && sourceOptions.length > 0 ? (
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
                {recipe.isAvailable && targetOptions.length > 0 ? (
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
                {recipe.isAvailable && recipe.partSubject !== 'none' ? (
                  <label className="field">
                    <span className="field__label">{t('processes.partLabel')}</span>
                    <select
                      className="input"
                      value={form.partKey}
                      disabled={busy || choices.length === 0}
                      onChange={(event) => change(recipe.recipeId, 'partKey', event.target.value)}
                    >
                      <option value="">{choices.length === 0 ? t('processes.noParts') : t('processes.choosePart')}</option>
                      {choices.map((entry) => (
                        <option key={entry.key} value={entry.key}>
                          {t('processes.partChoice', {
                            name: partName(entry.partTypeId),
                            condition: formatNumber(entry.conditionPercent, language),
                            quantity: formatNumber(entry.quantity, language),
                          })}
                        </option>
                      ))}
                    </select>
                  </label>
                ) : null}
                {recipe.isAvailable ? (
                  <TextField
                    label={t('processes.quantity')}
                    type="number"
                    min={1}
                    step={1}
                    value={form.quantity}
                    disabled={busy}
                    onChange={(event) => change(recipe.recipeId, 'quantity', event.target.value)}
                  />
                ) : null}
                <button
                  className="btn btn--secondary"
                  type="button"
                  disabled={busy || !canStart}
                  title={reasons.length > 0 ? reasons.join('. ') : undefined}
                  aria-describedby={reasons.length > 0 ? reasonsId : undefined}
                  onClick={() =>
                    onStart(
                      recipe,
                      {
                        sourceResourceTypeId: recipe.sourceGroupId === null ? null : Number(form.sourceResourceTypeId),
                        targetResourceTypeId: recipe.targetGroupId === null ? null : Number(form.targetResourceTypeId),
                        partTypeId: choice?.partTypeId ?? null,
                        partConditionPercent: choice === null || recipe.partSubject === 'new' ? null : choice.conditionPercent,
                      },
                      quantity,
                    )
                  }
                >
                  {t('processes.produce')}
                </button>
              </div>
              {reasons.length > 0 ? (
                <ul className="reason-list" id={reasonsId}>
                  {reasons.map((reason) => (
                    <li key={reason}>{reason}</li>
                  ))}
                </ul>
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
  const stacks = stock?.stacks ?? [];
  const partName = (partTypeId: number) => {
    const stack = stacks.find((entry) => entry.partTypeId === partTypeId);

    return (stack ? text(stack.nameKey) : undefined) ?? t('processes.part', { id: partTypeId });
  };
  const fullKinds = (stock?.storage.kinds ?? []).filter((kind) => kind.isFull);

  return (
    <div className="processes-block">
      <h3 className="panel__title">{t('processes.storage')}</h3>
      <p className="lead num">{t('processes.storageCapacity', { capacity: formatNumber(capacity, language) })}</p>
      {fullKinds.length > 0 ? (
        <Alert kind="error" title={t('processes.storageFull.title')}>
          {t('processes.storageFull.text', { parts: fullKinds.map((kind) => partName(kind.partTypeId)).join(', ') })}
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
