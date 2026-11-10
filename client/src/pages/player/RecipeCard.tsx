import { memo, useState } from 'react';
import type { ProductionRecipe } from '../../api/productionApi';
import { CardArt } from '../../components/CardArt';
import { TextField } from '../../components/FormField';
import { ResourceIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { formatAmount, formatDuration } from './planetText';
import { reasonText } from './productionRecipes';
import { type BodyAvailability, ingredientAvailability, outputQuantity } from './productionStock';
import { CharacteristicList } from './StorageBlocks';
import { useCharacteristics } from './useCharacteristics';

/**
 * How many characteristics of a part a plaque shows (task 00049). The rows come in the order of
 * the group of the part, so the first ones are the key ones.
 */
const keyCharacteristics = 4;

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
 * @param availability What the body has: the balances of the civilization, the resources of the body and the stock of its parts.
 * @param busy Whether a command is running.
 * @param onProduce What to do when the player starts the batch; it must be the same function between the draws.
 */
export const RecipeCard = memo(function RecipeCard({
  recipe,
  factoryName,
  resourceGroupCode,
  availability,
  busy,
  onProduce,
}: {
  recipe: ProductionRecipe;
  factoryName: string;
  resourceGroupCode: (id: number) => string | null;
  availability: BodyAvailability;
  busy: boolean;
  onProduce: (recipe: ProductionRecipe, quantity: number) => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const characteristics = useCharacteristics();
  const [typed, setTyped] = useState('1');
  const quantity = Number(typed);
  const valid = Number.isInteger(quantity) && quantity > 0;
  // The ingredients of the batch of the number the player chose, with what the body has.
  const ingredients = ingredientAvailability(recipe, valid ? quantity : 0, availability);
  // The amount of the part or of the resource on the stock of the body and the key characteristics
  // of the part (task 00049). The server leaves out a field that is null, so a recipe of a resource
  // has no part at all: neither null nor undefined.
  const partId = recipe.partTypeId ?? null;
  const inStock = outputQuantity(availability, recipe);
  const characteristicRows = partId === null ? [] : characteristics.rows(partId).slice(0, keyCharacteristics);

  return (
    <li className="building-card">
      <CardArt imageKey={recipe.imageKey} label={text(recipe.nameKey) ?? recipe.nameKey} />
      <div className="building-card__head">
        <h3 className="building-card__name">{text(recipe.nameKey) ?? recipe.nameKey}</h3>
        {recipe.isAvailable ? <span className="badge">{t('production.available')}</span> : <span className="badge badge--warning">{t('production.closed')}</span>}
      </div>
      {factoryName ? <p className="meta">{t('production.madeAt', { name: factoryName })}</p> : null}
      {inStock === null ? null : (
        <p className="production-card__stock meta">
          {t('production.inStorage', { quantity: formatAmount(inStock, language) })}
        </p>
      )}
      {characteristicRows.length > 0 ? <CharacteristicList language={language} rows={characteristicRows} /> : null}
      <div className="building-card__next">
        <span className="civ-label">{t('build.requiredResources')}</span>
        <ul aria-label={t('build.requiredResources')} className="cost-list cost-list--stock">
          {ingredients.map((entry) => {
            const { ingredient, required, available, missing } = entry;
            const short = missing > 0;

            return (
              <li className={short ? 'cost-list__row cost-list__row--short' : 'cost-list__row'} key={`${ingredient.resourceTypeId ?? 'part'}-${ingredient.partTypeId ?? 'resource'}`}>
                {ingredient.resourceTypeId === null ? <span /> : <ResourceIcon groupCode={resourceGroupCode(ingredient.resourceTypeId)} />}
                <span className="cost-list__name">{text(ingredient.nameKey) ?? ingredient.nameKey}</span>
                <span className="num cost-list__amount">
                  {formatAmount(required, language)}
                  {' / '}
                  <span className="cost-list__stock">{formatAmount(available, language)}</span>
                  {short ? <span className="cost-list__missing"> ({formatAmount(missing, language)})</span> : null}
                </span>
              </li>
            );
          })}
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
