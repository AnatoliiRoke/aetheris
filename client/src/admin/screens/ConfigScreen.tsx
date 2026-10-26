import { useEffect, useState, type FormEvent } from 'react';
import { Alert } from '../../components/Alert';
import type { ClockInfo, ConfigChange, ConfigParameter, Page, Row } from '../adminApi';
import { errorKey, fieldErrors, useAdmin } from '../adminContext';
import { DataTable, type Column, type Filter } from '../DataTable';
import type { AdminMessageKey } from '../i18n/uk';
import { formatMoment, useAdminText } from '../i18n/useAdminText';

/**
 * The configuration parameters (task 00007, decision 7): the table with the search, the
 * change in the row with a mandatory reason, the history and the rollback. The server checks
 * the type and the bounds; a change works at once, without a restart. The dangerous
 * parameters are shown but not changed.
 */
export function ConfigScreen() {
  const { t, language } = useAdminText();
  const [reloadKey, setReloadKey] = useState(0);
  const reloadTable = () => setReloadKey((value) => value + 1);

  const columns: Column<Row>[] = [
    { key: 'key', label: 'admin.col.key', sort: 'key', render: (row) => <code>{String(row['key'])}</code> },
    { key: 'value', label: 'admin.col.value', render: (row) => <code className="admin-value">{String(row['value'])}</code> },
    { key: 'type', label: 'admin.col.type', render: (row) => t(typeKey(row['type'] as ConfigParameter['type'])) },
    { key: 'minimum', label: 'admin.col.minimum' },
    { key: 'maximum', label: 'admin.col.maximum' },
    { key: 'description', label: 'admin.col.description' },
    { key: 'read_only', label: 'admin.col.readOnly' },
    { key: 'updated_by', label: 'admin.col.updatedBy' },
    { key: 'updated_at', label: 'admin.col.updatedAt', sort: 'updated_at', render: (row) => formatMoment(row['updated_at'] as string, language) },
  ];

  const filters: Filter[] = [
    {
      name: 'group',
      label: 'admin.config.group',
      options: ['admin', 'security', 'scheduler', 'world', 'test'].map((group) => ({ value: group, label: group })),
    },
    {
      name: 'read_only',
      label: 'admin.col.readOnly',
      options: [
        { value: 'true', label: t('admin.table.yes') },
        { value: 'false', label: t('admin.table.no') },
      ],
    },
  ];

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.config.title')}</h1>
        <p className="hint">{t('admin.config.lead')}</p>
      </header>
      <ClockPanel />
      <DataTable
        table="config"
        caption="admin.config.title"
        columns={columns}
        filters={filters}
        rowKey={(row) => String(row['key'])}
        reloadKey={reloadKey}
        expanded={(row) => <ParameterDetails parameter={row as unknown as ConfigParameter} onChanged={reloadTable} />}
      />
    </>
  );
}

function typeKey(type: ConfigParameter['type']): AdminMessageKey {
  switch (type) {
    case 'integer':
      return 'admin.type.integer';
    case 'decimal':
      return 'admin.type.decimal';
    case 'boolean':
      return 'admin.type.boolean';
    default:
      return 'admin.type.text';
  }
}

/**
 * The time multiplier, shown read-only: its change is a dangerous action of a later task.
 */
function ClockPanel() {
  const { t } = useAdminText();
  const { api, can, onError } = useAdmin();
  const [clock, setClock] = useState<ClockInfo | null>(null);

  useEffect(() => {
    let active = true;

    if (!can('admin.clock_read')) {
      return undefined;
    }

    api.clock().then(
      (answer) => {
        if (active) {
          setClock(answer);
        }
      },
      (failure: unknown) => onError(failure),
    );

    return () => {
      active = false;
    };
  }, [api, can, onError]);

  if (!clock) {
    return null;
  }

  return (
    <p className="hint">{t('admin.config.clock', { multiplier: clock.multiplier, max: clock.max_multiplier })}</p>
  );
}

/**
 * The change of one parameter, its history and the rollback of a change.
 */
