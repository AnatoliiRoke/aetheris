import type { HttpClient } from './http';

/**
 * The mail of a player (task 00060): the folders, the categories, the list of the letters and the
 * counters. The categories and their names come from the server as data, so the client holds no
 * table of codes and names.
 */

/**
 * The folders of the mail of the game.
 */
export const mailFolders = {
  incoming: 'incoming',
  sent: 'sent',
  saved: 'saved',
  trash: 'trash',
} as const;

/**
 * A code of a folder of the mail.
 */
export type MailFolderCode = (typeof mailFolders)[keyof typeof mailFolders];

/**
 * The kinds of the letters of the game.
 */
export const mailLetterKinds = {
  player: 'player',
  system: 'system',
  report: 'report',
} as const;

/**
 * A kind of a letter of the game.
 */
export type MailLetterKindCode = (typeof mailLetterKinds)[keyof typeof mailLetterKinds];

/**
 * The mail of a player with the folders, the categories and the list of the letters.
 */
export interface MailView {
  /** The folders of the mail with the number of the unread letters in each. */
  folders: MailFolder[];
  /** The categories of the letters with the number of the letters in each. */
  categories: MailCategory[];
  /** The letters of the list, the newest first. */
  letters: MailLetter[];
  /** How many letters of the folder «incoming» are not read. */
  unreadCount: number;
  /** How many letters the list holds. */
  totalCount: number;
  /** The game time of the answer, so the client counts the time left of a letter. */
  gameNow?: string;
}

/**
 * A folder of the mail with the number of the unread letters in it.
 */
export interface MailFolder {
  /** The code of the folder, one of the folders of the game. */
  code: string;
  /** How many letters of the folder are not read. */
  unreadCount: number;
  /** How many letters the folder holds. */
  totalCount: number;
}

/**
 * A category of the letters.
 */
export interface MailCategory {
  id: number;
  /** The code of the category. */
  code: string;
  /** The key of the name of the category in the texts of the game. */
  nameKey: string;
  /** The name of the icon of the category, or `null`. */
  imageKey: string | null;
  /** How many letters of the category the list holds. */
  count: number;
}

/**
 * A letter of the mail as the list shows it.
 */
export interface MailLetter {
  id: number;
  /** The folder the letter lies in for the player. */
  folderCode: string;
  /** The category of the letter. */
  categoryId: number;
  /** The key of the name of the category in the texts of the game. */
  categoryNameKey: string;
  /** The player who wrote the letter, or `null` when the game wrote it. */
  senderPlayerId: string | null;
  /** The nickname of the sender, or `null` when the game wrote it. */
  senderNickname: string | null;
  /** The colour of the race of the sender, or `null`. */
  senderRaceColorKey: string | null;
  /** The subject of the letter, or an empty text for a letter of the game. */
  subject: string;
  /** The first words of the letter. */
  preview: string;
  /** The kind of the letter: a letter of a player, a letter of the game or a report. */
  kindCode: string;
  /** The kind of a letter of the game, or `null`. */
  systemKindCode: string | null;
  /** Whether the player has read the letter. */
  isRead: boolean;
  /** Whether the player has saved the letter. */
  isSaved: boolean;
  /** Whether the letter lies in the bin. */
  isDeleted: boolean;
  /** Whether the letter carries resources. */
  hasAttachment: boolean;
  /** The moment of game time the letter was written at. */
  createdAt: string;
  /** How many letters of the same kind of the game the letter stands for. */
  groupCount: number;
}

/**
 * One letter of the mail with its whole text.
 */
export interface MailLetterDetail {
  /** The letter as the list shows it. */
  letter: MailLetter;
  /** The text of the letter of a player, or an empty text for a letter of the game. */
  body: string;
  /** The key of the text of a letter of the game, or `null`. */
  textKey: string | null;
  /** The parameters of the text of a letter of the game as a document, or `null`. */
  textParameters: string | null;
}

/**
 * The drafts of a player.
 */
export interface MailDrafts {
  drafts: MailDraft[];
}

/**
 * A draft of a letter.
 */
export interface MailDraft {
  id: number;
  /** The receiver named by the draft, or `null`. */
  receiverPlayerId: string | null;
  /** The subject of the draft. */
  subject: string;
  /** The text of the draft. */
  body: string;
  /** The moment of game time the draft was changed at last. */
  updatedAt: string;
}

/**
 * The players a letter may be written to.
 */
export interface MailRecipients {
  players: MailRecipient[];
}

/**
 * One player a letter may be written to.
 */
export interface MailRecipient {
  playerId: string;
  /** The nickname of the player. */
  nickname: string;
  /** The colour of the race of the player, or `null`. */
  raceColorKey: string | null;
  /** The key of the name of the race in the texts of the game. */
  raceNameKey: string;
  /** Whether the player has already written to them. */
  hasWritten: boolean;
}

