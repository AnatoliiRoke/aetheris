import { useCallback, useMemo, useState } from 'react';
import { mailFolders, type MailDraft, type MailLetter, type MailLetterDetail, type MailRecipient, type MailView } from '../../api/mailApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { GameIcon } from '../../components/GameIcon';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { ScreenHead } from './ScreenHead';
import { noticeFromError, type Notice } from './screenNotice';
import { useRead } from './market/useRead';
import { MailCategoryList, MailFolderList, MailLetterList } from './mail/MailList';
import { MailComposeForm } from './mail/MailComposeForm';
import { MailLetterPanel } from './mail/MailLetterPanel';
import { useMailUnread } from './mail/useMailUnread';

/**
 * The prefixes of the refusals of the server that have their own text for the player here.
 */
const reasonPrefixes = ['mail_'];

/**
 * What the form of a letter is opened for.
 */
type ComposeMode =
  | { kind: 'none' }
  | { kind: 'new' }
  | { kind: 'reply'; receiver: string }
  | { kind: 'forward'; receiver: string; body: string };

/**
 * The screen of the mail (task 00060, mock-up `documentation/design/aetheris-mail-html`): the
 * folders on the left, the list of the letters in the middle and the open letter on the right.
 *
 * Every rule is the answer of the server: the screen shows why a command is refused and never
 * decides a rule of its own. The commands answer with the whole list, so the screen does not ask
 * again after a command.
 */
