import { memo, useState } from 'react';
import type { ProductionRecipe } from '../../api/productionApi';
import { TextField } from '../../components/FormField';
import { ResourceIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { formatAmount, formatDuration } from './planetText';
import { reasonText } from './productionRecipes';

/**
 * The tile of a recipe on the production screen, drawn like the tile of a building or of a
 * technology: the name and whether it can be made, the factory, the resources of one unit one by
 * one, the time of a unit, the reasons it is closed, and, for an open recipe, the number of units
 * and the button. It is drawn again only when its own data changes, so a list of many recipes
 * stays light.
 *
 * @param recipe The recipe with its availability.
 * @param factoryName The name of the factory that makes it.
 * @param resourceGroupCode The code of the group of a resource, for its icon.
 * @param busy Whether a command is running.
 * @param onProduce What to do when the player starts the batch; it must be the same function between the draws.
 */
export const RecipeCard = memo(function RecipeCard({
  recipe,
  factoryName,
  resourceGroupCode,
  busy,
  onProduce,
}: {
  recipe: ProductionRecipe;
  factoryName: string;
  resourceGroupCode: (id: number) => string | null;
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
        <h3 className="building-card__name">{text(recipe.nameKey) ?? recipe.nameKey}</h3>
        {recipe.isAvailable ? <span className="badge">{t('production.available')}</span> : <span className="badge badge--warning">{t('production.closed')}</span>}
      </div>
      {factoryName ? <p className="meta">{t('production.madeAt', { name: factoryName })}</p> : null}
      <div className="building-card__next">
        <span className="civ-label">{t('build.requiredResources')}</span>
        <ul aria-label={t('build.requiredResources')} className="cost-list">
          {recipe.ingredients.map((ingredient) => (
            <li className="cost-list__row" key={`${ingredient.resourceTypeId ?? 'part'}-${ingredient.partTypeId ?? 'resource'}`}>
              {ingredient.resourceTypeId === null ? <span /> : <ResourceIcon groupCode={resourceGroupCode(ingredient.resourceTypeId)} />}
              <span className="cost-list__name">{text(ingredient.nameKey) ?? ingredient.nameKey}</span>
              <span className="num">{formatAmount(ingredient.amount, language)}</span>
              <span className="cost-list__missing" />
            </li>
          ))}
        </ul>
        <dl className="facts facts--compact">
          <dt>{t('production.duration')}</dt>
          <dd className="num">{formatDuration(language, recipe.unitSeconds)}</dd>
          {recipe.outputQuantity > 1 ? (
            <>
              <dt>{t('production.batchOutput')}</dt>
              <dd className="num">{t('production.pieces', { quantity: formatAmount(recipe.outputQuantity, language) })}</dd>
            </>
          ) : null}
        </dl>
      </div>
      {recipe.isAvailable ? (
        <>
          <TextField label={t('production.quantity')} type="number" min={1} step={1} value={typed} disabled={busy} onChange={(event) => setTyped(event.target.value)} />
          <button className="btn btn--secondary" type="button" disabled={busy || !valid} onClick={() => onProduce(recipe, quantity)}>
            {t('production.produce')}
          </button>
        </>
      ) : (
        <ul className="reason-list">
          {recipe.reasons.map((reason) => (
            <li key={`${reason.code}-${reason.targetId}`}>{reasonText(language, reason, text)}</li>
          ))}
        </ul>
      )}
    </li>
  );
});
