import { useMemo, useState } from 'react';
import type { MailDraft, MailRecipient } from '../../../api/mailApi';
import { GameIcon } from '../../../components/GameIcon';
import { Glyph } from '../../../components/Glyph';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { bodyLimit } from './MailLetterPanel';

/**
 * The form of a new letter (task 00060): the receiver with the hints of the players, the subject,
 * the text with its counter and the buttons.
 *
 * The form is the same for a new letter, for a reply and for a forwarding: only the receiver and
 * the subject come from outside.
 */

/**
 * The limit of the subject of a letter: the server counts it too.
 */
export const subjectLimit = 200;

/**
 * The form of a letter that is written.
 */
export function MailComposeForm({
  draft,
  lockedReceiver,
  forwardedBody,
  busy,
  error,
  recipients,
  onSubmit,
  onSaveDraft,
  onRemoveDraft,
  onCancel,
}: {
  /** The draft the form starts from, or `null` for a new letter. */
  draft: MailDraft | null;
  /** The receiver of a reply or of a forwarding, which the player cannot change. */
  lockedReceiver: string | null;
  /** The text of a forwarded letter, which the form starts from. */
  forwardedBody: string | null;
  busy: boolean;
  error: string | null;
  /** The players a letter may be written to. */
  recipients: readonly MailRecipient[];
  onSubmit: (request: { receiverNickname: string; subject: string; body: string }) => void;
  onSaveDraft: (request: { receiverNickname: string | null; subject: string; body: string }) => void;
  onRemoveDraft: () => void;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const { text } = useTexts();
  const [receiver, setReceiver] = useState(draft?.receiverPlayerId ?? '');
  const [subject, setSubject] = useState(draft?.subject ?? '');
  const [body, setBody] = useState(draft?.body ?? forwardedBody ?? '');

  const hint = useMemo(() => {
    const needle = receiver.trim().toLocaleLowerCase();

    if (needle.length === 0) {
      return recipients.slice(0, 5);
    }

    return recipients.filter((player) => player.nickname.toLocaleLowerCase().includes(needle)).slice(0, 5);
  }, [receiver, recipients]);

  const chosen = recipients.find((player) => player.nickname === receiver.trim());
  const canSend = receiver.trim().length > 0 && subject.trim().length > 0 && body.trim().length > 0 && !busy;

  return (
    <form
      aria-label={t('mail.newLetter')}
      className="mail-compose"
      onSubmit={(event) => {
        event.preventDefault();

        if (canSend) {
          onSubmit({ receiverNickname: receiver.trim(), subject, body });
        }
      }}
    >
      <header className="mail-compose__head">
        <h2 className="mail-compose__title">{t('mail.newLetter')}</h2>
        <button aria-label={t('mail.close')} className="mail-compose__close" onClick={onCancel} type="button">
          <GameIcon name="close" size={18} />
        </button>
      </header>

      <div className="mail-compose__body">
        {lockedReceiver === null ? (
          <div className="mail-field">
            <label className="mail-field__label" htmlFor="mail-to">
              {t('mail.to')}
            </label>
            <div className="mail-field__control">
              {chosen ? (
                <span className="mail-chip-picked">
                  {chosen.raceColorKey ? <Glyph colorKey={chosen.raceColorKey} small /> : null}
                  {chosen.nickname}
                  <button aria-label={t('mail.removeReceiver')} onClick={() => setReceiver('')} type="button">
                    <GameIcon name="close" size={14} />
                  </button>
                </span>
              ) : null}
              <input
                autoComplete="off"
                className="mail-input"
                id="mail-to"
                list="mail-to-list"
                onChange={(event) => setReceiver(event.target.value)}
                placeholder={t('mail.toPlaceholder')}
                type="text"
                value={receiver}
              />
            </div>
            {hint.length > 0 ? (
              <div aria-label={t('mail.players')} className="mail-hints" id="mail-to-list" role="listbox">
                {hint.map((player) => (
                  <button key={player.playerId} onClick={() => setReceiver(player.nickname)} role="option" type="button">
                    <span className="mail-hints__mark">{player.raceColorKey ? <Glyph colorKey={player.raceColorKey} small /> : null}</span>
                    <span className="mail-hints__text">
                      <span className="mail-hints__name">{player.nickname}</span>
                      <span className="mail-hints__note">
                        {text(player.raceNameKey) ?? player.raceNameKey}
                        {player.hasWritten ? ` · ${t('mail.wroteBefore')}` : ''}
                      </span>
                    </span>
                  </button>
                ))}
              </div>
            ) : null}
          </div>
        ) : (
          <div className="mail-field">
            <span className="mail-field__label">{t('mail.to')}</span>
            <span className="mail-compose__locked">{lockedReceiver}</span>
          </div>
        )}

        <div className="mail-field">
          <label className="mail-field__label" htmlFor="mail-subject">
            {t('mail.subject')}
          </label>
          <input
            className="mail-input"
            id="mail-subject"
            maxLength={subjectLimit}
            onChange={(event) => setSubject(event.target.value)}
            placeholder={t('mail.subjectPlaceholder')}
            type="text"
            value={subject}
          />
        </div>

        <div className="mail-field">
          <label className="mail-field__label" htmlFor="mail-body">
            {t('mail.message')}
          </label>
          <textarea
            className="mail-textarea"
            id="mail-body"
            maxLength={bodyLimit}
            onChange={(event) => setBody(event.target.value)}
            rows={9}
            value={body}
          />
          <div className="mail-field__side">
            <span className="mail-field__note">{t('mail.noLinks')}</span>
            <span className="mail-field__count">
              {body.length} / {bodyLimit}
            </span>
          </div>
        </div>

        <div className="mail-field">
          <button
            aria-describedby="mail-attach-note"
            className="mail-attach"
            disabled
            title={t('mail.attachReason')}
            type="button"
          >
            <GameIcon name="shapeCoin" size={17} />
            {t('mail.attach')}
          </button>
          <span className="mail-attach-note" id="mail-attach-note">
            <GameIcon name="info" size={14} />
            {t('mail.attachReason')}
          </span>
        </div>

        {error ? <p className="mail-compose__error">{error}</p> : null}

        <div className="mail-compose__actions">
          <button className="btn" onClick={onCancel} type="button">
            {t('mail.cancel')}
          </button>
          {draft ? (
            <button className="btn" disabled={busy} onClick={onRemoveDraft} type="button">
              {t('mail.removeDraft')}
            </button>
          ) : null}
          <button className="btn" disabled={busy} onClick={() => onSaveDraft({ receiverNickname: receiver.trim() || null, subject, body })} type="button">
            {t('mail.saveDraft')}
          </button>
          <button className="btn btn--primary" disabled={!canSend} type="submit">
            <GameIcon name="send" size={18} />
            {t('mail.send')}
          </button>
        </div>
      </div>
    </form>
  );
}
