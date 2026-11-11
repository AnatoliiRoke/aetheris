import type { Fleet } from '../../../api/fleetApi';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { formatNumber } from '../planetText';
import { ClassChip, PlanetAvatar, StatePill } from './FleetBits';
import { FleetPlace } from './FleetPlace';
import { classCounts, stateText } from './fleetsParts';
import { useFleetsScene } from './useFleetsScene';

/**
 * The card of a fleet in the list (task 00040, mock-up `AETHERIS_fleets`): the name, the home body with
 * its picture, the place the fleet stands on, how many ships of which class it has, the commander, the
 * upkeep and the state. The card is one button: it selects the fleet, and the panel beside the list
 * shows it.
 *
 * @param fleet The fleet.
 * @param selected Whether the panel shows this fleet.
 * @param hasCredits Whether the civilization has credits.
 * @param onSelect What to do when the player chooses the card.
 */
export function FleetCard({
  fleet,
  selected,
  hasCredits,
  onSelect,
}: {
  fleet: Fleet;
  selected: boolean;
  hasCredits: boolean;
  onSelect: (fleetId: number) => void;
}) {
  const { t, language } = useI18n();
  const scene = useFleetsScene();

  return (
    <button aria-pressed={selected} className="fl-card" onClick={() => onSelect(fleet.id)} type="button">
      <span className="fl-card__name">{fleet.name}</span>
      <span className="fl-card__row">
        <PlanetAvatar planetId={fleet.homePlanetId} />
        <span>
          {t('fleets.home')}: <b>{scene.nameOf(fleet.homePlanetId)}</b>
        </span>
      </span>
      <span className="fl-card__row">
        <GameIcon name="planet" size={18} />
        <span>
          <FleetPlace compact fleet={fleet} />
        </span>
      </span>
      <span className="fl-card__chips">
        <b>{t('fleets.shipsCount', { count: fleet.ships.length })}</b>
        {classCounts(fleet.ships).map((entry) => (
          <ClassChip key={entry.nameKey} nameKey={entry.nameKey} suffix={`×${entry.count}`} />
        ))}
      </span>
      <span className="fl-card__meta">
        <span>
          {t('fleets.commander')}: {t('fleets.noCommanderShort')}
        </span>
        <span>
          {t('fleets.upkeep')}: <b>{t('fleets.upkeepValue', { amount: formatNumber(fleet.upkeepPerHour, language) })}</b>
        </span>
      </span>
      <StatePill tone={hasCredits ? 'ok' : 'warn'}>{stateText(language, t, fleet, hasCredits)}</StatePill>
    </button>
  );
}
