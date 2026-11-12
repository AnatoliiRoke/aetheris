import { useMemo, useState } from 'react';
import type { MailLetterDetail } from '../../../api/mailApi';
import { GameIcon } from '../../../components/GameIcon';
import { Glyph } from '../../../components/Glyph';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { markKindOf } from './mailParts';

/**
 * The panel of an open letter (task 00060): the header of the letter, the actions with it, the
 * text and the quick reply.
 *
 * The panel holds no state of its own, apart from the text of the quick reply, so it is drawn
 * again only when the letter changes.
 */

/**
 * The limit of the text of a letter: the server counts it too, so the client only shows the
 * counter of the design.
 */
export const bodyLimit = 2000;

/**
 * The reasons of a complaint, as the design shows them.
 */
const complaintReasons = ['spam', 'abuse', 'offence', 'other'] as const;

/**
 * The panel of an open letter.
 */
export function MailLetterPanel({
  detail,
  busy,
  onReply,
  onForward,
  onToggleSaved,
  onDelete,
  onComplain,
  onSendReply,
}: {
  detail: MailLetterDetail;
  busy: boolean;
  onReply: () => void;
  onForward: () => void;
  onToggleSaved: () => void;
  onDelete: () => void;
  onComplain: (reason: string) => void;
  onSendReply: (body: string) => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const letter = detail.letter;
  const mark = markKindOf(letter);
  const [reply, setReply] = useState('');
  const [complaining, setComplaining] = useState(false);

  const paragraphs = useMemo(() => detail.body.split(/\r?\n\r?\n/).filter((part) => part.trim().length > 0), [detail.body]);
  const created = new Date(letter.createdAt);
  const date = Number.isNaN(created.getTime())
    ? ''
    : new Intl.DateTimeFormat(language === 'uk' ? 'uk-UA' : 'en-GB', { dateStyle: 'long', timeStyle: 'short' }).format(created);

  return (
    <article className="mail-letter">
      <header className="mail-letter__head">
        <span className={`mail-tag mail-tag--${mark}`}>{text(letter.categoryNameKey) ?? letter.categoryNameKey}</span>
        <h2 className="mail-letter__subject">{letter.subject || text(letter.categoryNameKey) || letter.preview}</h2>
        <div className="mail-letter__meta">
          {letter.senderNickname ? (
            <span className="mail-letter__sender">
              {letter.senderRaceColorKey ? <Glyph colorKey={letter.senderRaceColorKey} small /> : null}
              {letter.senderNickname}
            </span>
          ) : null}
          {letter.senderNickname ? <span>{t('mail.toYou')}</span> : null}
          {date ? <span className="mail-letter__date">{date}</span> : null}
        </div>
        <div aria-label={t('mail.letterActions')} className="mail-letter__actions" role="toolbar">
          {letter.kindCode === 'player' ? (
            <button className="mail-action is-primary" onClick={onReply} type="button">
              <GameIcon name="transfer" size={17} />
              <span>{t('mail.reply')}</span>
            </button>
          ) : null}
          <button className="mail-action" onClick={onForward} type="button">
            <GameIcon name="send" size={17} />
            <span>{t('mail.forward')}</span>
          </button>
          <button aria-pressed={letter.isSaved} className="mail-action" onClick={onToggleSaved} type="button">
            <GameIcon name="star" size={17} />
            <span>{t('mail.save')}</span>
          </button>
          <button className="mail-action" onClick={onDelete} type="button">
            <GameIcon name="trash" size={17} />
            <span>{t('mail.delete')}</span>
          </button>
          {letter.kindCode === 'player' ? (
            <button className="mail-action is-danger" onClick={() => setComplaining((value) => !value)} type="button">
              <GameIcon name="warn" size={17} />
              <span>{t('mail.complain')}</span>
            </button>
          ) : null}
        </div>
      </header>

      {complaining ? (
        <div className="mail-complaint">
          {complaintReasons.map((reason) => (
            <button disabled={busy} key={reason} onClick={() => onComplain(reason)} type="button">
              {t(`mail.complaintReason.${reason}` as never)}
            </button>
          ))}
        </div>
      ) : null}

      <div className="mail-letter__body">
        {detail.textKey ? (
          <p className="mail-letter__system">{text(detail.textKey) ?? detail.textKey}</p>
        ) : (
          paragraphs.map((part, index) => <p key={index}>{part}</p>)
        )}
      </div>

      {letter.kindCode === 'player' && letter.senderNickname ? (
        <div className="mail-reply">
          <label className="mail-reply__label" htmlFor="mail-reply">
            {t('mail.replyTo', { name: letter.senderNickname })}
          </label>
          <textarea
            className="mail-reply__text"
            id="mail-reply"
            maxLength={bodyLimit}
            onChange={(event) => setReply(event.target.value)}
            placeholder={t('mail.replyPlaceholder')}
            rows={3}
            value={reply}
          />
          <div className="mail-reply__side">
            <span className="mail-reply__count">
              {reply.length} / {bodyLimit}
            </span>
            <button className="btn btn--primary" disabled={busy || reply.trim().length === 0} onClick={() => onSendReply(reply)} type="button">
              <GameIcon name="send" size={17} />
              {t('mail.send')}
            </button>
          </div>
        </div>
      ) : null}
    </article>
  );
}
