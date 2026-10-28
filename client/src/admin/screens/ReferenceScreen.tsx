import { useCallback, useEffect, useMemo, useState, type ChangeEvent, type FormEvent } from 'react';
import { Alert } from '../../components/Alert';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { useTexts } from '../../texts/useTexts';
import type { Page } from '../adminApi';
import { useAdmin } from '../adminContext';
import { adminRoutes } from '../adminRoutes';
import { AdminDialog } from '../AdminDialog';
import { CardField, FailureBanner, FieldError, ReasonAndActions } from '../EditParts';
import { describeFailure, parseNumber, type EditFailure } from '../editFailure';
import { useAdminText, useWorldName } from '../i18n/useAdminText';
import type {
  ReferenceApi,
  ReferenceField,
  ReferenceKind,
  ReferenceKinds,
  ReferenceRecord,
  ReferenceRow,
  ReferenceValue,
  ReferenceValues,
} from '../referenceApi';

/** The rows of the kinds a form or a list picks from, by kind. */
type RowsByKind = Record<string, ReferenceRow[]>;

/** The address of the form of a new row. */
const newRecord = 'new';

/**
 * The reference kinds of the admin panel (task 00023, decision 1), like the reference menus of
 * Odoo: the kinds on the left, the list of the chosen kind, and the form of a row. The server
 * describes every kind with its fields, so the screen knows no kind: a new kind of a module shows
 * up here without a change of the client.
 */
export function ReferenceScreen() {
  const { t } = useAdminText();
  const { references, onError } = useAdmin();
  const { path, navigate } = useRouter();
  const name = useWorldName();
  const [described, setDescribed] = useState<ReferenceKinds | null>(null);

  useEffect(() => {
    let active = true;

    references.kinds().then(
      (answer) => {
        if (active) {
          setDescribed(answer);
        }
      },
      (failure: unknown) => onError(failure),
    );

    return () => {
      active = false;
    };
  }, [references, onError]);

  const [kindName, recordId] = path.slice(adminRoutes.reference.length + 1).split('/');
  const kinds = useMemo(() => described?.kinds ?? [], [described]);
  const kind = kinds.find((entry) => entry.kind === kindName);

  useEffect(() => {
    if (!kind && kinds.length > 0) {
      navigate(`${adminRoutes.reference}/${kinds[0].kind}`, { replace: true });
    }
  }, [kind, kinds, navigate]);

  if (!described) {
    return (
      <p className="hint" role="status">
        {t('admin.loading')}
      </p>
    );
  }

  return (
    <div className="admin-reference">
      <nav className="admin-reference__menu" aria-label={t('admin.reference.menu')}>
        {kinds.length === 0 ? <p className="hint">{t('admin.reference.noKinds')}</p> : null}
        {kinds.map((entry) => (
          <Link
            key={entry.kind}
            className={entry === kind ? 'admin__link admin__link--active' : 'admin__link'}
            aria-current={entry === kind ? 'page' : undefined}
            to={`${adminRoutes.reference}/${entry.kind}`}
          >
            {name(entry.title_key, entry.kind)}
          </Link>
        ))}
      </nav>
      <section className="admin-reference__body">
        {kind && recordId ? (
          <ReferenceForm key={`${kind.kind}/${recordId}`} kind={kind} described={described} recordId={recordId === newRecord ? null : Number(recordId)} />
        ) : null}
        {kind && !recordId ? <ReferenceList key={kind.kind} kind={kind} described={described} /> : null}
      </section>
    </div>
  );
}

/**
 * Reads every row of the kinds the fields pick from, the archived ones too, so a stored link to
 * an archived row keeps its name. A kind the administrator may not read stays empty.
 */
