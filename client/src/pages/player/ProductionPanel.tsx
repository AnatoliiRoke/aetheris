import { useEffect, useState } from 'react';
import { ApiError } from '../../api/http';
import type { ProductionStack, ProductionStorage } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { formatCoefficient, formatNumber, lookup } from './planetText';
import { CharacteristicList } from './StorageBlocks';
import { useCharacteristics } from './useCharacteristics';
import { useProduction } from './useProduction';
import { useShellActions } from './useShell';

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['production_', 'planets_', 'requirements_'];

/** The condition of a whole part in per cent: only a whole capsule can be used. */
const fullCondition = 100;

/**
 * The production block of the planet screen (tasks 00013 and 00031): the stock of the parts of
 * the body with the use of a capsule of inhabitants, and the way to the production screen, where
 * the recipes, the filters and the factories are (task 00032). The page only shows the rules;
 * the server checks them on every command.
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
    <section className="panel" aria-labelledby="production-title">
      <div className="panel__head">
        <h2 className="panel__title" id="production-title">
          {t('production.title')}
        </h2>
        <Link className="btn btn--secondary" to={routes.planetProduction}>
          {t('planet.chooseProduction')}
        </Link>
      </div>
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
          {stacks.map((stack) => {
            const weight = characteristics.weight(stack.partTypeId);
            const rows = characteristics.rows(stack.partTypeId);

            return (
              <li key={`${stack.partTypeId}-${stack.markLevel}-${stack.hasRaceBonus}-${stack.conditionPercent}`} className="storage-stock">
                <div className="storage-row">
                  <span>{text(stack.nameKey) ?? t('production.part', { id: stack.partTypeId })}</span>
                  <span className="num">
                    {t('production.mark', { level: markName(stack.markLevel) })}
                  </span>
                  {stack.hasRaceBonus ? <span className="badge">{t('production.raceBonus')}</span> : null}
                  <span className="num storage-stock__weight">
                    {formatNumber(stack.quantity, language)}
                    {weight === null ? null : (
                      <span className="meta">
                        {formatCoefficient(weight, language)} {t('storage.weightUnit')}
                      </span>
                    )}
                  </span>
                  {stack.capsuleInhabitants > 0 && stack.conditionPercent >= fullCondition ? (
                    stack.capsuleUsable ? (
                      <button type="button" className="btn btn--secondary" disabled={busy} onClick={() => onUseCapsule(stack.partTypeId)}>
                        {t('production.useCapsule', { inhabitants: formatNumber(stack.capsuleInhabitants, language) })}
                      </button>
                    ) : (
                      <span className="badge">{t('production.capsuleOtherRace')}</span>
                    )
                  ) : null}
                </div>
                {rows.length > 0 ? <CharacteristicList language={language} rows={rows} /> : null}
              </li>
            );
          })}
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
