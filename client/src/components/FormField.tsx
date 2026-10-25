import { useId, useState, type InputHTMLAttributes, type ReactNode, type Ref } from 'react';
import { useI18n } from '../i18n/useI18n';

interface FieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  label: string;
  /** The error under the field; it marks the field invalid. */
  error?: string;
  /** The hint under the field, replaced by the error when there is one. */
  hint?: string;
  /** Shows the hint as fulfilled. */
  hintOk?: boolean;
  /** Something shown next to the label, for example a link. */
  labelAside?: ReactNode;
  ref?: Ref<HTMLInputElement>;
}

/**
 * A labelled text field with an error or a hint, linked to the input for screen readers.
 * The error is shown by a red border, a warning sign and a text, never by colour alone.
 */
export function TextField({ label, error, hint, hintOk = false, labelAside, ref, ...inputProps }: FieldProps) {
  const id = useId();
  const messageId = `${id}-message`;

  return (
    <div className="field">
      <FieldHead id={id} label={label} aside={labelAside} />
      <input
        ref={ref}
        className="input"
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error || hint ? messageId : undefined}
        {...inputProps}
      />
      <FieldMessage id={messageId} error={error} hint={hint} hintOk={hintOk} />
    </div>
  );
}

/**
 * A password field with a show / hide button.
 */
export function PasswordField({ label, error, hint, hintOk = false, labelAside, ref, ...inputProps }: FieldProps) {
  const { t } = useI18n();
  const id = useId();
  const messageId = `${id}-message`;
  const [visible, setVisible] = useState(false);

  return (
    <div className="field">
      <FieldHead id={id} label={label} aside={labelAside} />
      <div className="field__control">
        <input
          ref={ref}
          className="input input--pw"
          id={id}
          type={visible ? 'text' : 'password'}
          aria-invalid={error ? true : undefined}
          aria-describedby={error || hint ? messageId : undefined}
          {...inputProps}
        />
        <button className="pw-toggle" type="button" aria-pressed={visible} aria-controls={id} onClick={() => setVisible((value) => !value)}>
          {t(visible ? 'auth.hide' : 'auth.show')}
        </button>
      </div>
      <FieldMessage id={messageId} error={error} hint={hint} hintOk={hintOk} />
    </div>
  );
}

function FieldHead({ id, label, aside }: { id: string; label: string; aside?: ReactNode }) {
  const labelElement = (
    <label className="field__label" htmlFor={id}>
      {label}
    </label>
  );

  return aside ? (
    <div className="field__head">
      {labelElement}
      {aside}
    </div>
  ) : (
    labelElement
  );
}

function FieldMessage({ id, error, hint, hintOk }: { id: string; error?: string; hint?: string; hintOk: boolean }) {
  if (error) {
    return (
      <p className={hint ? 'hint hint--error' : 'error-text'} id={id}>
        {error}
      </p>
    );
  }

  if (hint) {
    return (
      <p className={hintOk ? 'hint hint--ok' : 'hint'} id={id}>
        {hint}
      </p>
    );
  }

  return null;
}
