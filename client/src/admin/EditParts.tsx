import type { ReactNode } from 'react';
import { Alert } from '../components/Alert';
import type { ReferenceRow } from './adminApi';
import { parseNumber, type EditFailure } from './editFailure';
import type { AdminMessageKey } from './i18n/uk';
import { formatNumber, useAdminText, useWorldName } from './i18n/useAdminText';

const editErrorKeys: Record<string, AdminMessageKey> = {
  required: 'admin.edit.error.required',
  too_short: 'admin.edit.error.tooShort',
  too_long: 'admin.edit.error.tooLong',
  out_of_bounds: 'admin.edit.error.outOfBounds',
  negative: 'admin.edit.error.negative',
  range_inverted: 'admin.edit.error.rangeInverted',
  unknown: 'admin.edit.error.unknown',
  class_mismatch: 'admin.edit.error.classMismatch',
  duplicate: 'admin.edit.error.duplicate',
  share_too_large: 'admin.edit.error.shareTooLarge',
  not_allowed_on_body: 'admin.edit.error.notAllowedOnBody',
  orbit_taken: 'admin.edit.error.orbitTaken',
  fixed_by_system: 'admin.edit.error.fixedBySystem',
  mass_zero: 'admin.edit.error.massZero',
  not_colonizable: 'admin.edit.error.notColonizable',
  not_applicable: 'admin.edit.error.notApplicable',
  not_a_number: 'admin.edit.error.notANumber',
  archived: 'admin.edit.error.archived',
  levels_missing: 'admin.edit.error.levelsMissing',
  levels_gap: 'admin.edit.error.levelsGap',
  cost_missing: 'admin.edit.error.costMissing',
  requirements_missing: 'admin.edit.error.requirementsMissing',
  levels_in_use: 'admin.edit.error.levelsInUse',
  state_unchanged: 'admin.edit.error.stateUnchanged',
  invalid: 'admin.edit.error.invalid',
  image_type_refused: 'admin.edit.error.imageTypeRefused',
  image_too_large: 'admin.edit.error.imageTooLarge',
};

/**
 * The errors of one input, under it.
 */
export function FieldError({ codes }: { codes: string[] | undefined }) {
  const { t } = useAdminText();

  if (!codes || codes.length === 0) {
    return null;
  }

  return <span className="admin-field__error">{codes.map((code) => (editErrorKeys[code] ? t(editErrorKeys[code]) : code)).join(' ')}</span>;
}

/**
 * A labelled input of a card with its errors.
 */
export function CardField({ label, codes, hint, children }: { label: string; codes?: string[]; hint?: string; children: ReactNode }) {
  return (
    <label className="admin-field">
      <span>{label}</span>
      {children}
      {hint ? <span className="hint">{hint}</span> : null}
      <FieldError codes={codes} />
    </label>
  );
}

/**
 * The banner of a refused change: "the data changed, reload", the problems of the rules or
 * the text of the error.
 */
