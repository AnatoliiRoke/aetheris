import aboutBattle from './images/about-battle.webp';
import aboutConstructor from './images/about-constructor.webp';
import aboutMarket from './images/about-market.webp';
import aboutPlanets from './images/about-planets.webp';
import authSide from './images/auth-side.webp';
import logoMark from './images/logo-mark.webp';
import newsComet from './images/news-comet.webp';
import newsFleet from './images/news-fleet.webp';
import planetGas from './images/planet-gas.webp';
import planetLava from './images/planet-lava.webp';
import planetTerra from './images/planet-terra.webp';
import racesBackground from './images/races-bg.webp';
import screenConstructor from './images/screen-constructor.webp';
import screenMarket from './images/screen-market.webp';
import screenOverview from './images/screen-overview.webp';
import screenStarmap from './images/screen-starmap.webp';
import shipKvarr from './images/ship-kvarr.webp';
import shipNexar from './images/ship-nexar.webp';
import shipSkolar from './images/ship-skolar.webp';
import shipTerran from './images/ship-terran.webp';
import type { NewsImage, RaceId } from '../api/contentApi';

/**
 * The illustrations of the design (documentation/design/aetheris-web 1, ART-01…ART-16).
 *
 * The hero background and the star layer live in `public/`, so that `index.html` can
 * preload them before the script runs; every other picture is bundled and loaded lazily.
 */
export const images = {
  heroBackground: '/hero-bg.webp',
  heroStars: '/stars.webp',
  logoMark,
  authSide,
  racesBackground,
  planetTerra,
  planetGas,
  planetLava,
  about: {
    planets: aboutPlanets,
    constructor: aboutConstructor,
    market: aboutMarket,
    battle: aboutBattle,
  },
  screens: {
    overview: screenOverview,
    starmap: screenStarmap,
    constructor: screenConstructor,
    market: screenMarket,
  },
} as const;

/**
 * The flagship of each race.
 */
export const shipImages: Record<RaceId, string> = {
  terran: shipTerran,
  kvarr: shipKvarr,
  skolar: shipSkolar,
  nexar: shipNexar,
};

/**
 * The pictures of the news items.
 */
export const newsImages: Record<NewsImage, string> = {
  constructor: aboutConstructor,
  comet: newsComet,
  fleet: newsFleet,
};

/**
 * The natural size of the bundled illustrations, so the browser reserves their space.
 */
export const artSize = { width: 1200, height: 896 } as const;