export function MailPage() {
  const { t, language } = useI18n();
  const { mail: mailApi } = useApi();

  useDocumentTitle(t('meta.mail.title'));

  const [folder, setFolder] = useState<string | null>(null);
  const [categoryId, setCategoryId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<MailLetterDetail | null>(null);
  const [compose, setCompose] = useState<ComposeMode>({ kind: 'none' });
  const [composeDraft, setComposeDraft] = useState<MailDraft | null>(null);
  const [composeError, setComposeError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice>(null);
  const [busy, setBusy] = useState(false);

  // The counter of the top bar follows the mail: it is read when the frame loads and after every
  // command, so the number in the header is never a made-up one.
  const unread = useMailUnread();

  const load = useCallback(
    () =>
      mailApi.mail({
        folder,
        categoryId,
        search: search.trim().length > 0 ? search.trim() : null,
      }),
    [categoryId, folder, mailApi, search],
  );
  const read = useRead(load);

  const draftsRead = useRead(useCallback(() => mailApi.drafts(), [mailApi]), true, false);
  const recipientsRead = useRead(useCallback(() => mailApi.recipients(), [mailApi]), true, false);

  const view: MailView | null = read.data;

  /**
   * Runs a command of the mail and reads the list again, so the screen shows the new state.
   */
  const run = useCallback(
    async (command: () => Promise<unknown>, done: string): Promise<boolean> => {
      setBusy(true);

      try {
        await command();
        await read.reload();
        await unread.reload();
        setNotice({ kind: 'success', text: done });
        setComposeError(null);

        return true;
      } catch (error) {
        const refusal = noticeFromError(error, language, t('mail.failed'), reasonPrefixes);

        setNotice(refusal);
        setComposeError(refusal?.text ?? null);

        return false;
      } finally {
        setBusy(false);
      }
    },
    [language, read, t, unread],
  );

  /**
   * Opens a letter and reads its whole text. The server marks it as read.
   */
  const open = useCallback(
    async (letter: MailLetter) => {
      setSelectedId(letter.id);

      try {
        setDetail(await mailApi.letter(letter.id));
        await unread.reload();
      } catch {
        setDetail(null);
      }
    },
    [mailApi, unread],
  );

  const now = useMemo(() => gameNowOf(view?.gameNow), [view?.gameNow]);

  const recipients: readonly MailRecipient[] = recipientsRead.data?.players ?? [];
  const drafts: readonly MailDraft[] = draftsRead.data?.drafts ?? [];

  // A letter that is not on the screen any more is closed: the list changes with the folder and
  // with the category, and the panel of a letter that is gone would be a letter of another list.
  const selected = view?.letters.find((letter) => letter.id === selectedId) ?? null;
  const openLetter = detail && selected && detail.letter.id === selected.id ? detail : null;

  /**
   * Opens the form of a new letter, of a reply or of a forwarding.
   */
  const openCompose = useCallback((mode: ComposeMode) => {
    setCompose(mode);
    setComposeDraft(null);
    setComposeError(null);
  }, []);

  const sendLetter = useCallback(
    (request: { receiverNickname: string; subject: string; body: string }) =>
      void run(() => mailApi.sendLetter(request), t('mail.sent')),
    [mailApi, run, t],
  );

  const saveDraft = useCallback(
    (request: { receiverNickname: string | null; subject: string; body: string }) =>
      void run(
        () =>
          mailApi.saveDraft({
            draftId: composeDraft?.id ?? 0,
            receiverNickname: request.receiverNickname,
            subject: request.subject,
            body: request.body,
          }),
        t('mail.draftSaved'),
      ),
    [composeDraft?.id, mailApi, run, t],
  );

  if (read.failed && !view) {
    return (
      <div className="civ-main__in">
        <Alert kind="error">{t('mail.loadFailed')}</Alert>
      </div>
    );
  }

  if (!view) {
    return (
      <div className="civ-main__in">
        <p className="meta">{t('mail.loading')}</p>
      </div>
    );
  }

  const composing = compose.kind !== 'none';

  return (
    <div className="civ-main__in">
      <ScreenHead
        actions={
          <div className="mail-head-actions">
            <button
              className="btn"
              disabled={busy || view.unreadCount === 0}
              onClick={() => void run(() => mailApi.readAll(folder ?? mailFolders.incoming), t('mail.allRead'))}
              type="button"
            >
              <GameIcon name="done" size={18} />
              {t('mail.readAll')}
            </button>
            <button className="btn btn--primary" onClick={() => openCompose({ kind: 'new' })} type="button">
              <GameIcon name="pencil" size={18} />
              {t('mail.newLetter')}
            </button>
          </div>
        }
        breadcrumbs={[
          { label: t('civ.nav.overview'), to: routes.game },
          { label: t('civ.nav.mail') },
        ]}
        subtitle={t('mail.unreadCount', { count: view.unreadCount })}
        title={t('mail.title')}
      />

      {notice ? <Alert kind={notice.kind === 'error' ? 'error' : 'info'}>{notice.text}</Alert> : null}

      <div className="mail-filters">
        <MailFolderList folders={view.folders} onSelect={setFolder} selected={folder} />
        <MailCategoryList categories={view.categories} onSelect={setCategoryId} selected={categoryId} totalCount={view.totalCount} />
      </div>

      <div className="mail-layout">
        <section aria-label={t('mail.letterList')} className="mail-list">
          <div className="mail-search">
            <label className="visually-hidden" htmlFor="mail-search">
              {t('mail.search')}
            </label>
            <GameIcon name="search" size={20} />
            <input
              className="mail-search__input"
              id="mail-search"
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t('mail.searchPlaceholder')}
              type="search"
              value={search}
            />
          </div>

          <MailLetterList
            emptyText={folder === mailFolders.trash ? t('mail.trashEmpty') : t('mail.empty')}
            letters={view.letters}
            now={now}
            onOpen={(letter) => void open(letter)}
            onToggleSaved={(letter) => void run(() => mailApi.toggleSaved(letter.id), t('mail.saved'))}
            selectedId={selectedId}
          />

          {folder === mailFolders.trash && view.letters.length > 0 ? (
            <div className="mail-trash-actions">
              <button className="btn" disabled={busy} onClick={() => void run(() => mailApi.emptyTrash(), t('mail.trashEmptied'))} type="button">
                <GameIcon name="trash" size={16} />
                {t('mail.emptyTrash')}
              </button>
            </div>
          ) : null}

          {drafts.length > 0 ? (
            <div className="mail-drafts">
              <h2 className="mail-drafts__title">{t('mail.drafts')}</h2>
              {drafts.map((draft) => (
                <button
                  className="mail-drafts__row"
                  key={draft.id}
                  onClick={() => {
                    setComposeDraft(draft);
                    setCompose({ kind: 'new' });
                    setComposeError(null);
                  }}
                  type="button"
                >
                  <span className="mail-drafts__subject">{draft.subject || t('mail.draftWithoutSubject')}</span>
                  <span className="mail-drafts__note">{draft.body.slice(0, 80)}</span>
                </button>
              ))}
            </div>
          ) : null}
        </section>

        <section aria-label={t('mail.letter')} className="mail-panel">
          {composing ? (
            <MailComposeForm
              busy={busy}
              draft={composeDraft}
              error={composeError}
              forwardedBody={compose.kind === 'forward' ? compose.body : null}
              key={composeDraft?.id ?? `compose-${compose.kind}`}
              lockedReceiver={compose.kind === 'reply' || compose.kind === 'forward' ? compose.receiver : null}
              onCancel={() => setCompose({ kind: 'none' })}
              onRemoveDraft={() => void run(() => mailApi.removeDraft(composeDraft?.id ?? 0), t('mail.draftRemoved'))}
              onSaveDraft={saveDraft}
              onSubmit={sendLetter}
              recipients={recipients}
            />
          ) : openLetter ? (
            <MailLetterPanel
              busy={busy}
              detail={openLetter}
              key={openLetter.letter.id}
              onComplain={(reason) => void run(() => mailApi.complain({ messageId: openLetter.letter.id, reason }), t('mail.complained'))}
              onDelete={() => void run(() => mailApi.toggleDeleted(openLetter.letter.id), t('mail.deleted'))}
              onForward={() => openCompose({ kind: 'forward', receiver: openLetter.letter.senderNickname ?? '', body: openLetter.body })}
              onReply={() => openCompose({ kind: 'reply', receiver: openLetter.letter.senderNickname ?? '' })}
              onSendReply={(body) =>
                void run(
                  () =>
                    mailApi.sendLetter({
                      receiverNickname: openLetter.letter.senderNickname ?? '',
                      subject: openLetter.letter.subject ? t('mail.replySubject', { subject: openLetter.letter.subject }) : t('mail.replySubjectEmpty'),
                      body,
                    }),
                  t('mail.sent'),
                )
              }
              onToggleSaved={() => void run(() => mailApi.toggleSaved(openLetter.letter.id), t('mail.saved'))}
            />
          ) : (
            <div className="mail-empty">
              <svg aria-hidden="true" className="mail-empty__art" viewBox="0 0 160 92">
                <rect fill="none" height="90" rx="12" stroke="currentColor" strokeDasharray="4 4" width="158" x="1" y="1" />
                <path d="M80 22l30 14v30L80 80 50 66V36z M80 22v58" fill="none" stroke="var(--accent-hover)" strokeLinejoin="round" strokeWidth="2" />
              </svg>
              <p className="mail-empty__text">{t('mail.pickLetter')}</p>
            </div>
          )}
        </section>
      </div>

      <p className="meta">
        <Link to={routes.game}>{t('mail.back')}</Link>
      </p>
    </div>
  );
}

/**
 * The moment of game time the list was read at, so the client counts the days and the times of the
 * letters by the clock of the game and not by the clock of the computer.
 *
 * @param gameNow The game time of the answer, or `null` when the server did not send it.
 */
function gameNowOf(gameNow: string | undefined): Date {
  if (!gameNow) {
    return new Date();
  }

  const parsed = new Date(gameNow);

  return Number.isNaN(parsed.getTime()) ? new Date() : parsed;
}