export function FailureBanner({ failure, onReload }: { failure: EditFailure | null; onReload: () => void }) {
  const { t } = useAdminText();

  if (!failure) {
    return null;
  }

  if (failure.conflict) {
    return (
      <div className="admin-details">
        <Alert kind="error">{t('admin.edit.conflict')}</Alert>
        <div className="admin-form__actions">
          <button className="btn btn--secondary" type="button" onClick={onReload}>
            {t('admin.edit.reload')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <Alert kind="error">
      {failure.message ? t(failure.message) : null}
      {failure.problems.length > 0 ? (
        <ul className="admin-problems">
          {failure.problems.map((problem) => (
            <li key={problem}>
              <code>{problem}</code>
            </li>
          ))}
        </ul>
      ) : null}
    </Alert>
  );
}

/**
 * The mandatory reason of a change and the buttons of a card.
 */
export function ReasonAndActions({
  reason,
  onReason,
  codes,
  busy,
  canSave,
  onCancel,
}: {
  reason: string;
  onReason: (value: string) => void;
  codes?: string[];
  busy: boolean;
  canSave: boolean;
  onCancel: () => void;
}) {
  const { t } = useAdminText();

  return (
    <>
      <label className="admin-field admin-field--wide">
        <span>{t('admin.col.reason')}</span>
        <input className="input" value={reason} placeholder={t('admin.config.reasonHint')} onChange={(event) => onReason(event.target.value)} />
        <FieldError codes={codes} />
      </label>
      <div className="admin-form__actions">
        <button className="btn btn--primary" type="submit" disabled={!canSave || busy}>
          {t('admin.config.save')}
        </button>
        <button className="btn btn--secondary" type="button" onClick={onCancel}>
          {t('admin.card.cancel')}
        </button>
      </div>
    </>
  );
}

/** A row of the editor of the shares. */
export interface ShareDraft {
  key: number;
  resource_id: number;
  value: string;
}

/**
 * The editor of the resources of a field or a body: the resource and its coefficient per row,
 * add and remove. A field types its shares in per cent and sees the empty rock; a body types
 * the richness multiplier of every deposit (task 00009, decision 10).
 */
export function SharesEditor({
  rows,
  onChange,
  resources,
  mode,
  maxShare,
  codes,
}: {
  rows: ShareDraft[];
  onChange: (rows: ShareDraft[]) => void;
  resources: ReferenceRow[];
  mode: 'percent' | 'multiplier';
  maxShare?: number;
  codes?: string[];
}) {
  const { t, language } = useAdminText();
  const name = useWorldName();
  const used = new Set(rows.map((row) => row.resource_id));
  const free = resources.filter((resource) => !used.has(resource.id));
  const total = rows.reduce((sum, row) => sum + (parseNumber(row.value) ?? 0), 0);

  const update = (key: number, change: Partial<ShareDraft>) => onChange(rows.map((row) => (row.key === key ? { ...row, ...change } : row)));
  const nextKey = rows.reduce((max, row) => Math.max(max, row.key), 0) + 1;

  return (
    <fieldset className="admin-shares-editor">
      <legend>{t(mode === 'percent' ? 'admin.card.composition' : 'admin.card.deposits')}</legend>
      {mode === 'multiplier' ? <p className="hint">{t('admin.card.multiplierHint')}</p> : null}
      {rows.map((row) => (
        <div className="admin-shares-editor__row" key={row.key}>
          <label className="admin-field">
            <span>{t('admin.card.resource')}</span>
            <select className="input" value={row.resource_id} onChange={(event) => update(row.key, { resource_id: Number(event.target.value) })}>
              {resources
                .filter((resource) => resource.id === row.resource_id || !used.has(resource.id))
                .map((resource) => (
                  <option key={resource.id} value={resource.id} title={resource.code}>
                    {name(resource.name_key, resource.code)}
                  </option>
                ))}
            </select>
          </label>
          <label className="admin-field">
            <span>{t(mode === 'percent' ? 'admin.card.sharePercent' : 'admin.card.multiplier')}</span>
            <input className="input" inputMode="decimal" value={row.value} onChange={(event) => update(row.key, { value: event.target.value })} />
          </label>
          <button className="btn btn--secondary" type="button" onClick={() => onChange(rows.filter((other) => other.key !== row.key))}>
            {t('admin.card.remove')}
          </button>
        </div>
      ))}
      {mode === 'percent' ? (
        <p className="hint">
          {t('admin.card.rock', { percent: formatNumber(Math.max(0, 100 - total), language, 1) })}
          {maxShare !== undefined ? ` ${t('admin.card.maxShare', { percent: formatNumber(maxShare * 100, language, 1) })}` : null}
        </p>
      ) : null}
      <FieldError codes={codes} />
      <div className="admin-form__actions">
        <button
          className="btn btn--secondary"
          type="button"
          disabled={free.length === 0}
          onClick={() => onChange([...rows, { key: nextKey, resource_id: free[0].id, value: '' }])}
        >
          {t('admin.card.addResource')}
        </button>
      </div>
    </fieldset>
  );
}

/**
 * A name of the world with its code in the tooltip (task 00009, decision 9).
 */
export function WorldName({ nameKey, code }: { nameKey: string | null | undefined; code: string | null | undefined }) {
  const name = useWorldName();

  return <span title={code ?? undefined}>{name(nameKey, code)}</span>;
}
