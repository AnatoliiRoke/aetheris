import type { ReactNode } from 'react';
import { GameIcon, type GameIconName } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';

/** A row of the list view: the same place as a mark of the map. */
export interface MapListRow {
  key: string;
  icon: GameIconName;
  name: string;
  type: string;
  owner: ReactNode;
  place: string;
  distance: string;
  onShow: () => void;
}

/**
 * The list view of the map (task 00034): the places of the level as a table, so the map is reachable
 * with a keyboard and a screen reader. "On the map" returns to the drawn map with the place chosen.
 *
 * @param rows The places.
 */
export function MapList({ rows }: { rows: MapListRow[] }) {
  const { t } = useI18n();

  if (rows.length === 0) {
    return (
      <div className="civ-empty">
        <p>{t('map.listEmpty')}</p>
      </div>
    );
  }

  return (
    <div className="map-list">
      <table className="map-table">
        <caption className="visually-hidden">{t('map.listCaption')}</caption>
        <thead>
          <tr>
            <th scope="col">{t('map.col.object')}</th>
            <th scope="col">{t('map.col.type')}</th>
            <th scope="col">{t('map.col.owner')}</th>
            <th scope="col">{t('map.col.place')}</th>
            <th className="num" scope="col">
              {t('map.col.distance')}
            </th>
            <th scope="col">{t('map.col.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key}>
              <th scope="row">
                <span className="map-table__name">
                  <GameIcon name={row.icon} size={18} />
                  {row.name}
                </span>
              </th>
              <td>{row.type}</td>
              <td>{row.owner}</td>
              <td>{row.place}</td>
              <td className="num">{row.distance}</td>
              <td>
                <button className="btn btn--secondary" type="button" onClick={row.onShow}>
                  <GameIcon name="mapGalaxy" size={16} />
                  {t('map.showOnMap')}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
