import { artImage, bodyArtClass } from '../../assets/images';
import type { OwnedPlanet } from '../../api/worldApi';
import { GameIcon, ResourceIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';

/**
 * The picture of the type of a body: the key of the picture comes from the reference table of
 * the world, so the client never maps a type to a picture on its own (task 00010, section 7.3).
 *
 * @param imageKey The key of the picture of the type.
 */
export function BodyArt({ imageKey, name }: { imageKey: string | null | undefined; name: string }) {
  const source = artImage(imageKey);

  return source ? (
    <img alt="" className="body-art__image" height={116} loading="lazy" src={source} width={116} />
  ) : (
    <span aria-hidden="true" className="body-art__placeholder">
      {name.slice(0, 1)}
    </span>
  );
}

/**
 * The deposits of a body: the resources it holds with the coefficient of their richness.
 *
 * @param planet The body.
 * @param names The names of the resources.
 * @param groups The codes of the groups of the resources.
 */
export function Deposits({
  planet,
  names,
  groups,
}: {
  planet: OwnedPlanet;
  names: (id: number) => string;
  groups: (id: number) => string | null;
}) {
  const { t } = useI18n();

  if (planet.deposits.length === 0) {
    return <span className="meta">{t('civ.depositsNone')}</span>;
  }

  return (
    <ul className="deposits">
      {planet.deposits.map((deposit) => {
        const name = names(deposit.resourceTypeId);

        return (
          <li className="deposits__item" key={deposit.resourceTypeId}>
            <ResourceIcon groupCode={groups(deposit.resourceTypeId)} />
            <span>{name}</span>
          </li>
        );
      })}
    </ul>
  );
}

/**
 * A bar of a fact of the body: the population, the energy or the storage.
 *
 * @param label The name of the fact.
 * @param value The text of the value.
 * @param percent How full the bar is, from 0 to 100.
 * @param tone The tone of the bar.
 * @param warning Whether the value is a warning.
 */
export function FactBar({
  label,
  value,
  percent,
  tone,
  warning = false,
}: {
  label: string;
  value: string;
  percent: number;
  tone: 'accent' | 'energy' | 'store';
  warning?: boolean;
}) {
  const filled = Math.max(0, Math.min(100, percent));

  return (
    <div className="bar-row">
      <div className="bar-row__head">
        <span>{label}</span>
        <b className={warning ? 'warn' : undefined}>{value}</b>
      </div>
      <div aria-label={label} aria-valuemax={100} aria-valuemin={0} aria-valuenow={Math.round(filled)} className="track" role="progressbar">
        <i className={warning ? 'f-stripe' : `f-${tone}`} style={{ width: `${filled}%` }} />
      </div>
    </div>
  );
}

/**
 * The identity of a body: the picture, the name, the type and the place in the galaxy.
 *
 * @param planet The body.
 * @param typeName The name of the type of the body.
 * @param typeImageKey The key of the picture of the type.
 * @param selected Whether the body is the chosen one.
 * @param badge What needs the attention of the player, shown on the picture, or `null`.
 * @param onSelect What to do when the body is chosen.
 */
export function BodyIdentity({
  planet,
  typeName,
  typeImageKey,
  selected,
  badge = null,
  onSelect,
}: {
  planet: OwnedPlanet;
  typeName: string;
  typeImageKey: string | null;
  selected: boolean;
  badge?: { tone: 'warning' | 'danger'; text: string } | null;
  onSelect: () => void;
}) {
  const { t } = useI18n();

  return (
    <div className="body-id">
      <div className={bodyArtClass(typeImageKey)}>
        <BodyArt imageKey={typeImageKey} name={planet.name ?? typeName} />
        {badge ? (
          <span className={`body-badge body-badge--${badge.tone}`} role="status">
            <GameIcon name="warn" size={13} />
            {badge.text}
          </span>
        ) : null}
        {selected ? <span className="body-sel">{t('civ.selected')}</span> : null}
      </div>
      <div className="body-meta">
        <h3>
          <button aria-pressed={selected} onClick={onSelect} type="button">
            {planet.name ?? t('civ.untitled')}
          </button>
        </h3>
        <span className="chip-type">{typeName}</span>
        <span className="coords">
          {t('civ.sector')} {planet.sectorName?.trim() ? planet.sectorName.trim() : planet.sectorId} · {t('civ.system')} {planet.systemName?.trim() ? planet.systemName.trim() : planet.systemId}
          {planet.orbit === null ? '' : `\n${t('civ.orbit')} ${planet.orbit}`}
        </span>
      </div>
    </div>
  );
}
