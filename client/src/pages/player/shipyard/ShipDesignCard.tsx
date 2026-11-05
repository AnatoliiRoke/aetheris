import { memo } from 'react';
import type { ShipDesign, ShipHull, ShipModule } from '../../../api/fleetApi';
import { CardArt } from '../../../components/CardArt';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatAmount } from '../planetText';
import { installedModules, designProblem } from './shipyardParts';

/**
 * The card of a project of a ship on the design tab of the shipyard (task 00038): the picture of
 * the hull, the name of the project, the hull and its class, the list of the modules with their
 * mark and how many slots they take, the characteristics the modules give and the actions.
 *
 * @param design The project.
 * @param hull The hull of the project, or `null` when the catalog does not have it.
 * @param modules The modules of the catalog.
 * @param busy Whether a command is running.
 * @param onEdit What to do when the player edits the project.
 * @param onAssembly What to do when the player sends the project to the assembly.
 * @param onArchive What to do when the player archives the project.
 */
export const ShipDesignCard = memo(function ShipDesignCard({
  design,
  hull,
  modules,
  busy,
  onEdit,
  onAssembly,
  onArchive,
}: {
  design: ShipDesign;
  hull: ShipHull | null;
  modules: ShipModule[];
  busy: boolean;
  onEdit: (design: ShipDesign) => void;
  onAssembly: (design: ShipDesign) => void;
  onArchive: (design: ShipDesign) => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();

  const hullName = hull ? (text(hull.nameKey) ?? hull.nameKey) : '';
  const hullClass = hull ? (text(hull.partGroupNameKey) ?? hull.partGroupNameKey) : '';
  const installed = installedModules(design.slots, modules);
  const summary = design.summary;
  const problem = designProblem(summary, t, language);

  return (
    <article className="sh-card">
      {hull ? <CardArt imageKey={hull.imageKey} label={hullName} /> : null}

      <div className="sh-card__head">
        <div>
          <h3 className="sh-card__title">{design.name}</h3>
          <p className="sh-card__hull">{t('shipyard.hullOf', { hull: hullName, class: hullClass })}</p>
        </div>
        <div className="sh-card__badges">
          {problem ? (
            <span className="badge badge--warning" title={problem}>
              {t('shipyard.needsFix')}
            </span>
          ) : null}
          {design.isArchived ? <span className="badge badge--warning">{t('shipyard.archived')}</span> : null}
        </div>
      </div>

      <section aria-labelledby={`sh-modules-${design.id}`}>
        <span className="civ-label" id={`sh-modules-${design.id}`}>
          {t('shipyard.installedModules')}
        </span>
        {installed.length === 0 ? (
          <p className="sh-note">{t('shipyard.noModules')}</p>
        ) : (
          <ul className="sh-modules">
            {installed.map((entry) => (
              <li className="sh-modules__row" key={entry.module.id}>
                <span className="sh-modules__name">{text(entry.module.nameKey) ?? entry.module.nameKey}</span>
                <span className="sh-modules__mark">{t('shipyard.mark', { mark: entry.markLevel })}</span>
                {entry.count > 1 ? <span className="sh-modules__count">×{entry.count}</span> : null}
              </li>
            ))}
          </ul>
        )}
      </section>

      <dl className="sh-facts">
        <div>
          <dt className="sh-facts__term">{t('shipyard.mass')}</dt>
          <dd className="sh-facts__value">{formatAmount(summary.weight, language)} т</dd>
        </div>
        <div>
          <dt className="sh-facts__term">{t('shipyard.slotsUsed')}</dt>
          <dd className="sh-facts__value">
            {summary.usedSlots} / {summary.totalSlots}
          </dd>
        </div>
        <div>
          <dt className="sh-facts__term">{t('shipyard.energy')}</dt>
          <dd className="sh-facts__value">
            {formatAmount(summary.energyUse, language)} / {formatAmount(summary.energyPower, language)} МВт
          </dd>
        </div>
        <div>
          <dt className="sh-facts__term">{t('shipyard.hold')}</dt>
          <dd className="sh-facts__value">{formatAmount(summary.holdCapacity, language)} т</dd>
        </div>
        <div>
          <dt className="sh-facts__term">{t('shipyard.crew')}</dt>
          <dd className="sh-facts__value">{formatAmount(summary.crew, language)}</dd>
        </div>
        <div>
          <dt className="sh-facts__term">{t('shipyard.drivePush')}</dt>
          <dd className="sh-facts__value">{formatAmount(summary.drivePush, language)} кН</dd>
        </div>
        <div>
          <dt className="sh-facts__term">{t('shipyard.integrity')}</dt>
          <dd className="sh-facts__value">{formatAmount(summary.integrity, language)}</dd>
        </div>
        <div>
          <dt className="sh-facts__term">{t('shipyard.shield')}</dt>
          <dd className="sh-facts__value">{formatAmount(summary.shieldCapacity, language)}</dd>
        </div>
        <div>
          <dt className="sh-facts__term">{t('shipyard.damage')}</dt>
          <dd className="sh-facts__value">
            {formatAmount(summary.energyDamage, language)} {t('shipyard.energyDamage')} · {formatAmount(summary.kineticDamage, language)}{' '}
            {t('shipyard.kineticDamage')}
          </dd>
        </div>
      </dl>

      <div className="sh-card__actions">
        <button className="btn btn--secondary" disabled={busy} onClick={() => onEdit(design)} type="button">
          {t('shipyard.edit')}
        </button>
        <button className="btn btn--primary" disabled={busy} onClick={() => onAssembly(design)} type="button">
          {t('shipyard.toAssembly')}
        </button>
        <button className="btn btn--danger" disabled={busy} onClick={() => onArchive(design)} type="button">
          {t('shipyard.archive')}
        </button>
      </div>
    </article>
  );
});
