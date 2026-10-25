import type { Language } from '../i18n/context';

/**
 * The races of the game (documentation/game-design/08-rasy.md).
 */
export type RaceId = 'terran' | 'kvarr' | 'skolar' | 'nexar';

/**
 * One line of the public ranking.
 */
export interface RankingEntry {
  place: number;
  playerName: string;
  race: RaceId;
  score: number;
}

/**
 * The picture of a news item: one of the illustrations bundled with the client.
 */
export type NewsImage = 'constructor' | 'comet' | 'fleet';

/**
 * One news item, already in the requested language.
 */
export interface NewsItem {
  id: string;
  publishedOn: string;
  title: string;
  excerpt: string;
  image: NewsImage;
}

/**
 * The public content of the landing page: ranking and news.
 *
 * The server has no endpoints for them yet; the landing page talks to this interface only,
 * so the mock below is replaced by an HTTP implementation without touching the components.
 */
export interface ContentApi {
  getTopPlayers(): Promise<RankingEntry[]>;
  getLatestNews(language: Language): Promise<NewsItem[]>;
}
