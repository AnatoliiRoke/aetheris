import { useMemo } from 'react';
import type { ShipyardCost } from '../../../api/fleetApi';
import { ResourceIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatAmount } from '../planetText';
import { orderCatalogCosts } from '../productionStock';
import { useResourceGroups } from '../useResourceGroups';
import { useWorldReference } from '../useWorldReference';

/**
 * The resources an assembly takes, in the same list the building and recipe plaques use:
 * the name, how many the assembly needs and how many the player has.
 *
 * @param costs The resources of the assembly. A row without `available` shows only the amount.
 */
export function AssemblyCosts({ costs }: { costs: readonly ShipyardCost[] }) {
  const { t, language } = useI18n();
  const { name } = useTexts();
  const reference = useWorldReference();
  const resourceGroupCode = useResourceGroups(reference);
  const codeByResourceId = useMemo(
    () => new Map((reference?.resources ?? []).map((resource) => [resource.id, resource.code])),
    [reference],
  );
  const ordered = orderCatalogCosts(costs, (resourceTypeId) => codeOf(resourceTypeId, codeByResourceId, costs));

  if (ordered.length === 0) {
    return null;
  }

  return (
    <div className="sh-costs">
      <span className="civ-label">{t('build.requiredResources')}</span>
      <ul aria-label={t('build.requiredResources')} className="cost-list cost-list--stock">
        {ordered.map((cost) => {
          const onHand = typeof cost.available === 'number' ? cost.available : null;
          const isShort = onHand !== null && cost.amount > onHand;

          return (
            <li className={isShort ? 'cost-list__row cost-list__row--short' : 'cost-list__row'} key={cost.resourceTypeId}>
              <ResourceIcon groupCode={resourceGroupCode(cost.resourceTypeId)} />
              <span className="cost-list__name">{name(cost.nameKey)}</span>
              <span className="num cost-list__amount">
                {formatAmount(cost.amount, language)}
                {onHand === null ? null : (
                  <>
                    {' / '}
                    <span className="cost-list__stock">{formatAmount(onHand, language)}</span>
                    {isShort ? <span className="cost-list__missing"> ({formatAmount(cost.amount - onHand, language)})</span> : null}
                  </>
                )}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function codeOf(
  resourceTypeId: number,
  codeByResourceId: ReadonlyMap<number, string>,
  costs: readonly ShipyardCost[],
): string | null {
  const catalogCode = codeByResourceId.get(resourceTypeId);

  if (catalogCode) {
    return catalogCode;
  }

  const nameKey = costs.find((cost) => cost.resourceTypeId === resourceTypeId)?.nameKey ?? '';
  const matched = /resource_\d+/.exec(nameKey);

  return matched?.[0] ?? null;
}
