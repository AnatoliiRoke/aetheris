import { memo, useState } from 'react';
import type { ProductionRecipe } from '../../api/productionApi';
import { TextField } from '../../components/FormField';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { formatDuration, formatNumber } from './planetText';
import { reasonText } from './productionRecipes';

/**
 * The tile of a recipe on the production screen, like the tile of a building or of a technology:
 * the name, whether it can be made, what a batch gives and how long a unit takes, what a unit
 * costs, the reasons it is closed, and, for an open recipe, the number of units and the button.
 * It is drawn again only when its own data changes, so a list of many recipes stays light.
 *
 * @param recipe The recipe with its availability.
 * @param factoryName The name of the factory that makes it.
 * @param busy Whether a command is running.
 * @param onProduce What to do when the player starts the batch; it must be the same function between the draws.
 */
export const RecipeCard = memo(function RecipeCard({
  recipe,
  factoryName,
  busy,
  onProduce,
}: {
  recipe: ProductionRecipe;
  factoryName: string;
  busy: boolean;
  onProduce: (recipe: ProductionRecipe, quantity: number) => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const [typed, setTyped] = useState('1');
  const quantity = Number(typed);
  const valid = Number.isInteger(quantity) && quantity > 0;

  return (
    <li className="building-card">
      <div className="building-card__head">
        <span className="building-card__name">{text(recipe.nameKey) ?? recipe.nameKey}</span>
        {recipe.isAvailable ? <span className="badge">{t('production.available')}</span> : <span className="badge badge--warning">{t('production.closed')}</span>}
      </div>
      <span className="building-card__text">{text(recipe.descriptionKey) ?? recipe.descriptionKey}</span>
      {factoryName ? <span className="meta">{t('production.madeAt', { name: factoryName })}</span> : null}
      <span className="building-card__text num">
        {t('production.output', { quantity: formatNumber(recipe.outputQuantity, language), time: formatDuration(language, recipe.unitSeconds) })}
      </span>
      {recipe.ingredients.length > 0 ? (
        <span className="building-card__text num">
          {t('production.ingredients', {
            list: recipe.ingredients.map((ingredient) => `${text(ingredient.nameKey) ?? ingredient.nameKey} ${formatNumber(ingredient.amount, language)}`).join(', '),
          })}
        </span>
      ) : null}
      {!recipe.isAvailable ? (
        <ul className="reason-list">
          {recipe.reasons.map((reason) => (
            <li key={`${reason.code}-${reason.targetId}`}>{reasonText(language, reason, text)}</li>
          ))}
        </ul>
      ) : (
        <div className="building-card__next">
          <TextField label={t('production.quantity')} type="number" min={1} step={1} value={typed} disabled={busy} onChange={(event) => setTyped(event.target.value)} />
          <button className="btn btn--secondary" type="button" disabled={busy || !valid} onClick={() => onProduce(recipe, quantity)}>
            {t('production.produce')}
          </button>
        </div>
      )}
    </li>
  );
});