function ParameterDetails({ parameter, onChanged }: { parameter: ConfigParameter; onChanged: () => void }) {
  const { t, language } = useAdminText();
  const { api, can, onError } = useAdmin();
  const [value, setValue] = useState(parameter.value);
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [history, setHistory] = useState<Page<ConfigChange> | null>(null);
  const [historyVersion, setHistoryVersion] = useState(0);
  const canChange = !parameter.read_only && can('admin.config_changed');
  const canRollback = !parameter.read_only && can('admin.config_rolled_back');

  useEffect(() => {
    let active = true;

    api.configHistory(parameter.key).then(
      (answer) => {
        if (active) {
          setHistory(answer);
        }
      },
      (failure: unknown) => onError(failure),
    );

    return () => {
      active = false;
    };
  }, [api, parameter.key, onError, historyVersion]);

  const describe = (failure: unknown): string => {
    const fields = fieldErrors(failure);

    if (fields['reason']?.length) {
      return t('admin.config.reasonRequired');
    }

    if (fields['value']?.length || fields['changeid']?.length) {
      return t('admin.config.valueInvalid', { type: t(typeKey(parameter.type)), min: parameter.minimum ?? '—', max: parameter.maximum ?? '—' });
    }

    return t(errorKey(failure));
  };

  const run = async (action: () => Promise<void>) => {
    setError(null);
    setSaved(false);
    setBusy(true);

    try {
      await action();
      setSaved(true);
      setReason('');
      setHistoryVersion((current) => current + 1);
      onChanged();
    } catch (failure) {
      onError(failure);
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  };

  const onSave = (event: FormEvent) => {
    event.preventDefault();

    if (reason.trim() === '') {
      setError(t('admin.config.reasonRequired'));
      return;
    }

    void run(() => api.changeConfig(parameter.key, value, reason.trim()));
  };

  const onRollback = (change: ConfigChange) => {
    if (reason.trim() === '') {
      setError(t('admin.config.reasonRequired'));
      return;
    }

    void run(async () => {
      await api.rollbackConfig(parameter.key, change.id, reason.trim());
      setValue(change.old_value);
    });
  };

  return (
    <div className="admin-details">
      {parameter.read_only ? <Alert kind="info">{t('admin.config.readOnly')}</Alert> : null}
      <form className="admin-form admin-form--inline" onSubmit={onSave}>
        <label className="admin-field">
          <span>{t('admin.col.value')}</span>
          {parameter.type === 'boolean' ? (
            <select className="input" value={value} disabled={!canChange} onChange={(event) => setValue(event.target.value)}>
              <option value="true">true</option>
              <option value="false">false</option>
            </select>
          ) : (
            <input
              className="input"
              value={value}
              disabled={!canChange}
              inputMode={parameter.type === 'text' ? 'text' : 'decimal'}
              onChange={(event) => setValue(event.target.value)}
            />
          )}
        </label>
        <label className="admin-field admin-field--wide">
          <span>{t('admin.col.reason')}</span>
          <input
            className="input"
            value={reason}
            disabled={!canChange && !canRollback}
            placeholder={t('admin.config.reasonHint')}
            onChange={(event) => setReason(event.target.value)}
          />
        </label>
        <button className="btn btn--primary" type="submit" disabled={!canChange || busy}>
          {t('admin.config.save')}
        </button>
      </form>
      {error ? <Alert kind="error">{error}</Alert> : null}
      {saved ? <Alert kind="success">{t('admin.config.saved')}</Alert> : null}

      <h3>{t('admin.config.history')}</h3>
      {history && history.items.length === 0 ? <p className="hint">{t('admin.config.noHistory')}</p> : null}
      {history && history.items.length > 0 ? (
        <table className="admin-history">
          <thead>
            <tr>
              <th scope="col">{t('admin.col.updatedAt')}</th>
              <th scope="col">{t('admin.col.admin')}</th>
              <th scope="col">{t('admin.col.oldValue')}</th>
              <th scope="col">{t('admin.col.newValue')}</th>
              <th scope="col">{t('admin.col.reason')}</th>
              <th scope="col">{t('admin.config.rollback')}</th>
            </tr>
          </thead>
          <tbody>
            {history.items.map((change) => (
              <tr key={change.id}>
                <td>{formatMoment(change.changed_at, language)}</td>
                <td>{change.admin_name}</td>
                <td>
                  <code>{change.old_value}</code>
                </td>
                <td>
                  <code>{change.new_value}</code>
                </td>
                <td>{change.rolled_back_change_id ? t('admin.config.rolledBack', { reason: change.reason }) : change.reason}</td>
                <td>
                  <button className="btn btn--secondary" type="button" disabled={!canRollback || busy} onClick={() => onRollback(change)}>
                    {t('admin.config.rollbackTo', { value: change.old_value })}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