function useRowsOf(references: ReferenceApi, describedKinds: ReferenceKind[], fields: ReferenceField[]): RowsByKind | null {
  const wanted = useMemo(() => [...new Set(referencedKinds(fields))].sort().join(','), [fields]);
  const [rows, setRows] = useState<RowsByKind | null>(null);

  useEffect(() => {
    let active = true;
    const names = wanted.length === 0 ? [] : wanted.split(',');

    Promise.all(
      names.map(async (kindName) => {
        const kind = describedKinds.find((entry) => entry.kind === kindName);

        return [kindName, kind ? await allRows(references, kind).catch(() => []) : []] as const;
      }),
    ).then((entries) => {
      if (active) {
        setRows(Object.fromEntries(entries));
      }
    });

    return () => {
      active = false;
    };
  }, [references, describedKinds, wanted]);

  return rows;
}

async function allRows(references: ReferenceApi, kind: ReferenceKind): Promise<ReferenceRow[]> {
  const rows: ReferenceRow[] = [];

  for (let page = 1; ; page += 1) {
    const answer: Page<ReferenceRow> = await references.list(kind, { page, state: 'all' });
    rows.push(...answer.items);

    if (answer.items.length === 0 || rows.length >= answer.total) {
      return rows;
    }
  }
}

function referencedKinds(fields: ReferenceField[]): string[] {
  return fields.flatMap((field) => [
    ...(field.reference_kind ? [field.reference_kind] : []),
    ...(field.options ?? []).flatMap((option) => (option.reference_kind ? [option.reference_kind] : [])),
    ...referencedKinds(field.fields ?? []),
  ]);
}

/**
 * The list of a kind: the columns the server marks for the list, the picture, the code, the name,
 * the state and the version; the search by name and code, the filter «Архівні» and the creation.
 */
