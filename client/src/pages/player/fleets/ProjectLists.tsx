import { useMemo } from 'react';
import type { FleetShip } from '../../../api/fleetApi';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { ClassChip } from './FleetBits';
import { groupByClass } from './fleetsParts';

/**
 * The ships grouped as the screen of the fleets shows them: the class of the hull, and under it the
 * projects with how many ships each has. The ships are not listed one by one.
 *
 * @param ships The ships.
 */
export function ProjectList({ ships }: { ships: readonly FleetShip[] }) {
  const { t } = useI18n();
  const { text } = useTexts();
  const groups = useMemo(() => groupByClass(ships), [ships]);

  return (
    <div className="fl-groups">
      {groups.map((group) => (
        <section className="fl-group" key={group.classNameKey}>
          <h4 className="fl-group__head">
            <ClassChip nameKey={group.classNameKey} />
          </h4>
          <ul className="fl-projects">
            {group.projects.map((project) => (
              <li className="fl-project" key={project.designId}>
                <span className="fl-project__text">
                  <span className="fl-project__name">{project.name}</span>
                  <span className="fl-project__sub">{text(project.hullNameKey) ?? project.hullNameKey}</span>
                </span>
                <b className="fl-project__count">{t('fleets.projectCount', { count: project.ships.length })}</b>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

/**
 * The same groups with a field for the number of ships of each project that the player takes. The field
 * holds a whole number from zero to the number of ships the project has.
 *
 * @param ships The ships the player picks from.
 * @param quantities How many ships of each project are taken, by the number of the project.
 * @param onChange Called with a project and the number the player typed.
 */
export function ProjectQuantities({
  ships,
  quantities,
  onChange,
}: {
  ships: readonly FleetShip[];
  quantities: ReadonlyMap<number, number>;
  onChange: (designId: number, quantity: number) => void;
}) {
  const { t } = useI18n();
  const { text } = useTexts();
  const groups = useMemo(() => groupByClass(ships), [ships]);

  return (
    <div className="fl-groups">
      {groups.map((group) => (
        <section className="fl-group" key={group.classNameKey}>
          <h4 className="fl-group__head">
            <ClassChip nameKey={group.classNameKey} />
          </h4>
          <ul className="fl-projects">
            {group.projects.map((project) => {
              const quantity = quantities.get(project.designId) ?? 0;

              return (
                <li className={quantity > 0 ? 'fl-project fl-project--on' : 'fl-project'} key={project.designId}>
                  <span className="fl-project__text">
                    <span className="fl-project__name">{project.name}</span>
                    <span className="fl-project__sub">{text(project.hullNameKey) ?? project.hullNameKey}</span>
                  </span>
                  <span className="fl-project__qty">
                    <input
                      aria-label={t('fleets.quantityLabel', { name: project.name })}
                      className="fl-qty"
                      inputMode="numeric"
                      max={project.ships.length}
                      min={0}
                      onChange={(event) => {
                        const typed = Math.floor(Number(event.target.value));

                        onChange(project.designId, Number.isFinite(typed) ? Math.min(project.ships.length, Math.max(0, typed)) : 0);
                      }}
                      type="number"
                      value={quantity}
                    />
                    <span className="fl-project__of">{t('fleets.quantityOf', { count: project.ships.length })}</span>
                  </span>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}
