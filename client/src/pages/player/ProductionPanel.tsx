import { useMemo, useState } from 'react';
import { ApiError } from '../../api/http';
import type { ProductionFactory, ProductionOrder, ProductionRecipe, ProductionStack } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { TextField } from '../../components/FormField';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { formatDuration, formatNumber, lookup } from './planetText';
import { useNow } from './usePlanet';
import { useProduction } from './useProduction';

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['production_', 'planets_', 'requirements_'];

/**
 * The factory of a body with its recipes, its queue and the stock of the parts (task 00013).
 * The page only shows the rules; the server checks them on every command.
 */
export function ProductionPanel({ planetId }: { planetId: number }) {
  const { t, language } = useI18n();
  const { production } = useApi();
  const { text } = useTexts();
  const { factories, factory, stock, receivedAt, failed, reload, applyFactory, selectFactory } = useProduction(planetId);
  const [notice, setNotice] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const now = useNow();

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
        text: reasonPrefixes.some((prefix) => code.startsWith(prefix)) ? reasonText(language, code) : t('error.generic.text'),
      });
      await reload();
    } finally {
      setBusy(false);
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
          <ProductionQueue
            orders={factory.queue}
            recipes={factory.recipes}
            language={language}
            gameNow={factory.gameNow}
            receivedAt={receivedAt}
            now={now}
            busy={busy}
            onCancel={(order) =>
              void run(
                () => production.cancel(planetId, factory.planetBuildingId, order.orderId),
                t('production.cancelled'),
              )
            }
          />
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
      <StockPanel stacks={stock?.stacks ?? []} capacity={stock?.storage.capacity ?? 0} stored={stock?.storage.stored ?? 0} />
    </section>
  );
}

/**
 * The batches of the queue of the factory with the progress of the units.
 */
function ProductionQueue({
  orders,
  recipes,
  language,
  gameNow,
  receivedAt,
  now,
  busy,
  onCancel,
}: {
  orders: ProductionOrder[];
  recipes: ProductionRecipe[];
  language: Language;
  gameNow: string;
  receivedAt: number;
  now: number;
  busy: boolean;
  onCancel: (order: ProductionOrder) => void;
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
      <div className="production-block">
        <h3 className="panel__title">{t('production.queue')}</h3>
        <p className="lead">{t('production.queueEmpty')}</p>
      </div>
    );
  }

  return (
    <div className="production-block">
      <h3 className="panel__title">{t('production.queue')}</h3>
      <ol className="queue-list">
        {orders.map((order) => (
          <li key={order.orderId} className="queue-item">
            <div className="queue-item__body">
              <strong>{nameOf(order.recipeId)}</strong>
              <span className="meta num">
                {t('production.batch', { made: order.producedQuantity, total: order.quantity })}
              </span>
              {order.isBlocked ? <span className="meta">{t('production.blocked')}</span> : null}
              {order.nextUnitAt ? (
                <span className="meta num">
                  {t('production.nextUnit', {
                    time: formatDuration(language, Math.max(0, secondsLeft(order.nextUnitAt, gameNow, receivedAt, now))),
                  })}
                </span>
              ) : null}
            </div>
            <button className="btn btn--secondary" type="button" disabled={busy} onClick={() => onCancel(order)}>
              {t('production.cancel')}
            </button>
          </li>
        ))}
      </ol>
    </div>
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
                  time: formatDuration(language, recipe.durationSeconds),
                })}
              </span>
              {!recipe.isAvailable ? (
                <ul className="reason-list">
                  {recipe.reasons.map((reason) => (
                    <li key={`${reason.code}-${reason.targetId}`}>{reasonText(language, reason.code)}</li>
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
function StockPanel({ stacks, capacity, stored }: { stacks: ProductionStack[]; capacity: number; stored: number }) {
  const { t, language } = useI18n();

  return (
    <div className="production-block">
      <h3 className="panel__title">{t('production.stock')}</h3>
      <p className="lead num">{t('production.stockCapacity', { stored: formatNumber(stored, language), capacity: formatNumber(capacity, language) })}</p>
      {capacity > 0 && stored >= capacity ? (
        <Alert kind="error" title={t('production.storageFull.title')}>
          {t('production.storageFull.text')}
        </Alert>
      ) : null}
      {stacks.length === 0 ? (
        <p className="lead">{t('production.stockEmpty')}</p>
      ) : (
        <ul className="storage-list">
          {stacks.map((stack) => (
            <li key={`${stack.partTypeId}-${stack.markLevel}-${stack.hasRaceBonus}-${stack.conditionPercent}`} className="storage-row">
              <span>{t('production.part', { id: stack.partTypeId })}</span>
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
 * The real seconds left until a moment of game time, from the answer of the server and the
 * moment it was received.
 */
function secondsLeft(finishesAt: string, gameNow: string, receivedAt: number, now: number): number {
  const millisecondsPerSecond = 1000;
  const gameSeconds = (Date.parse(finishesAt) - Date.parse(gameNow)) / millisecondsPerSecond;

  return gameSeconds - (now - receivedAt) / millisecondsPerSecond;
}

/**
 * The mark level of a part as the player sees it: the first, the second, the third.
 */
function markName(level: number): string {
  const names: Record<number, string> = { 1: 'I', 2: 'II', 3: 'III' };

  return names[level] ?? String(level);
}

/**
 * The text of a reason of the server.
 */
function reasonText(language: Language, code: string): string {
  return lookup(language, `reason.${code}`, code);
}
