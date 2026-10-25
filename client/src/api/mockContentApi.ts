import type { Language } from '../i18n/context';
import type { ContentApi, NewsItem, RankingEntry } from './contentApi';

/**
 * Invented players of the design (task 00003, section 3.4): no real persons.
 */
const topPlayers: RankingEntry[] = [
  { place: 1, playerName: 'Kapitan_Zoria', race: 'terran', score: 128450 },
  { place: 2, playerName: 'NovaWolf', race: 'nexar', score: 121930 },
  { place: 3, playerName: 'RedFang', race: 'skolar', score: 117205 },
  { place: 4, playerName: 'Krystal_Kvarr', race: 'kvarr', score: 109880 },
  { place: 5, playerName: 'Strilets', race: 'terran', score: 102340 },
  { place: 6, playerName: 'Hex_Nomad', race: 'nexar', score: 98715 },
  { place: 7, playerName: 'Tikha_Hvylia', race: 'kvarr', score: 91060 },
  { place: 8, playerName: 'Orion_77', race: 'terran', score: 87420 },
  { place: 9, playerName: 'Luna_Vartova', race: 'nexar', score: 83190 },
  { place: 10, playerName: 'IronMayak', race: 'skolar', score: 79655 },
];

type LocalizedNews = Omit<NewsItem, 'title' | 'excerpt'> & Record<'title' | 'excerpt', Record<Language, string>>;

/**
 * Placeholder news of the design, in both languages.
 */
const news: LocalizedNews[] = [
  {
    id: 'update-0-9',
    publishedOn: '2026-10-21',
    image: 'constructor',
    title: { uk: 'Оновлення 0.9: модулі щитів', en: 'Update 0.9: shield modules' },
    excerpt: {
      uk: 'Нові щитові модулі для всіх рас і перероблений екран конструктора кораблів.',
      en: 'New shield modules for every race and a reworked ship designer screen.',
    },
  },
  {
    id: 'crystal-storm',
    publishedOn: '2026-10-14',
    image: 'comet',
    title: { uk: 'Подія «Кристальний шторм»', en: 'Event “Crystal Storm”' },
    excerpt: {
      uk: 'Тиждень подвоєного видобутку кристалів у туманності Вереск. Встигни заповнити склади.',
      en: 'A week of double crystal mining in the Heather nebula. Fill your storage while it lasts.',
    },
  },
  {
    id: 'commanders-tournament',
    publishedOn: '2026-10-02',
    image: 'fleet',
    title: { uk: 'Турнір командирів: реєстрація', en: 'Commanders’ tournament: sign-up' },
    excerpt: {
      uk: 'Збери флот і перевір свою тактику в турнірі на вибування. Приз — рідкісний корпус.',
      en: 'Gather a fleet and test your tactics in a knockout tournament. The prize is a rare hull.',
    },
  },
];

/**
 * The placeholder content until the server has ranking and news endpoints.
 */
export const mockContentApi: ContentApi = {
  getTopPlayers() {
    return Promise.resolve(topPlayers.map((entry) => ({ ...entry })));
  },
  getLatestNews(language) {
    return Promise.resolve(
      news.map((item) => ({ ...item, title: item.title[language], excerpt: item.excerpt[language] })),
    );
  },
};
