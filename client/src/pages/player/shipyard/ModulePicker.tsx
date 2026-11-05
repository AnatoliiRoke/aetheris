import { useMemo, useState } from 'react';
import type { ShipHull, ShipModule, ShipModuleGroup } from '../../../api/fleetApi';
import { CardArt } from '../../../components/CardArt';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatAmount } from '../planetText';
import { Tabs, type TabItem } from '../Tabs';
import { ShipyardDialog } from './ShipyardDialog';
import { mainTrait, moduleFits, moduleReason, type ModuleReason } from './shipyardParts';

/**
 * The picker of a module: the list of the modules that go into the hull, with the filters by
 * the type of a module (task 00038, the mock-up of the shipyard).
 *
 * @remarks
 * The modules that do not go into the hull are not shown at all: the stock of the body does not
 * matter for a project, and a module of another hull is of no use here.
 *
 * @param modules The modules of the catalog.
 * @param moduleGroups The types of the modules.
 * @param hull The hull of the project, or `null` when the player has not chosen one.
 * @param used How many slots are taken.
 * @param total How many slots the hull has.
 * @param onChoose What to do when the player chooses a module.
 * @param onClose What to do when the player closes the window.
 */
export function ModulePicker({
  modules,
  moduleGroups,
  hull,
  used,
  total,
  onChoose,
  onClose,
}: {
  modules: ShipModule[];
  moduleGroups: ShipModuleGroup[];
  hull: ShipHull | null;
  used: number;
  total: number;
  onChoose: (module: ShipModule) => void;
  onClose: () => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const [type, setType] = useState('all');

  // Only the modules that go into the hull are offered; the rest is not shown.
  const fitting = useMemo(() => modules.filter((module) => moduleFits(module, hull)), [hull, modules]);

  const groups = useMemo(() => {
    const counts = new Map<string, number>();

    for (const module of fitting) {
      counts.set(module.partGroupCode, (counts.get(module.partGroupCode) ?? 0) + 1);
    }

    return moduleGroups
      .filter((group) => counts.has(group.code))
      .map((group) => ({ ...group, count: counts.get(group.code) ?? 0 }));
  }, [fitting, moduleGroups]);

  const tabs: TabItem[] = useMemo(
    () => [
      { id: 'all', label: t('shipyard.classAll') },
      ...groups.map((group) => ({ id: group.code, label: text(group.nameKey) ?? group.nameKey })),
    ],
    [groups, t, text],
  );

  const visible = type === 'all' ? fitting : fitting.filter((module) => module.partGroupCode === type);

  const reasons = useMemo(
    () => new Map<number, ModuleReason | null>(visible.map((module) => [module.id, moduleReason(hull, used, total)])),
    [hull, total, used, visible],
  );

  return (
    <ShipyardDialog onClose={onClose} title={t('shipyard.pickModule')}>
      <Tabs active={type} label={t('shipyard.moduleTypes')} onChange={setType} tabs={tabs} />

      <div className="sh-picker">
        {visible.map((module) => {
          const reason = reasons.get(module.id) ?? null;
          const trait = mainTrait(module);

          return (
            <div className={reason ? 'sh-module sh-module--closed' : 'sh-module'} key={module.id}>
              <CardArt imageKey={module.imageKey} label={text(module.nameKey) ?? module.nameKey} />
              <h3 className="sh-module__name">{text(module.nameKey) ?? module.nameKey}</h3>
              <p className="sh-module__facts">
                {trait ? `${t(`shipyard.trait.${trait.label}` as never)}: ${formatAmount(Number(trait.value), language)}` : null}
                {trait && module.weight > 0 ? ` · ${formatAmount(module.weight, language)} т` : null}
              </p>
              {reason ? <p className="sh-module__reason">{t(`shipyard.reason.${reason}` as never)}</p> : null}
              <button className="btn btn--primary" disabled={reason !== null} onClick={() => onChoose(module)} type="button">
                {t('shipyard.put')}
              </button>
            </div>
          );
        })}
      </div>
    </ShipyardDialog>
  );
}
