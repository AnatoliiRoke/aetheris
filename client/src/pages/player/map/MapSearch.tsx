import { useEffect, useRef, useState } from 'react';
import type { MapApi, MapSearchResult } from '../../../api/mapApi';
import { GameIcon, type GameIconName } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';

/** The pause after the last key before the search is asked, in milliseconds. */
const searchDelayMs = 250;

/** The shortest text the server searches for; a shorter text asks nothing. */
const minQueryLength = 2;

const kindIcon: Record<MapSearchResult['kind'], GameIconName> = { system: 'mapSystem', object: 'mapPlanet', player: 'shapePeople' };

/**
 * The search of the map: a field with the list of what was found (systems, bodies, players the
 * player sees). The server is asked once the player stops typing, not on every key.
 *
 * @param search The read of the search.
 * @param placeText The place of a result as the text "Sector · System · Orbit".
 * @param onChoose What to do when the player chooses a result.
 */
export function MapSearch({ search, placeText, onChoose }: { search: MapApi['search']; placeText: (result: MapSearchResult) => string; onChoose: (result: MapSearchResult) => void }) {
  const { t } = useI18n();
  const [text, setText] = useState('');
  const [results, setResults] = useState<MapSearchResult[] | null>(null);
  const [open, setOpen] = useState(false);
  const searchRef = useRef(search);

  useEffect(() => {
    searchRef.current = search;
  });

  useEffect(() => {
    const query = text.trim();

    if (query.length < minQueryLength) {
      return undefined;
    }

    let cancelled = false;
    const timer = window.setTimeout(() => {
      searchRef.current(query)
        .then((found) => {
          if (!cancelled) {
            setResults(found);
            setOpen(true);
          }
        })
        .catch(() => {
          if (!cancelled) {
            setResults([]);
          }
        });
    }, searchDelayMs);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [text]);

  const showList = open && text.trim().length >= minQueryLength && results !== null;

  return (
    <div className="map-search">
      <label className="visually-hidden" htmlFor="map-search">
        {t('map.searchLabel')}
      </label>
      <span aria-hidden="true" className="map-search__icon">
        <GameIcon name="search" size={20} />
      </span>
      <input
        aria-controls="map-search-results"
        aria-expanded={showList}
        autoComplete="off"
        className="input"
        id="map-search"
        placeholder={t('map.searchPlaceholder')}
        type="search"
        value={text}
        onChange={(event) => {
          setText(event.target.value);
          setOpen(true);
        }}
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            setOpen(false);
          }
        }}
      />
      {text !== '' ? (
        <button
          aria-label={t('map.searchClear')}
          className="map-search__clear"
          type="button"
          onClick={() => {
            setText('');
            setResults(null);
          }}
        >
          <GameIcon name="mapClose" size={16} />
        </button>
      ) : null}
      {showList ? (
        <div aria-label={t('map.searchResults')} className="map-search__results" id="map-search-results" role="listbox">
          {results.length === 0 ? (
            <p className="map-search__empty">{t('map.searchEmpty')}</p>
          ) : (
            <>
              <p className="map-search__count">
                {t('map.searchResults')} · <span className="num">{results.length}</span>
              </p>
              {results.map((result) => (
                <button
                  aria-selected="false"
                  className="map-search__item"
                  key={`${result.kind}-${result.objectId ?? result.systemId}-${result.name}`}
                  role="option"
                  type="button"
                  onClick={() => {
                    setOpen(false);
                    onChoose(result);
                  }}
                >
                  <GameIcon name={kindIcon[result.kind]} size={18} />
                  <span className="map-search__text">
                    <span className="map-search__name">
                      {result.name} <span className="meta">· {t(`map.searchKind.${result.kind}` as never)}</span>
                    </span>
                    <span className="meta">{placeText(result)}</span>
                  </span>
                </button>
              ))}
            </>
          )}
        </div>
      ) : null}
    </div>
  );
}