function ReferenceList({ kind, described }: { kind: ReferenceKind; described: ReferenceKinds }) {
  const { t } = useAdminText();
  const { references, can, onError } = useAdmin();
  const name = useWorldName();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [archived, setArchived] = useState(false);
  const [rows, setRows] = useState<Page<ReferenceRow> | null>(null);
  const columns = kind.fields.filter((field) => field.in_list && field.name !== 'name');

  const options = useRowsOf(references, described.kinds, columns);

  useEffect(() => {
    let active = true;

    references.list(kind, { page, search: search.trim() || undefined, state: archived ? 'archived' : 'active' }).then(
      (answer) => {
        if (active) {
          setRows(answer);
        }
      },
      (failure: unknown) => onError(failure),
    );

    return () => {
      active = false;
    };
  }, [references, kind, page, search, archived, onError]);

  const pages = rows ? Math.max(1, Math.ceil(rows.total / Math.max(1, rows.page_size))) : 1;

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{name(kind.title_key, kind.kind)}</h1>
        <p className="hint">{t('admin.reference.lead')}</p>
      </header>
      <div className="admin-table">
        <div className="admin-table__tools">
          <label className="admin-field admin-field--wide">
            <span>{t('admin.reference.search')}</span>
            <input
              className="input"
              type="search"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
          </label>
          <label className="admin-check">
            <input
              type="checkbox"
              checked={archived}
              onChange={(event) => {
                setArchived(event.target.checked);
                setPage(1);
              }}
            />
            {t('admin.reference.archivedFilter')}
          </label>
          {can(kind.permissions.added) ? (
            <Link className="btn btn--primary" to={`${adminRoutes.reference}/${kind.kind}/${newRecord}`}>
              {t('admin.reference.create')}
            </Link>
          ) : null}
        </div>
        {rows ? (
          <div className="admin-table__scroll">
            <table>
              <caption className="visually-hidden">{name(kind.title_key, kind.kind)}</caption>
              <thead>
                <tr>
                  <th scope="col">{t('admin.reference.picture')}</th>
                  <th scope="col">{t('admin.col.name')}</th>
                  {columns.map((field) => (
                    <th scope="col" key={field.name}>
                      {name(field.label_key, field.name)}
                    </th>
                  ))}
                  <th scope="col">{t('admin.col.code')}</th>
                  <th scope="col">{t('admin.reference.state')}</th>
                  <th scope="col">{t('admin.reference.version')}</th>
                </tr>
              </thead>
              <tbody>
                {rows.items.length === 0 ? (
                  <tr>
                    <td colSpan={columns.length + 5}>{t('admin.reference.empty')}</td>
                  </tr>
                ) : null}
                {rows.items.map((row) => (
                  <tr key={row.id}>
                    <td>
                      <Picture imageKey={row.image_key} label={name(row.name_key, row.code)} />
                    </td>
                    <td>
                      <Link to={`${adminRoutes.reference}/${kind.kind}/${row.id}`} title={row.code}>
                        {name(row.name_key, row.code)}
                      </Link>
                    </td>
                    {columns.map((field) => (
                      <td key={field.name} className={field.type === 'number' ? 'num' : undefined}>
                        <ShownValue field={field} value={row.values[field.name]} options={options ?? {}} />
                      </td>
                    ))}
                    <td>
                      <code>{row.code}</code>
                    </td>
                    <td>{t(row.is_archived ? 'admin.reference.archived' : 'admin.reference.active')}</td>
                    <td className="num">{row.version}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="hint" role="status">
            {t('admin.loading')}
          </p>
        )}
        <div className="admin-table__pager">
          <button className="btn btn--secondary" type="button" disabled={page <= 1} onClick={() => setPage(page - 1)}>
            {t('admin.reference.previous')}
          </button>
          <span>{t('admin.reference.page', { page, pages })}</span>
          <button className="btn btn--secondary" type="button" disabled={page >= pages} onClick={() => setPage(page + 1)}>
            {t('admin.reference.next')}
          </button>
        </div>
      </div>
    </>
  );
}

/**
 * A value of a list column: the name of a linked row, the label of an option, a yes or a no.
 */
function ShownValue({ field, value, options }: { field: ReferenceField; value: ReferenceValue; options: RowsByKind }) {
  const { t } = useAdminText();
  const name = useWorldName();

  if (field.type === 'flag') {
    return <>{t(value === true ? 'admin.reference.yes' : 'admin.reference.no')}</>;
  }

  if (field.type === 'select') {
    const option = field.options?.find((entry) => entry.value === value);

    return <>{option ? name(option.label_key, option.value) : '—'}</>;
  }

  if (field.type === 'reference') {
    const row = (options[field.reference_kind ?? ''] ?? []).find((entry) => entry.id === value);

    return <>{row ? name(row.name_key, row.code) : '—'}</>;
  }

  return <>{value === null || value === undefined || typeof value === 'object' ? '—' : String(value)}</>;
}

/**
 * The picture of a row, or the neutral placeholder when the row has none or its file is gone.
 */
function Picture({ imageKey, label }: { imageKey: string | null | undefined; label: string }) {
  const { t } = useAdminText();
  const { references } = useAdmin();
  const [broken, setBroken] = useState(false);

  if (!imageKey || broken) {
    return (
      <span className="admin-reference__picture admin-reference__picture--empty" role="img" aria-label={t('admin.reference.noPicture')}>
        ?
      </span>
    );
  }

  return <img className="admin-reference__picture" src={references.imageUrl(imageKey)} alt={label} onError={() => setBroken(true)} />;
}

/**
 * The form of a row: every field drawn by its type, the name and the description in every
 * language, the reason, the archive and the return from the archive. Without the permission of
 * the change the form is read only.
 */
function ReferenceForm({ kind, described, recordId }: { kind: ReferenceKind; described: ReferenceKinds; recordId: number | null }) {
  const { t } = useAdminText();
  const { references, can, onError } = useAdmin();
  const { reload: reloadTexts } = useTexts();
  const { navigate } = useRouter();
  const name = useWorldName();
  const [record, setRecord] = useState<ReferenceRecord | null>(null);
  const [values, setValues] = useState<ReferenceValues | null>(recordId === null ? emptyValues(kind.fields) : null);
  const [reason, setReason] = useState('');
  const [failure, setFailure] = useState<EditFailure | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [archiving, setArchiving] = useState(false);
  const [generation, setGeneration] = useState(0);
  const options = useRowsOf(references, described.kinds, kind.fields);
  const editable = recordId === null ? can(kind.permissions.added) : can(kind.permissions.updated);

  useEffect(() => {
    if (recordId === null) {
      return;
    }

    let active = true;

    references.read(kind, recordId).then(
      (answer) => {
        if (active) {
          setRecord(answer);
          setValues(formValues(kind.fields, answer.values));
        }
      },
      (error: unknown) => {
        onError(error);
        setFailure(describeFailure(error));
      },
    );

    return () => {
      active = false;
    };
  }, [references, kind, recordId, generation, onError]);

  const reloadRecord = useCallback(() => {
    setFailure(null);
    setGeneration((value) => value + 1);
  }, []);

  if (!values) {
    return failure ? (
      <FailureBanner failure={failure} onReload={reloadRecord} />
    ) : (
      <p className="hint" role="status">
        {t('admin.loading')}
      </p>
    );
  }

  const missingTexts = kind.fields.some(
    (field) => field.type === 'text' && field.has_languages && field.is_required && described.languages.some((language) => !textOf(values[field.name], language).trim()),
  );

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);
    setSaved(false);

    const { payload, errors } = toPayload(kind.fields, values, '');

    if (Object.keys(errors).length > 0) {
      setFailure({ fields: errors, conflict: false, problems: [], message: 'admin.error.validation' });
      return;
    }

    setBusy(true);

    try {
      const answer =
        recordId === null
          ? await references.create(kind, payload, reason.trim())
          : await references.update(kind, recordId, record?.version ?? 0, payload, reason.trim());
      setReason('');
      setSaved(true);
      reloadTexts();

      if (recordId === null) {
        navigate(`${adminRoutes.reference}/${kind.kind}/${answer.id}`);
      } else {
        reloadRecord();
      }
    } catch (error) {
      onError(error);
      setFailure(describeFailure(error));
    } finally {
      setBusy(false);
    }
  };

  const title = recordId === null ? t('admin.reference.createTitle', { kind: name(kind.title_key, kind.kind) }) : name(record?.name_key, record?.code);

  return (
    <form className="admin-form admin-card panel" onSubmit={(event) => void onSubmit(event)} noValidate aria-label={title}>
      <div className="admin-form__actions">
        <Link className="btn btn--secondary" to={`${adminRoutes.reference}/${kind.kind}`}>
          {t('admin.reference.back')}
        </Link>
      </div>
      <h2 className="admin-screen__title">{title}</h2>
      {record ? (
        <dl className="admin-card__facts">
          <dt>{t('admin.col.code')}</dt>
          <dd>
            <code>{record.code}</code>
          </dd>
          <dt>{t('admin.reference.state')}</dt>
          <dd>{t(record.is_archived ? 'admin.reference.archived' : 'admin.reference.active')}</dd>
          <dt>{t('admin.reference.version')}</dt>
          <dd>{record.version}</dd>
        </dl>
      ) : (
        <p className="hint">{t('admin.reference.codeHint')}</p>
      )}
      {editable ? null : <Alert kind="info">{t('admin.reference.readOnly')}</Alert>}
      <fieldset className="admin-card__group" disabled={!editable || busy}>
        {kind.fields.map((field) => (
          <FieldInput
            key={field.name}
            field={field}
            path={field.name}
            value={values[field.name]}
            row={values}
            siblings={kind.fields}
            onChange={(value) => setValues({ ...values, [field.name]: value })}
            languages={described.languages}
            maxLength={described.max_length}
            imageTypes={described.image_types}
            imageMaxBytes={described.image_max_bytes}
            options={options ?? {}}
            failure={failure}
          />
        ))}
      </fieldset>
      <FailureBanner failure={failure} onReload={reloadRecord} />
      {saved ? <Alert kind="success">{t('admin.reference.saved')}</Alert> : null}
      {editable ? (
        <ReasonAndActions
          reason={reason}
          onReason={setReason}
          codes={failure?.fields['reason']}
          busy={busy}
          canSave={reason.trim().length > 0 && !missingTexts}
          onCancel={() => navigate(`${adminRoutes.reference}/${kind.kind}`)}
        />
      ) : null}
      {record && can(record.is_archived ? kind.permissions.restored : kind.permissions.archived) ? (
        <div className="admin-form__actions">
          <button className="btn btn--secondary" type="button" onClick={() => setArchiving(true)}>
            {t(record.is_archived ? 'admin.reference.restore' : 'admin.reference.archive')}
          </button>
        </div>
      ) : null}
      {archiving && record ? (
        <ArchiveDialog
          kind={kind}
          record={record}
          onClose={() => setArchiving(false)}
          onDone={() => {
            setArchiving(false);
            reloadRecord();
          }}
        />
      ) : null}
    </form>
  );
}

