import { useCallback, useMemo } from 'react';
import { useApi } from '../../../api/useApi';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { useRead } from '../market/useRead';
import { formatNumber } from '../planetText';
import { useShellSelector } from '../useShell';
import { cargoAmount, cargoKey, emptyCargoItem, holdMass, maxLoadable, type CargoDraftItem, type HoldContent } from './flightPlanCargo';

/**
 * One thing the player may choose to carry: what it is, how much there is of it where it comes from, and what
 * one unit weighs (task 00057).
 */
interface CargoOption {
  key: string;
  item: Pick<CargoDraftItem, 'kind' | 'id' | 'markLevel' | 'hasRaceBonus'>;
  label: string;
  /** How much there is of it, or `null` when the window does not know. */
  available: number | null;
  /** The weight of one unit, in tonnes. */
  weight: number;
  /** Whether the stock has none of it: the entry is shown, but it cannot be chosen (task 00072). */
  missing?: boolean;
}

/**
 * The neutral code of the group of the resources that are the fuel of the drives (task 00072). The
 * fuel is shown in the list of a load even when the stock has none of it, so the player sees that
 * it is missing; the same code names the group on the server.
 */
const fuelGroupCode = 'resource_group_3';

/**
 * The settings of an order of a load or an unload (task 00057). They are shown only for these two types.
 *
 * A load takes from the stock of the body the fleet stands on: the list is what the body holds, resources and
 * modules, and when the window does not know the body the list is every resource. An unload puts out of the
 * holds: the list is what the holds carry now, or are meant to carry after the loads before the order. The
 * amount of every item is obligatory. A load is checked against the room that is left in the holds.
 *
 * @param id The identifier of the order; the fields of the order are named after it.
 * @param typeCode `load` or `unload`.
 * @param items The items of the order.
 * @param planetId The body the order works on, or `null` when the window does not know it.
 * @param content What the holds carry before the order.
 * @param capacity The room of the holds of the fleet, in tonnes, or `null` when it is not read yet.
 * @param partNames The localisation keys of the names of the modules, by the kind of the module.
 * @param partWeights The weight of one module, by the kind, in tonnes.
 * @param resourceWeights The weight of one unit of a resource, by the resource, in tonnes, as the data of the resource says.
 * @param onChange Answers the new items of the order.
 */
