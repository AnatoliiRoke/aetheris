import { useMemo } from 'react';
import type { MailCategory, MailFolder, MailLetter } from '../../../api/mailApi';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { Glyph } from '../../../components/Glyph';
import { groupByDay, letterTimeOf, markKindOf, type MailDayGroup } from './mailParts';
import { loadShortKindCode } from './loadShortReport';

/**
 * The list of the letters of the mail (task 00060): the folders, the categories, the search and the
 * letters themselves, grouped by the days.
 *
 * The list holds no state of its own: the folder, the category, the search and the chosen letter
 * belong to the screen, so the list is drawn again only when one of them changes.
 */

/**
 * The names of the folders of the mail: they are the folders of the game, so the client names
 * them by their own keys and never by a table of codes.
 */
const folderLabelKeys = {
  incoming: 'mail.folder.incoming',
  sent: 'mail.folder.sent',
  saved: 'mail.folder.saved',
  trash: 'mail.folder.trash',
} as const;

/**
 * The icon of a folder of the mail.
 */
const folderIcons = {
  incoming: 'mail',
  sent: 'send',
  saved: 'star',
  trash: 'trash',
} as const;

/**
 * The names of the days of the groups of the list.
 */
const dayLabelKeys = {
  today: 'mail.day.today',
  yesterday: 'mail.day.yesterday',
  earlier: 'mail.day.earlier',
} as const;

/**
 * The folders of the mail with their counters.
 */
export function MailFolderList({
  folders,
  selected,
  onSelect,
}: {
  folders: readonly MailFolder[];
  selected: string | null;
  onSelect: (code: string | null) => void;
}) {
  const { t } = useI18n();

  return (
    <div aria-label={t('mail.folders')} className="mail-folders" role="group">
      {folders.map((folder) => {
        const key = folderLabelKeys[folder.code as keyof typeof folderLabelKeys] ?? 'mail.folder.incoming';
        const active = selected === folder.code;

        return (
          <button
            aria-pressed={active}
            className={active ? 'mail-folder is-active' : 'mail-folder'}
            key={folder.code}
            onClick={() => onSelect(active ? null : folder.code)}
            type="button"
          >
            <GameIcon name={folderIcons[folder.code as keyof typeof folderIcons] ?? 'mail'} size={17} />
            {t(key)}
            {folder.unreadCount > 0 ? <span className="mail-badge">{folder.unreadCount}</span> : null}
          </button>
        );
      })}
    </div>
  );
}

/**
 * The categories of the letters with their counters. The first button is «all», so the client
 * holds no list of the categories of the game: it draws the ones the server sent.
 */
export function MailCategoryList({
  categories,
  selected,
  totalCount,
  onSelect,
}: {
  categories: readonly MailCategory[];
  selected: number | null;
  totalCount: number;
  onSelect: (categoryId: number | null) => void;
}) {
  const { t } = useI18n();
  const { text } = useTexts();

  return (
    <div aria-label={t('mail.categories')} className="mail-categories" role="group">
      <button aria-pressed={selected === null} className={selected === null ? 'mail-chip is-active' : 'mail-chip'} onClick={() => onSelect(null)} type="button">
        <span className="mail-chip__mark">
          <GameIcon name="mail" size={17} />
        </span>
        {t('mail.all')}
        <span className="mail-chip__count">{totalCount}</span>
      </button>
      {categories.map((category) => (
        <button
          aria-pressed={selected === category.id}
          className={selected === category.id ? 'mail-chip is-active' : 'mail-chip'}
          key={category.id}
          onClick={() => onSelect(category.id)}
          type="button"
        >
          <span className="mail-chip__mark">
            <GameIcon name={category.imageKey === 'player' ? 'shapePeople' : category.imageKey === 'market' ? 'exchange' : 'star'} size={17} />
          </span>
          {text(category.nameKey) ?? category.nameKey}
          <span className="mail-chip__count">{category.count}</span>
        </button>
      ))}
    </div>
  );
}

/**
 * One letter of the list: the mark, the sender, the subject, the first words, the labels of the
 * category and the time.
 */