/**
 * The confirmation of the archive or of the return from it, with its reason.
 */
function ArchiveDialog({ kind, record, onClose, onDone }: { kind: ReferenceKind; record: ReferenceRecord; onClose: () => void; onDone: () => void }) {
  const { t } = useAdminText();
  const { references, onError } = useAdmin();
  const name = useWorldName();
  const [reason, setReason] = useState('');
  const [failure, setFailure] = useState<EditFailure | null>(null);
  const [busy, setBusy] = useState(false);
  const title = t(record.is_archived ? 'admin.reference.restore' : 'admin.reference.archive');

  const onConfirm = async () => {
    setFailure(null);
    setBusy(true);

    try {
      await references.setArchived(kind, record.id, record.version, !record.is_archived, reason.trim());
      onDone();
    } catch (error) {
      onError(error);
      setFailure(describeFailure(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AdminDialog title={title} onClose={onClose}>
      <p>{t(record.is_archived ? 'admin.reference.confirmRestore' : 'admin.reference.confirmArchive', { name: name(record.name_key, record.code) })}</p>
      <label className="admin-field admin-field--wide">
        <span>{t('admin.col.reason')}</span>
        <input className="input" value={reason} placeholder={t('admin.config.reasonHint')} onChange={(event) => setReason(event.target.value)} />
        <FieldError codes={failure?.fields['reason']} />
      </label>
      <FailureBanner failure={failure} onReload={onDone} />
      <div className="admin-form__actions">
        <button className="btn btn--primary" type="button" disabled={busy || reason.trim().length === 0} onClick={() => void onConfirm()}>
          {title}
        </button>
        <button className="btn btn--secondary" type="button" onClick={onClose}>
          {t('admin.card.cancel')}
        </button>
      </div>
    </AdminDialog>
  );
}

interface FieldInputProps {
  field: ReferenceField;
  path: string;
  value: ReferenceValue;
  /** The row the field belongs to, for a field whose kind another field of the row names. */
  row: ReferenceValues;
  /** The fields of the row. */
  siblings: ReferenceField[];
  onChange: (value: ReferenceValue) => void;
  languages: string[];
  maxLength: number;
  imageTypes: string[];
  imageMaxBytes: number;
  options: RowsByKind;
  failure: EditFailure | null;
}

/**
 * An input of the form drawn by the type of its field; a list field draws its rows with the same
 * inputs.
 */
function FieldInput(props: FieldInputProps) {
  const { field, path, value, row, siblings, onChange, languages, maxLength, options, failure } = props;
  const { t } = useAdminText();
  const name = useWorldName();
  const label = name(field.label_key, field.name);
  const codes = failure?.fields[path];

  switch (field.type) {
    case 'text':
      return field.has_languages ? (
        <div className="admin-form admin-form--inline">
          {languages.map((language) => (
            <CardField key={language} label={`${label} (${language})`} codes={failure?.fields[`${path}.${language}`]}>
              <input
                className="input"
                maxLength={maxLength}
                value={textOf(value, language)}
                onChange={(event) => onChange({ ...(isTexts(value) ? value : {}), [language]: event.target.value })}
              />
            </CardField>
          ))}
        </div>
      ) : (
        <CardField label={label} codes={codes}>
          <input className="input" maxLength={maxLength} value={typeof value === 'string' ? value : ''} onChange={(event) => onChange(event.target.value)} />
        </CardField>
      );

    case 'number':
      return (
        <CardField label={label} codes={codes}>
          <input className="input" inputMode={field.is_decimal ? 'decimal' : 'numeric'} value={value === null || value === undefined ? '' : String(value)} onChange={(event) => onChange(event.target.value)} />
        </CardField>
      );

    case 'flag':
      return (
        <label className="admin-check">
          <input type="checkbox" checked={value === true} onChange={(event) => onChange(event.target.checked)} />
          {label}
          <FieldError codes={codes} />
        </label>
      );

    case 'select':
      return (
        <CardField label={label} codes={codes}>
          <select className="input" value={typeof value === 'string' ? value : ''} onChange={(event) => onChange(event.target.value)}>
            {field.is_required ? null : <option value="">{t('admin.reference.none')}</option>}
            {(field.options ?? []).map((option) => (
              <option key={option.value} value={option.value}>
                {name(option.label_key, option.value)}
              </option>
            ))}
          </select>
        </CardField>
      );

    case 'reference': {
      const kindName = field.reference_kind ?? dependentKind(field, row, siblings);
      const rows = options[kindName ?? ''] ?? [];

      return (
        <CardField label={label} codes={codes}>
          <select
            className="input"
            value={typeof value === 'number' ? String(value) : ''}
            onChange={(event) => onChange(event.target.value === '' ? null : Number(event.target.value))}
          >
            <option value="">{t('admin.reference.none')}</option>
            {rows
              .filter((entry) => !entry.is_archived || entry.id === value)
              .map((entry) => (
                <option key={entry.id} value={entry.id} title={entry.code}>
                  {name(entry.name_key, entry.code)}
                </option>
              ))}
          </select>
        </CardField>
      );
    }

    case 'image':
      return <ImageInput {...props} />;

    default:
      return <ListInput {...props} />;
  }
}

/**
 * The kind of a reference field whose kind the chosen option of another field of the row names,
 * for example the target of a requirement.
 */
function dependentKind(field: ReferenceField, row: ReferenceValues, siblings: ReferenceField[]): string | undefined {
  const source = siblings.find((sibling) => sibling.name === field.reference_kind_from);

  return source?.options?.find((option) => option.value === row[source.name])?.reference_kind;
}

/**
 * The rows of a list field: the levels, the costs, the effects or the requirements, each row with
 * the inputs of the columns of the field, added and removed in place.
 */
function ListInput(props: FieldInputProps) {
  const { field, path, value, onChange, failure } = props;
  const { t } = useAdminText();
  const name = useWorldName();
  const rows = Array.isArray(value) ? value : [];
  const columns = field.fields ?? [];

  const update = (index: number, changed: ReferenceValues) => onChange(rows.map((entry, position) => (position === index ? changed : entry)));
  const added = (): ReferenceValues => {
    const fresh = emptyValues(columns);

    // A new level follows the last one.
    if (columns.some((column) => column.name === 'level')) {
      fresh['level'] = String(rows.length + 1);
    }

    return fresh;
  };

  return (
    <fieldset className="admin-shares-editor">
      <legend>{name(field.label_key, field.name)}</legend>
      <FieldError codes={failure?.fields[path]} />
      {rows.map((entry, index) => (
        <div className="admin-reference__row" key={index}>
          <div className="admin-shares-editor__row">
            {columns.map((column) => (
              <FieldInput
                key={column.name}
                {...props}
                field={column}
                path={`${path}.${index}.${column.name}`}
                value={entry[column.name]}
                row={entry}
                siblings={columns}
                onChange={(changed) => update(index, { ...entry, [column.name]: changed })}
              />
            ))}
            <button className="btn btn--secondary" type="button" onClick={() => onChange(rows.filter((_, position) => position !== index))}>
              {t('admin.card.remove')}
            </button>
          </div>
          <FieldError codes={failure?.fields[`${path}.${index}`]} />
        </div>
      ))}
      <div className="admin-form__actions">
        <button className="btn btn--secondary" type="button" onClick={() => onChange([...rows, added()])}>
          {t('admin.reference.addRow', { name: name(field.label_key, field.name) })}
        </button>
      </div>
    </fieldset>
  );
}

/**
 * The picture of a row: the picture or the placeholder, the choice of a PNG or WebP file checked
 * against the types and the size of the configuration before it is sent, and the removal.
 */
function ImageInput({ field, path, value, onChange, imageTypes, imageMaxBytes, failure }: FieldInputProps) {
  const { t } = useAdminText();
  const { references, onError } = useAdmin();
  const name = useWorldName();
  const [problem, setProblem] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const imageKey = typeof value === 'string' && value.length > 0 ? value : null;

  const onFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    setProblem(null);

    if (!file) {
      return;
    }

    if (!imageTypes.includes(file.type)) {
      setProblem('image_type_refused');
      return;
    }

    if (file.size > imageMaxBytes) {
      setProblem('image_too_large');
      return;
    }

    setBusy(true);

    try {
      const answer = await references.uploadImage(file);
      onChange(answer.image_key);
    } catch (error) {
      onError(error);
      setProblem(describeFailure(error).fields['file']?.[0] ?? 'image_type_refused');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="admin-reference__image">
      <Picture imageKey={imageKey} label={name(field.label_key, field.name)} />
      <CardField label={name(field.label_key, field.name)} hint={t('admin.reference.imageHint', { types: imageTypes.join(', '), size: Math.round(imageMaxBytes / 1024) })} codes={problem ? [problem] : failure?.fields[path]}>
        <input className="input" type="file" accept={imageTypes.join(',')} disabled={busy} onChange={(event) => void onFile(event)} />
      </CardField>
      {imageKey ? (
        <button className="btn btn--secondary" type="button" onClick={() => onChange(null)}>
          {t('admin.reference.removePicture')}
        </button>
      ) : null}
    </div>
  );
}

function isTexts(value: ReferenceValue): value is Record<string, string> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function textOf(value: ReferenceValue, language: string): string {
  return isTexts(value) ? (value[language] ?? '') : '';
}

/**
 * The values of a new row: empty texts, no numbers, the first option of a mandatory select, no
 * link, no picture and no rows.
 */
function emptyValues(fields: ReferenceField[]): ReferenceValues {
  return Object.fromEntries(
    fields.map((field) => {
      switch (field.type) {
        case 'text':
          return [field.name, field.has_languages ? {} : ''];
        case 'flag':
          return [field.name, false];
        case 'select':
          return [field.name, field.is_required ? (field.options?.[0]?.value ?? '') : ''];
        case 'number':
          return [field.name, ''];
        case 'reference':
        case 'image':
          return [field.name, null];
        default:
          return [field.name, []];
      }
    }),
  );
}

/**
 * The values of a stored row for the form: the numbers become texts the inputs edit.
 */
function formValues(fields: ReferenceField[], values: ReferenceValues): ReferenceValues {
  const result: ReferenceValues = { ...emptyValues(fields) };

  for (const field of fields) {
    const value = values[field.name];

    if (field.type === 'number') {
      result[field.name] = value === null || value === undefined ? '' : String(value);
    } else if (field.fields && Array.isArray(value)) {
      result[field.name] = value.map((entry) => formValues(field.fields ?? [], entry));
    } else if (value !== undefined) {
      result[field.name] = value;
    }
  }

  return result;
}

/**
 * The values of the form as the server takes them: the numbers parsed, an empty choice as no value;
 * a text that is not a number is refused at its field.
 */
function toPayload(fields: ReferenceField[], values: ReferenceValues, prefix: string): { payload: ReferenceValues; errors: Record<string, string[]> } {
  const payload: ReferenceValues = {};
  const errors: Record<string, string[]> = {};

  for (const field of fields) {
    const path = `${prefix}${field.name}`;
    const value = values[field.name];

    if (field.type === 'number') {
      const text = value === null || value === undefined ? '' : String(value);
      const number = parseNumber(text);

      if (text.trim() !== '' && number === null) {
        errors[path] = ['not_a_number'];
      }

      payload[field.name] = number;
    } else if (field.type === 'select') {
      payload[field.name] = value === '' ? null : value;
    } else if (field.fields) {
      const rows = Array.isArray(value) ? value : [];

      payload[field.name] = rows.map((entry, index) => {
        const converted = toPayload(field.fields ?? [], entry, `${path}.${index}.`);
        Object.assign(errors, converted.errors);

        return converted.payload;
      });
    } else {
      payload[field.name] = value ?? null;
    }
  }

  return { payload, errors };
}