export function FlightCargoFields({
  id,
  typeCode,
  items,
  planetId,
  content,
  capacity,
  partNames,
  partWeights,
  resourceWeights,
  onChange,
}: {
  id: string;
  typeCode: 'load' | 'unload';
  items: readonly CargoDraftItem[];
  planetId: number | null;
  content: HoldContent;
  capacity: number | null;
  partNames: ReadonlyMap<number, string>;
  partWeights: ReadonlyMap<number, number>;
  resourceWeights: ReadonlyMap<number, number>;
  onChange: (items: CargoDraftItem[]) => void;
}) {
  const { t, language } = useI18n();
  const { planets, production } = useApi();
  const { name } = useTexts();
  const reference = useShellSelector((state) => state.reference);
  const isLoad = typeCode === 'load';

  // The stock of the body: its resources and its modules. They are read once for the order, only for a load.
  const loadPlanet = useCallback(() => (planetId === null ? Promise.resolve(null) : planets.planet(planetId)), [planets, planetId]);
  const planetRead = useRead(loadPlanet, isLoad && planetId !== null, false);
  const loadStock = useCallback(() => (planetId === null ? Promise.resolve(null) : production.stock(planetId)), [production, planetId]);
  const stockRead = useRead(loadStock, isLoad && planetId !== null, false);

  const resourceNames = useMemo(() => new Map((reference?.resources ?? []).map((resource) => [resource.id, resource.nameKey])), [reference]);

  // The weight of a unit of a thing comes from its data: a resource from the resource, a module from its kind.
  const weightOf = useCallback(
    (kind: 'resource' | 'part', thingId: number) => (kind === 'resource' ? (resourceWeights.get(thingId) ?? 0) : (partWeights.get(thingId) ?? 0)),
    [partWeights, resourceWeights],
  );

  const options = useMemo<CargoOption[]>(() => {
    const list: CargoOption[] = [];
    const resourceLabel = (resourceId: number) => name(resourceNames.get(resourceId), String(resourceId));
    const partLabel = (partTypeId: number, mark: number, bonus: boolean) =>
      `${name(partNames.get(partTypeId), String(partTypeId))} · ${t('shipyard.mark', { mark })}${bonus ? ' ★' : ''}`;

    if (isLoad) {
      const planet = planetRead.data;
      const stock = stockRead.data;

      if (planet) {
        for (const resource of planet.resources) {
          if (resource.amount >= 1) {
            list.push({
              key: `r:${resource.resourceTypeId}`,
              item: { kind: 'resource', id: resource.resourceTypeId, markLevel: 1, hasRaceBonus: false },
              label: resourceLabel(resource.resourceTypeId),
              available: Math.floor(resource.amount),
              weight: resourceWeights.get(resource.resourceTypeId) ?? resource.weight ?? 0,
            });
          }
        }

        // The fuel the stock has none of is shown too, so the player sees that it is missing (task 00072).
        for (const resource of reference?.resources ?? []) {
          if (resource.groupCode === fuelGroupCode && !list.some((option) => option.key === `r:${resource.id}`)) {
            list.push({
              key: `r:${resource.id}`,
              item: { kind: 'resource', id: resource.id, markLevel: 1, hasRaceBonus: false },
              label: resourceLabel(resource.id),
              available: 0,
              weight: weightOf('resource', resource.id),
              missing: true,
            });
          }
        }
      } else if (planetId === null) {
        // The body is not known (the fleet flies to a place the window cannot name): every resource may be named.
        for (const resource of reference?.resources ?? []) {
          list.push({
            key: `r:${resource.id}`,
            item: { kind: 'resource', id: resource.id, markLevel: 1, hasRaceBonus: false },
            label: resourceLabel(resource.id),
            available: null,
            weight: weightOf('resource', resource.id),
          });
        }
      }

      for (const stack of stock?.stacks ?? []) {
        // Only new parts leave the stock for a hold, and a capsule of inhabitants is not a module.
        if (stack.conditionPercent < 100 || stack.capsuleInhabitants > 0) {
          continue;
        }

        const key = `p:${stack.partTypeId}:${stack.markLevel}:${stack.hasRaceBonus ? 1 : 0}`;
        const existing = list.find((option) => option.key === key);

        if (existing) {
          existing.available = (existing.available ?? 0) + stack.quantity;
          continue;
        }

        list.push({
          key,
          item: { kind: 'part', id: stack.partTypeId, markLevel: stack.markLevel, hasRaceBonus: stack.hasRaceBonus },
          label: `${name(stack.nameKey, String(stack.partTypeId))} · ${t('shipyard.mark', { mark: stack.markLevel })}${stack.hasRaceBonus ? ' ★' : ''}`,
          available: stack.quantity,
          weight: partWeights.get(stack.partTypeId) ?? 0,
        });
      }
    } else {
      for (const entry of content.values()) {
        list.push({
          key: cargoKey(entry),
          item: entry,
          label: entry.kind === 'resource' ? resourceLabel(entry.id) : partLabel(entry.id, entry.markLevel, entry.hasRaceBonus),
          available: entry.amount,
          weight: weightOf(entry.kind, entry.id),
        });
      }
    }

    // A thing the order already names stays in the list, even when its source has none of it now.
    for (const item of items) {
      if (item.id === null) {
        continue;
      }

      const key = cargoKey(item);

      if (!list.some((option) => option.key === key)) {
        list.push({
          key,
          item,
          label: item.kind === 'resource' ? resourceLabel(item.id) : partLabel(item.id, item.markLevel, item.hasRaceBonus),
          available: 0,
          weight: weightOf(item.kind, item.id),
        });
      }
    }

    return list;
  }, [content, isLoad, items, name, partNames, partWeights, planetId, planetRead.data, reference, resourceNames, resourceWeights, stockRead.data, t, weightOf]);

  const byKey = useMemo(() => new Map(options.map((option) => [option.key, option])), [options]);
  const resources = options.filter((option) => option.item.kind === 'resource');
  const parts = options.filter((option) => option.item.kind === 'part');

  // The weight of the load against the room that is left in the holds.
  const mass = useMemo(() => {
    let sum = 0;

    for (const item of items) {
      const amount = cargoAmount(item);

      if (item.id !== null && amount !== null) {
        sum += amount * (byKey.get(cargoKey(item))?.weight ?? 0);
      }
    }

    return sum;
  }, [byKey, items]);
  const carried = useMemo(() => holdMass(content, (entry) => weightOf(entry.kind, entry.id)), [content, weightOf]);
  const free = capacity === null ? null : Math.max(0, capacity - carried);
  const overflow = isLoad && free !== null && mass > free;

  const change = (index: number, patch: Partial<CargoDraftItem>) => {
    onChange(items.map((item, place) => (place === index ? { ...item, ...patch } : item)));
  };

  const choose = (index: number, key: string) => {
    const option = byKey.get(key);

    if (option) {
      change(index, { kind: option.item.kind, id: option.item.id, markLevel: option.item.markLevel, hasRaceBonus: option.item.hasRaceBonus });
    }
  };

  return (
    <div className="pl-cargo">
      {isLoad && planetId === null ? <p className="fl-note">{t('plan.cargo.planetUnknown')}</p> : null}
      {items.length === 0 ? <p className="fl-note">{t(isLoad ? 'plan.cargo.emptyLoad' : 'plan.cargo.emptyUnload')}</p> : null}
      {isLoad ? <p className="fl-note">{t('plan.cargo.partialHint')}</p> : null}

      <ul className="pl-cargo__items">
        {items.map((item, index) => {
          const key = item.id === null ? '' : cargoKey(item);
          const option = byKey.get(key);
          const available = option?.available ?? null;

          // How much of the thing still fits: the room left by the other items of the load, by the weight of a unit.
          const itemAmount = cargoAmount(item) ?? 0;
          const fit =
            isLoad && free !== null && option !== undefined ? maxLoadable(free - (mass - itemAmount * option.weight), option.weight, available) : null;

          return (
            <li className="pl-cargo__item" key={index}>
              <select
                aria-label={t('plan.cargo.thing')}
                className="fl-input"
                id={`plan-cargo-${id}-${index}`}
                onChange={(event) => choose(index, event.target.value)}
                value={key}
              >
                <option value="">{t('plan.cargo.choose')}</option>
                {resources.length > 0 ? (
                  <optgroup label={t('plan.cargo.resources')}>
                    {resources.map((entry) => (
                      <option disabled={entry.missing === true} key={entry.key} value={entry.key}>
                        {entry.missing === true ? `${entry.label} — ${t('plan.cargo.notInStock')}` : entry.label}
                      </option>
                    ))}
                  </optgroup>
                ) : null}
                {parts.length > 0 ? (
                  <optgroup label={t('plan.cargo.parts')}>
                    {parts.map((entry) => (
                      <option key={entry.key} value={entry.key}>
                        {entry.label}
                      </option>
                    ))}
                  </optgroup>
                ) : null}
              </select>
              <input
                aria-label={t('plan.cargo.amount')}
                className="fl-input pl-cargo__amount"
                inputMode="numeric"
                min={1}
                onChange={(event) => change(index, { amount: event.target.value })}
                placeholder={t('plan.cargo.amount')}
                type="number"
                value={item.amount}
              />
              <span className="pl-cargo__available">
                {available === null ? '' : t('plan.cargo.available', { amount: formatNumber(available, language) })}
                {fit === null ? '' : ` · ${t('plan.cargo.fits', { amount: formatNumber(fit, language) })}`}
              </span>
              <button
                aria-label={t('plan.cargo.remove')}
                className="btn fl-btn fl-btn--small pl-tool fl-btn--danger-solid"
                onClick={() => onChange(items.filter((_, place) => place !== index))}
                type="button"
              >
                <GameIcon name="close" size={14} />
              </button>
            </li>
          );
        })}
      </ul>

      <button className="btn fl-btn fl-btn--small" onClick={() => onChange([...items, emptyCargoItem()])} type="button">
        <GameIcon name="mapPlus" size={16} />
        {t('plan.cargo.add')}
      </button>

      {isLoad && free !== null && mass > 0 ? (
        <p className="fl-note">
          {overflow
            ? t('plan.cargo.overflow', { mass: formatNumber(mass, language), free: formatNumber(free, language) })
            : t('plan.cargo.mass', { mass: formatNumber(mass, language), free: formatNumber(free, language) })}
        </p>
      ) : null}
    </div>
  );
}
