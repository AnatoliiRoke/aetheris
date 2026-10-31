import { useEffect, useState } from 'react';
import { ApiError } from '../../api/http';
import type { ProductionFactory, ProductionReason, ProductionRecipe, ProductionStack, ProductionStorage } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { TextField } from '../../components/FormField';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import type { EntityText } from '../../texts/context';
import { useTexts } from '../../texts/useTexts';
import { formatDuration, formatNumber, lookup } from './planetText';
import { useProduction } from './useProduction';
import { useShellActions } from './useShell';

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['production_', 'planets_', 'requirements_'];

/**
 * The factory of a body with its recipes and the stock of the parts (task 00013). The queue of
 * the batches is not here: the page shows the batches of all the factories in the standard
 * queue (task 00020, section 7.5). The page only shows the rules; the server checks them on
 * every command.
 *
 * @param planetId The body.
 * @param onChanged What to tell the page after a command, so the queue of the production reads again.
 * @param revision A number the page raises when something outside this panel changed the production, so the panel reads again.
 */
export function ProductionPanel({
  planetId,
  onChanged,
  revision = 0,
}: {
  planetId: number;
  onChanged?: () => void;
  revision?: number;
}) {
  const { t, language } = useI18n();
  const { production } = useApi();
  const { refreshAfterCommand } = useShellActions();
  const { text } = useTexts();
  const { factories, factory, stock, loaded, failed, reload, applyFactory, selectFactory } = useProduction(planetId);
  const [notice, setNotice] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (revision > 0) {
      void reload();
    }
  }, [revision, reload]);

  const run = async (action: () => Promise<ProductionFactory>, success: string) => {
    setBusy(true);
    setNotice(null);

    try {
      applyFactory(await action());
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

  if (factories.length === 0) {
    return (
      <section className="panel" aria-labelledby="production-title">
        <div className="panel__head">
          <h2 className="panel__title" id="production-title">
            {t('production.title')}
          </h2>
        </div>
        {failed ? (
          <Alert kind="error">{t('production.loadFailed')}</Alert>
        ) : loaded ? (
          <p className="lead">{t('production.noFactory')}</p>
        ) : (
          <p className="lead" role="status">
            {t('production.loading')}
          </p>
        )}
      </section>
    );
  }

  return (
    <section className="panel" aria-labelledby="production-title">
      <div className="panel__head">
        <h2 className="panel__title" id="production-title">
          {t('production.title')}
        </h2>
        {factory ? <span className="meta">{t('production.factoryLevel', { level: factory.factoryLevel })}</span> : null}
      </div>
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      {factories.length > 1 ? (
        <label className="field">
          <span className="field__label">{t('production.factory')}</span>
          <select
            className="input"
            value={factory?.planetBuildingId ?? ''}
            disabled={busy}
            onChange={(event) => selectFactory(Number(event.target.value))}
          >
            {factories.map((summary) => (
              <option key={summary.planetBuildingId} value={summary.planetBuildingId}>
                {text(summary.nameKey) ?? summary.nameKey}
              </option>
            ))}
          </select>
        </label>
      ) : null}
      {!factory ? (
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      ) : (
        <>
          <RecipeList
            recipes={factory.recipes}
            language={language}
            busy={busy}
            onProduce={(recipe, quantity) =>
              void run(
                () => production.enqueue(planetId, factory.planetBuildingId, recipe.recipeId, quantity),
                t('production.queued'),
              )
            }
          />
        </>
      )}
      <StockPanel stacks={stock?.stacks ?? []} storage={stock?.storage ?? null} />
    </section>
  );
}

/**
 * The recipes of the factory: the open ones can be ordered, the closed ones say why.
 */
function RecipeList({
  recipes,
  language,
  busy,
  onProduce,
}: {
  recipes: ProductionRecipe[];
  language: Language;
  busy: boolean;
  onProduce: (recipe: ProductionRecipe, quantity: number) => void;
}) {
  const { t } = useI18n();
  const { text } = useTexts();
  const [quantities, setQuantities] = useState<Record<number, string>>({});

  if (recipes.length === 0) {
    return (
      <div className="production-block">
        <h3 className="panel__title">{t('production.recipes')}</h3>
        <p className="lead">{t('production.noRecipes')}</p>
      </div>
    );
  }

  return (
    <div className="production-block">
      <h3 className="panel__title">{t('production.recipes')}</h3>
      <ul className="building-grid">
        {recipes.map((recipe) => {
          const quantity = Number(quantities[recipe.recipeId] ?? '1');
          const valid = Number.isInteger(quantity) && quantity > 0;
          const name = text(recipe.nameKey) ?? recipe.nameKey;

          return (
            <li key={recipe.recipeId} className="building-card">
              <div className="building-card__head">
                <span className="building-card__name">{name}</span>
                {recipe.isAvailable ? (
                  <span className="badge">{t('production.available')}</span>
                ) : (
                  <span className="badge badge--warning">{t('production.closed')}</span>
                )}
              </div>
              <span className="building-card__text">{text(recipe.descriptionKey) ?? recipe.descriptionKey}</span>
              <span className="building-card__text num">
                {t('production.output', {
                  quantity: formatNumber(recipe.outputQuantity, language),
                  time: formatDuration(language, recipe.unitSeconds),
                })}
              </span>
              {recipe.ingredients.length > 0 ? (
                <span className="building-card__text num">
                  {t('production.ingredients', {
                    list: recipe.ingredients
                      .map((ingredient) => `${text(ingredient.nameKey) ?? ingredient.nameKey} ${formatNumber(ingredient.amount, language)}`)
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
                  <TextField
                    label={t('production.quantity')}
                    type="number"
                    min={1}
                    step={1}
                    value={quantities[recipe.recipeId] ?? '1'}
                    disabled={busy}
                    onChange={(event) =>
                      setQuantities((current) => ({ ...current, [recipe.recipeId]: event.target.value }))
                    }
                  />
                  <button
                    className="btn btn--secondary"
                    type="button"
                    disabled={busy || !valid}
                    onClick={() => onProduce(recipe, quantity)}
                  >
                    {t('production.produce')}
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
 * The stock of the parts of the body with the mark of the race.
 */
function StockPanel({ stacks, storage }: { stacks: ProductionStack[]; storage: ProductionStorage | null }) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const partName = (partTypeId: number) => {
    const stack = stacks.find((entry) => entry.partTypeId === partTypeId);

    return (stack ? text(stack.nameKey) : undefined) ?? t('production.part', { id: partTypeId });
  };
  const fullKinds = (storage?.kinds ?? []).filter((kind) => kind.isFull);

  return (
    <div className="production-block">
      <h3 className="panel__title">{t('production.stock')}</h3>
      <p className="lead num">{t('production.stockCapacity', { capacity: formatNumber(storage?.capacity ?? 0, language) })}</p>
      {fullKinds.length > 0 ? (
        <Alert kind="error" title={t('production.storageFull.title')}>
          {t('production.storageFull.text', { parts: fullKinds.map((kind) => partName(kind.partTypeId)).join(', ') })}
        </Alert>
      ) : null}
      {stacks.length === 0 ? (
        <p className="lead">{t('production.stockEmpty')}</p>
      ) : (
        <ul className="storage-list">
          {stacks.map((stack) => (
            <li key={`${stack.partTypeId}-${stack.markLevel}-${stack.hasRaceBonus}-${stack.conditionPercent}`} className="storage-row">
              <span>{text(stack.nameKey) ?? t('production.part', { id: stack.partTypeId })}</span>
              <span className="num">
                {t('production.mark', { level: markName(stack.markLevel) })}
              </span>
              {stack.hasRaceBonus ? <span className="badge">{t('production.raceBonus')}</span> : null}
              <span className="num">{formatNumber(stack.quantity, language)}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
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
function reasonText(language: Language, reason: ProductionReason, text: EntityText): string {
  const name = reason.targetNameKey ? (text(reason.targetNameKey) ?? reason.targetNameKey) : String(reason.targetId);

  return lookup(language, `reason.${reason.code}`, reason.code, { name, level: reason.targetLevel });
}