/**
 * How many letters of the player are not read.
 */
export interface MailUnread {
  unreadCount: number;
}

/**
 * What the screen of the mail reads: the list with its filters, one letter, the drafts, the
 * players to write to and the counter of the top bar.
 */
export interface MailApi {
  /** The list of the letters with the folders and the categories. */
  mail(request?: MailListRequest): Promise<MailView>;
  /** One letter with its whole text. The letter the player opens is marked as read. */
  letter(messageId: number): Promise<MailLetterDetail>;
  /** The drafts of the player. */
  drafts(): Promise<MailDrafts>;
  /** The players a letter may be written to. */
  recipients(text?: string): Promise<MailRecipients>;
  /** The number of the letters that are not read: the counter of the top bar. */
  unread(): Promise<MailUnread>;
  /** Writes a letter to another player. */
  sendLetter(request: { receiverNickname: string; subject: string; body: string }): Promise<{ id: number }>;
  /** Writes a draft of a letter or changes the one that is named. */
  saveDraft(request: { draftId: number; receiverNickname: string | null; subject: string; body: string }): Promise<MailDraft>;
  /** Removes a draft of a letter. */
  removeDraft(draftId: number): Promise<{ removed: boolean }>;
  /** Marks every letter of a folder of the player as read. */
  readAll(folderCode: string): Promise<{ marked: number }>;
  /** Saves a letter of the player or takes it back from the saved ones. */
  toggleSaved(messageId: number): Promise<{ saved: boolean | null }>;
  /** Moves a letter of the player to the bin or takes it back from the bin. */
  toggleDeleted(messageId: number): Promise<{ deleted: boolean | null }>;
  /** Empties the bin of the player. */
  emptyTrash(): Promise<{ removed: number }>;
  /** Complains about a letter of another player. */
  complain(request: { messageId: number; reason: string }): Promise<{ id: number }>;
}

/**
 * The filters of the list of the letters.
 */
export interface MailListRequest {
  /** The folder to show, or `null` for every folder. */
  folder?: string | null;
  /** The category to show, or `null` for every category. */
  categoryId?: number | null;
  /** The text to look for in the subject and in the name of the sender. */
  search?: string | null;
  /** The page of the list, from one. */
  page?: number;
}

/**
 * The routes of the mail module over HTTP.
 */
const prefix = '/api/v1/mail';

/**
 * Makes the endpoints of the mail module over HTTP.
 *
 * @param http The client of the server.
 */
export function createMailApi(http: HttpClient): MailApi {
  return {
    mail: (request) => {
      const query = new URLSearchParams();

      if (request?.folder) {
        query.set('folder', request.folder);
      }

      if (request?.categoryId !== undefined && request.categoryId !== null) {
        query.set('category_id', String(request.categoryId));
      }

      if (request?.search) {
        query.set('search', request.search);
      }

      if (request?.page !== undefined && request.page > 1) {
        query.set('page', String(request.page));
      }

      const search = query.toString();

      return http.get<MailView>(`${prefix}/${search ? `?${search}` : ''}`);
    },
    letter: (messageId) => http.get<MailLetterDetail>(`${prefix}/letters/${messageId}`),
    drafts: () => http.get<MailDrafts>(`${prefix}/drafts`),
    recipients: (text) => http.get<MailRecipients>(`${prefix}/recipients${text ? `?text=${encodeURIComponent(text)}` : ''}`),
    unread: () => http.get<MailUnread>(`${prefix}/unread`),
    sendLetter: (request) =>
      http.command<{ id: number }>(`${prefix}/letters`, {
        receiver_nickname: request.receiverNickname,
        subject: request.subject,
        body: request.body,
      }),
    saveDraft: (request) =>
      http.command<MailDraft>(`${prefix}/drafts`, {
        draft_id: request.draftId,
        receiver_nickname: request.receiverNickname,
        subject: request.subject,
        body: request.body,
      }),
    removeDraft: (draftId) => http.command<{ removed: boolean }>(`${prefix}/drafts/remove`, { draft_id: draftId }),
    readAll: (folderCode) => http.command<{ marked: number }>(`${prefix}/read-all`, { folder_code: folderCode }),
    toggleSaved: (messageId) => http.command<{ saved: boolean | null }>(`${prefix}/letters/save`, { message_id: messageId }),
    toggleDeleted: (messageId) => http.command<{ deleted: boolean | null }>(`${prefix}/letters/delete`, { message_id: messageId }),
    emptyTrash: () => http.command<{ removed: number }>(`${prefix}/trash/empty`, {}),
    complain: (request) =>
      http.command<{ id: number }>(`${prefix}/complaints`, {
        message_id: request.messageId,
        reason: request.reason,
      }),
  };
}
