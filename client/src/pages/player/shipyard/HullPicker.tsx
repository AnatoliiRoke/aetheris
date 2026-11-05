import { useMemo, useState } from 'react';
import type { ShipHull } from '../../../api/fleetApi';
import { CardArt } from '../../../components/CardArt';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatAmount } from '../planetText';
import { Tabs, type TabItem } from '../Tabs';
import { ShipyardDialog } from './ShipyardDialog';
import type { HullClass } from './shipyardParts';

/**
 * The first step of a new design: the list of the hulls (task 00038, the mock-up of the
 * shipyard). The classes of the hulls are the tabs over the list.
 *
 * @param hulls The hulls of the catalog.
 * @param classes The classes of the hulls.
 * @param onChoose What to do when the player chooses a hull.
 * @param onClose What to do when the player closes the window.
 */
export function HullPicker({ hulls, classes, onChoose, onClose }: { hulls: ShipHull[]; classes: HullClass[]; onChoose: (hull: ShipHull) => void; onClose: () => void }) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const [active, setActive] = useState('all');

  const tabs: TabItem[] = useMemo(
    () => [
      { id: 'all', label: t('shipyard.classAll') },
      ...classes.map((entry) => ({ id: entry.code, label: text(entry.nameKey) ?? entry.nameKey })),
    ],
    [classes, t, text],
  );

  const visible = active === 'all' ? hulls : hulls.filter((hull) => hull.partGroupCode === active);

  return (
    <ShipyardDialog
      actions={
        <button className="btn btn--secondary" onClick={onClose} type="button">
          {t('shipyard.cancel')}
        </button>
      }
      lead={t('shipyard.chooseHull')}
      onClose={onClose}
      title={t('shipyard.newDesign')}
    >
      <p className="sh-note">{t('shipyard.hullLead')}</p>

      <Tabs active={active} label={t('shipyard.hullClasses')} onChange={setActive} tabs={tabs} />

      <div className="sh-hulls">
        {visible.map((hull) => (
          <div className="sh-hull" key={hull.id}>
            <CardArt imageKey={hull.imageKey} label={text(hull.nameKey) ?? hull.nameKey} />
            <h3 className="sh-hull__name">{text(hull.nameKey) ?? hull.nameKey}</h3>
            <p className="sh-hull__class">{text(hull.partGroupNameKey) ?? hull.partGroupNameKey}</p>
            <dl className="sh-facts">
              <div>
                <dt className="sh-facts__term">{t('shipyard.slots')}</dt>
                <dd className="sh-facts__value">{hull.slots}</dd>
              </div>
              <div>
                <dt className="sh-facts__term">{t('shipyard.hold')}</dt>
                <dd className="sh-facts__value">{formatAmount(hull.holdCapacity, language)} т</dd>
              </div>
              <div>
                <dt className="sh-facts__term">{t('shipyard.integrity')}</dt>
                <dd className="sh-facts__value">{formatAmount(hull.integrity, language)}</dd>
              </div>
            </dl>
            <button className="btn btn--primary" onClick={() => onChoose(hull)} type="button">
              {t('shipyard.choose')}
            </button>
          </div>
        ))}
      </div>
    </ShipyardDialog>
  );
}