function MailLetterRow({
  letter,
  now,
  active,
  onOpen,
  onToggleSaved,
}: {
  letter: MailLetter;
  now: Date;
  active: boolean;
  onOpen: (letter: MailLetter) => void;
  onToggleSaved: (letter: MailLetter) => void;
}) {
  const { t } = useI18n();
  const { text } = useTexts();
  const mark = markKindOf(letter);
  const sender = letter.senderNickname ?? text(letter.categoryNameKey) ?? t('mail.game');
  const subject = letter.systemKindCode === 'scan_report' ? t('mail.scanTitle') : letter.systemKindCode === loadShortKindCode ? t('mail.loadShortTitle') : letter.subject || letter.preview;
  const label = `${letter.isRead ? '' : `${t('mail.unread')}. `}${text(letter.categoryNameKey) ?? ''}: ${subject}`;

  return (
    <div className={active ? 'mail-row is-active' : letter.isRead ? 'mail-row' : 'mail-row is-unread'}>
      <span aria-hidden="true" className={`mail-row__mark mail-row__mark--${mark}`}>
        <GameIcon name={mark === 'player' ? 'shapePeople' : mark === 'report' ? 'star' : mark === 'market' ? 'exchange' : mark === 'scout' ? 'radar' : 'mail'} size={20} />
      </span>
      <button aria-label={label} className="mail-row__body" onClick={() => onOpen(letter)} type="button">
        <span className={letter.isRead ? 'mail-row__sender' : 'mail-row__sender is-unread'}>
          {letter.senderRaceColorKey && mark === 'player' ? <Glyph colorKey={letter.senderRaceColorKey} small /> : null}
          <span className="mail-row__name">{sender}</span>
        </span>
        <span className={letter.isRead ? 'mail-row__subject' : 'mail-row__subject is-unread'}>{subject}</span>
        {letter.preview && letter.subject ? <span className="mail-row__preview">{letter.preview}</span> : null}
        <span className="mail-row__labels">
          <span className={`mail-tag mail-tag--${mark}`}>{text(letter.categoryNameKey) ?? letter.categoryNameKey}</span>
          {letter.hasAttachment ? (
            <span className="mail-tag">
              <GameIcon name="shapeCoin" size={11} />
              {t('mail.resources')}
            </span>
          ) : null}
          {letter.groupCount > 1 ? <span className="mail-tag">{t('mail.groupCount', { count: letter.groupCount })}</span> : null}
        </span>
      </button>
      <span className="mail-row__side">
        <span className="mail-row__time">{letterTimeOf(letter, now)}</span>
        <button
          aria-label={t('mail.save')}
          aria-pressed={letter.isSaved}
          className={letter.isSaved ? 'mail-row__save is-on' : 'mail-row__save'}
          onClick={() => onToggleSaved(letter)}
          title={t('mail.save')}
          type="button"
        >
          <GameIcon name="star" size={18} />
        </button>
      </span>
    </div>
  );
}

/**
 * The list of the letters of the current folder and category, grouped by the days.
 */
export function MailLetterList({
  letters,
  now,
  selectedId,
  onOpen,
  onToggleSaved,
  emptyText,
}: {
  letters: readonly MailLetter[];
  now: Date;
  selectedId: number | null;
  onOpen: (letter: MailLetter) => void;
  onToggleSaved: (letter: MailLetter) => void;
  emptyText: string;
}) {
  const { t } = useI18n();
  const sections = useMemo(() => groupByDay(letters, now), [letters, now]);

  if (letters.length === 0) {
    return (
      <div className="mail-empty">
        <svg aria-hidden="true" className="mail-empty__art" viewBox="0 0 160 92">
          <rect fill="none" height="90" rx="12" stroke="currentColor" strokeDasharray="4 4" width="158" x="1" y="1" />
          <path d="M32 38h96v38H32z M32 38l48 28 48-28" fill="none" stroke="var(--accent-hover)" strokeLinejoin="round" strokeWidth="2" />
        </svg>
        <p className="mail-empty__text">{emptyText}</p>
      </div>
    );
  }

  return (
    <div className="mail-rows">
      {sections.map((section) => (
        <section className="mail-day" key={section.day}>
          <h2 className="mail-day__title">{t(dayLabelKeys[section.day as MailDayGroup])}</h2>
          {section.letters.map((letter) => (
            <MailLetterRow
              active={letter.id === selectedId}
              key={letter.id}
              letter={letter}
              now={now}
              onOpen={onOpen}
              onToggleSaved={onToggleSaved}
            />
          ))}
        </section>
      ))}
    </div>
  );
}
