import { GameIcon } from '../../../components/GameIcon';
import { marketVisibilities, type MarketLevel } from '../../../api/marketApi';
import { useI18n } from '../../../i18n/useI18n';
import { levelLabelKeys } from './levelLabels';

/**
 * The switch of the level of the market: the clan level is shown but not open while there are no
 * clans, and says why in its hint; the fee of the chosen level stands beside it.
 *
 * @param levels The levels of the market in their order.
 * @param selectedId The chosen level.
 * @param onPick What to do when the player picks a level.
 */
export function LevelSwitch({ levels, selectedId, onPick }: { levels: MarketLevel[]; selectedId: number | null; onPick: (levelId: number) => void }) {
  const { t } = useI18n();
  const selected = levels.find((level) => level.id === selectedId) ?? null;

  return (
    <div className="mk-levels">
      <div aria-label={t('market.levelAria')} className="mk-levels__group" role="group">
        {levels.map((level) => {
          const label = t(levelLabelKeys[level.visibility] ?? 'market.lvSector');

          if (!level.isActive) {
            const tip = level.visibility === marketVisibilities.clan ? t('market.clanTip') : t('market.soonLevel');

            return (
              <span key={level.id} title={tip}>
                <button aria-disabled="true" aria-label={`${label}: ${tip}`} className="mk-level mk-level--off" disabled type="button">
                  <GameIcon name="lock" size={16} />
                  {label}
                </button>
              </span>
            );
          }

          return (
            <button aria-pressed={level.id === selectedId} className="mk-level" key={level.id} onClick={() => onPick(level.id)} type="button">
              {label}
            </button>
          );
        })}
      </div>
      {selected ? <span className="mk-levels__fee">{t('market.feeLine', { percent: selected.commissionPercent })}</span> : null}
    </div>
  );
}
