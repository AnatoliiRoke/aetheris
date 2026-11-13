import { useMemo } from 'react';
import type { FlightFuelEstimate } from '../../../api/fleetApi';
import { ResourceIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatNumber } from '../planetText';
import { useShellSelector } from '../useShell';

/**
 * The block of the fuel of a route (task 00072): what every resource of the route costs, what the
 * hold of the fleet holds now, and the resources the fleet does not have enough of.
 *
 * The block is shown with the plan of the fleet, so the player sees the shortage before the fleet
 * leaves and the button «Send» stays dark.
 *
 * @param estimate The estimate of the fuel of the route.
 */
export function FlightFuelBlock({ estimate }: { estimate: FlightFuelEstimate | null }) {
  const { t, language } = useI18n();
  const { name } = useTexts();
  const reference = useShellSelector((state) => state.reference);
  const resources = useMemo(() => new Map((reference?.resources ?? []).map((resource) => [resource.id, resource])), [reference]);

  const rows = useMemo(
    () =>
      (estimate?.fuelNeed ?? []).map((item) => {
        const resource = resources.get(item.resourceTypeId);

        return {
          resourceTypeId: item.resourceTypeId,
          amount: item.amount,
          have: item.have,
          short: item.amount > item.have,
          groupCode: resource?.groupCode ?? null,
          label: name(resource?.nameKey, String(item.resourceTypeId)),
        };
      }),
    [estimate, name, resources],
  );

  if (estimate === null) {
    return null;
  }

  if (rows.length === 0) {
    return (
      <div className="pl-propellant">
        <span className="fl-label">{t('plan.propellant.title')}</span>
        <p className="fl-note">{t('plan.propellant.none')}</p>
      </div>
    );
  }

  return (
    <div className="pl-propellant">
      <span className="fl-label">{t('plan.propellant.title')}</span>
      <ul className="pl-propellant__list">
        {rows.map((row) => (
          <li className={row.short ? 'pl-propellant__item pl-propellant__item--short' : 'pl-propellant__item'} key={row.resourceTypeId}>
            <span className="pl-propellant__name">
              <ResourceIcon groupCode={row.groupCode} />
              <span>{row.label}</span>
            </span>
            <span className="pl-propellant__amount">
              {t('plan.propellant.need', { amount: formatNumber(row.amount, language), have: formatNumber(row.have, language) })}
            </span>
          </li>
        ))}
      </ul>
      {estimate.shortages.length > 0 ? <p className="pl-error">{t('plan.propellant.short')}</p> : null}
    </div>
  );
}
