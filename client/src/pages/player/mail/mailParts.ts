import { mailFolders, type MailLetter } from '../../../api/mailApi';

/**
 * The helpers of the screen of the mail (task 00060): the groups of the list, the day of a letter
 * and the kind of the mark of a letter.
 */

/**
 * The day of a group of the list: the letters of today, of yesterday and of the older ones.
 */
export type MailDayGroup = 'today' | 'yesterday' | 'earlier';

/**
 * One group of the list of the letters: the day and the letters of it.
 */
export interface MailDaySection {
  /** The day of the group. */
  day: MailDayGroup;
  /** The letters of the group, the newest first. */
  letters: MailLetter[];
}

/**
 * The key of a letter that groups the letters of the game of the same kind: a letter of a player
 * stands on its own, a letter of the game stands with the letters of the same kind.
 *
 * @param letter The letter.
 */
export function groupKeyOf(letter: MailLetter): string {
  if (letter.kindCode === 'player') {
    return `player:${letter.id}`;
  }

  return `${letter.kindCode}:${letter.systemKindCode ?? letter.categoryId}`;
}

/**
 * The number of the days between two moments, counted by their calendar days: the day of a letter
 * of the game is the day of the clock of the game, not a difference of hours.
 *
 * @param created The moment the letter was written at.
 * @param now The moment of game time the list was read at.
 */
function dayDistance(created: Date, now: Date): number {
  const createdDay = Date.UTC(created.getFullYear(), created.getMonth(), created.getDate());
  const nowDay = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());

  return Math.round((nowDay - createdDay) / (24 * 60 * 60 * 1000));
}

/**
 * Puts the letters into the groups of the days, the newest first. The letters that stand for
 * several ones of the game are counted in the group, not repeated.
 *
 * @param letters The letters of the list.
 * @param now The moment of game time the list was read at.
 */
export function groupByDay(letters: readonly MailLetter[], now: Date): MailDaySection[] {
  const sections: MailDaySection[] = [];
  const today: MailLetter[] = [];
  const yesterday: MailLetter[] = [];
  const earlier: MailLetter[] = [];

  for (const letter of letters) {
    const created = new Date(letter.createdAt);

    if (Number.isNaN(created.getTime())) {
      earlier.push(letter);
      continue;
    }

    const distance = dayDistance(created, now);

    if (distance <= 0) {
      today.push(letter);
    } else if (distance === 1) {
      yesterday.push(letter);
    } else {
      earlier.push(letter);
    }
  }

  if (today.length > 0) {
    sections.push({ day: 'today', letters: today });
  }

  if (yesterday.length > 0) {
    sections.push({ day: 'yesterday', letters: yesterday });
  }

  if (earlier.length > 0) {
    sections.push({ day: 'earlier', letters: earlier });
  }

  return sections;
}

/**
 * The kind of the mark of a letter in the list: a letter of the game, a letter of a player or a
 * report. The mark of a letter of a player carries the sign of its race.
 *
 * @param letter The letter.
 */
export type MailMarkKind = 'player' | 'system' | 'report' | 'market' | 'scout';

/**
 * Tells the kind of the mark of a letter by the kind of the letter and by the kind of the game
 * that wrote it.
 *
 * @param letter The letter.
 */
export function markKindOf(letter: MailLetter): MailMarkKind {
  if (letter.kindCode === 'report') {
    return 'report';
  }

  if (letter.kindCode === 'player') {
    return 'player';
  }

  switch (letter.systemKindCode) {
    case 'market_offer':
    case 'market_lot_sold':
      return 'market';
    case 'scout_report':
      return 'scout';
    default:
      return 'system';
  }
}

/**
 * The folders of the mail in the order the design shows them, with the code of the folder.
 */
export const folderOrder: readonly string[] = [mailFolders.incoming, mailFolders.sent, mailFolders.saved, mailFolders.trash];

/**
 * The time of a letter as the list shows it: the time of the day for a letter of today and the
 * date for an older one.
 *
 * @param letter The letter.
 * @param now The moment of game time the list was read at.
 */
export function letterTimeOf(letter: MailLetter, now: Date): string {
  const created = new Date(letter.createdAt);

  if (Number.isNaN(created.getTime())) {
    return '';
  }

  if (dayDistance(created, now) <= 0) {
    return `${String(created.getHours()).padStart(2, '0')}:${String(created.getMinutes()).padStart(2, '0')}`;
  }

  return `${String(created.getDate()).padStart(2, '0')}.${String(created.getMonth() + 1).padStart(2, '0')}`;
}
