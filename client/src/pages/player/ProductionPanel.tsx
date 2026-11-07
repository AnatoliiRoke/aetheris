import { useEffect, useState } from 'react';
import { ApiError } from '../../api/http';
import type { ProductionStack, ProductionStorage } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { formatAmount, formatCoefficient, formatNumber, lookup } from './planetText';
import { useCharacteristics } from './useCharacteristics';
import { useProduction } from './useProduction';
import { useShellActions } from './useShell';

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['production_', 'planets_', 'requirements_'];

/** The condition of a whole part in per cent: only a whole capsule can be used. */
const fullCondition = 100;

/** A full bar of the storage. */
const fullPercent = 100;

/**
 * The production block of the planet screen (tasks 00013 and 00031): the stock of the parts of
 * the body with the use of a capsule of inhabitants, and the way to the production screen, where
 * the recipes, the filters and the factories are (task 00032). The page only shows the rules;
 * the server checks them on every command.
 *
 * @param planetId The body.
 * @param onChanged What to tell the page after a command, so the queue of the production reads again.
 * @param revision A number the page raises when something outside this panel changed the production, so the panel reads again.
 * @param showOpenLink Whether the way to the production screen stays in this block. The planet screen puts that link on the queue instead.
 */
export function ProductionPanel({
  planetId,
  onChanged,
  revision = 0,
  showOpenLink = false,
}: {
  planetId: number;
  onChanged?: () => void;
  revision?: number;
  showOpenLink?: boolean;
}) {
  const { t, language } = useI18n();
  const { production } = useApi();
  const { refreshAfterCommand } = useShellActions();
  const { stock, loaded, failed, reload } = useProduction(planetId);
  const [notice, setNotice] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (revision > 0) {
      void reload();
    }
  }, [revision, reload]);

  const spendCapsule = async (partTypeId: number) => {
    setBusy(true);
    setNotice(null);

    try {
      const used = await production.useCapsule(planetId, partTypeId);
      await reload();
      setNotice({ kind: 'success', text: t('production.capsuleUsed', { inhabitants: formatNumber(used.inhabitants, language) }) });
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

  return (
    <section className="panel" aria-label={t('production.title')}>
      {showOpenLink ? (
        <Link className="btn btn--secondary" to={routes.planetProduction}>
          {t('planet.chooseProduction')}
        </Link>
      ) : null}
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      {failed && !stock ? <Alert kind="error">{t('production.loadFailed')}</Alert> : null}
      {!loaded ? (
        <p className="lead" role="status">
          {t('production.loading')}
        </p>
      ) : (
        <StockPanel stacks={stock?.stacks ?? []} storage={stock?.storage ?? null} busy={busy} onUseCapsule={(partTypeId) => void spendCapsule(partTypeId)} />
      )}
    </section>
  );
}

/**
 * The stock of the parts of the body with the mark of the race and the weight of every part.
 */
function StockPanel({
  stacks,
  storage,
  busy,
  onUseCapsule,
}: {
  stacks: ProductionStack[];
  storage: ProductionStorage | null;
  busy: boolean;
  onUseCapsule: (partTypeId: number) => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const characteristics = useCharacteristics();
  const partName = (partTypeId: number) => {
    const stack = stacks.find((entry) => entry.partTypeId === partTypeId);

    return (stack ? text(stack.nameKey) : undefined) ?? t('production.part', { id: partTypeId });
  };
  const fullKinds = (storage?.kinds ?? []).filter((kind) => kind.isFull);

  const groups = partGroups(stacks);

  return (
    <div>
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
        <div className="storage-groups">
          {groups.map((group) => {
            const title = group.titleKey ? (text(group.titleKey) ?? group.titleKey) : t('planet.resourcesOther');

            return (
              <div className="storage-group" key={group.groupId}>
                <h4 className="storage-group__title">{title}</h4>
                <ul aria-label={title} className="storage-resources">
                  {group.stacks.map((stack) => (
                    <PartStockRow
                      busy={busy}
                      capacity={storage?.kinds.find((kind) => kind.partTypeId === stack.partTypeId)?.capacity ?? storage?.capacity ?? 0}
                      full={storage?.kinds.find((kind) => kind.partTypeId === stack.partTypeId)?.isFull === true}
                      key={`${stack.partTypeId}-${stack.markLevel}-${stack.hasRaceBonus}-${stack.conditionPercent}`}
                      onUseCapsule={onUseCapsule}
                      stack={stack}
                      weight={characteristics.weight(stack.partTypeId)}
                    />
                  ))}
                </ul>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

/**
 * The stacks of one group of parts, in the order the server sent them.
 */
function partGroups(stacks: ProductionStack[]): { groupId: number; titleKey: string | null; stacks: ProductionStack[] }[] {
  const groups: { groupId: number; titleKey: string | null; stacks: ProductionStack[] }[] = [];

  for (const stack of stacks) {
    const group = groups.find((candidate) => candidate.groupId === stack.partGroupId);

    if (group) {
      group.stacks.push(stack);
    } else {
      groups.push({ groupId: stack.partGroupId, titleKey: stack.partGroupNameKey, stacks: [stack] });
    }
  }

  return groups;
}

/**
 * One stack drawn as a row of the storage: the name, the bar and the amount against the room of
 * the kind. The first mark and the bonus of the race are not written. A higher mark and the weight stay.
 */
function PartStockRow({
  stack,
  capacity,
  full,
  weight,
  busy,
  onUseCapsule,
}: {
  stack: ProductionStack;
  capacity: number;
  full: boolean;
  weight: number | null;
  busy: boolean;
  onUseCapsule: (partTypeId: number) => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const name = text(stack.nameKey) ?? t('production.part', { id: stack.partTypeId });
  const percent = capacity > 0 ? Math.min(fullPercent, Math.round((stack.quantity / capacity) * fullPercent)) : 0;
  const capsule = stack.capsuleInhabitants > 0 && stack.conditionPercent >= fullCondition;

  return (
    <li className="storage-resource storage-resource--part">
      <span className="storage-resource__name">
        <span>{name}</span>
        {stack.markLevel > 1 ? <span className="num">{t('production.mark', { level: markName(stack.markLevel) })}</span> : null}
      </span>
      <div aria-label={name} aria-valuemax={fullPercent} aria-valuemin={0} aria-valuenow={percent} className="track" role="progressbar">
        <i className={full ? 'f-stripe' : 'f-store'} style={{ width: `${percent}%` }} />
      </div>
      <span className="storage-resource__value num">
        {full ? <span className="badge badge--warning">{t('planet.storageFullBadge')}</span> : null}
        {capacity > 0 ? (
          <span>{t('planet.storageAmount', { amount: formatAmount(stack.quantity, language), capacity: formatAmount(capacity, language) })}</span>
        ) : (
          <span title={t('planet.storageNoCapacityHint')}>{t('planet.storageNoCapacity', { amount: formatAmount(stack.quantity, language) })}</span>
        )}
        {weight === null ? null : (
          <span className="storage-stock__weight">
            {formatCoefficient(weight, language)} {t('storage.weightUnit')}
          </span>
        )}
      </span>
      {capsule ? (
        <span className="storage-resource__action">
          {stack.capsuleUsable ? (
            <button type="button" className="btn btn--secondary" disabled={busy} onClick={() => onUseCapsule(stack.partTypeId)}>
              {t('production.useCapsule', { inhabitants: formatNumber(stack.capsuleInhabitants, language) })}
            </button>
          ) : (
            <span className="badge">{t('production.capsuleOtherRace')}</span>
          )}
        </span>
      ) : null}
    </li>
  );
}

/**
 * The mark level of a part as the player sees it: the first, the second, the third.
 */
function markName(level: number): string {
  const names: Record<number, string> = { 1: 'I', 2: 'II', 3: 'III' };

  return names[level] ?? String(level);
}
