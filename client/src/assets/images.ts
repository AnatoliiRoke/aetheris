import aboutBattle from './images/about-battle.webp';
import aboutConstructor from './images/about-constructor.webp';
import aboutMarket from './images/about-market.webp';
import aboutPlanets from './images/about-planets.webp';
import authSide from './images/auth-side.webp';
import logoMark from './images/logo-mark.webp';
import newsComet from './images/news-comet.webp';
import newsFleet from './images/news-fleet.webp';
import racesBackground from './images/races-bg.webp';
import screenConstructor from './images/screen-constructor.webp';
import screenMarket from './images/screen-market.webp';
import screenOverview from './images/screen-overview.webp';
import screenStarmap from './images/screen-starmap.webp';
import art07 from './images/art-07.webp';
import art08 from './images/art-08.webp';
import art09 from './images/art-09.webp';
import art10 from './images/art-10.webp';
import art12a from './images/art-12a.webp';
import art12b from './images/art-12b.webp';
import art12c from './images/art-12c.webp';
import art12d from './images/art-12d.webp';
import art12e from './images/art-12e.webp';
import art12f from './images/art-12f.webp';
import type { NewsImage } from '../api/contentApi';

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
  planetTerra: art12a,
  planetGas: art12f,
  planetLava: art12b,
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
 * The illustrations a row of the game data names by its `image_key` (task 00022): the
 * identifiers of the art list of task 00003. A row names a picture, the client never maps a race
 * or a body type to one; a key that is not here shows the neutral placeholder.
 */
export const artImages: Record<string, string> = {
  'art-07': art07,
  'art-08': art08,
  'art-09': art09,
  'art-10': art10,
  'art-12a': art12a,
  'art-12b': art12b,
  'art-12c': art12c,
  'art-12d': art12d,
  'art-12e': art12e,
  'art-12f': art12f,
};

/**
 * The picture of an `image_key`, or `undefined` for the neutral placeholder.
 */
export function artImage(imageKey: string | null | undefined): string | undefined {
  return imageKey ? artImages[imageKey] : undefined;
}

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
