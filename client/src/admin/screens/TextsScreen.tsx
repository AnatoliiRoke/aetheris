import { useState, type FormEvent } from 'react';
import { Alert } from '../../components/Alert';
import { useTexts } from '../../texts/useTexts';
import { AdminDialog } from '../AdminDialog';
import type { Row, TextRow } from '../adminApi';
import { useAdmin } from '../adminContext';
import { DataTable, type Column, type Filter } from '../DataTable';
import { CardField, FailureBanner, ReasonAndActions } from '../EditParts';
import { describeFailure, type EditFailure } from '../editFailure';
import { formatMoment, useAdminText } from '../i18n/useAdminText';
import { useTextSettings } from '../useTextSettings';

/**
 * The screen «Тексти» (task 00022, decision 6): every key of the game data in every language,
 * the search by the key and the text, the filters by language and by missing translations, and
 * the change of one text with a mandatory reason, the journal and the version check.
 */
export function TextsScreen() {
  const { t, language } = useAdminText();
  const settings = useTextSettings();
  const [openRow, setOpenRow] = useState<TextRow | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const columns: Column<Row>[] = [
    { key: 'key', label: 'admin.col.key', render: (row) => <code>{String(row['key'])}</code> },
    { key: 'language', label: 'admin.col.language' },
    {
      key: 'text',
      label: 'admin.col.text',
      render: (row) => (row['text'] === null ? <span className="hint">{t('admin.texts.missingText')}</span> : String(row['text'])),
    },
    { key: 'version', label: 'admin.col.version' },
    { key: 'updated_by', label: 'admin.col.updatedBy' },
    { key: 'updated_at', label: 'admin.col.updatedAt', render: (row) => formatMoment(row['updated_at'] as string | null, language) },
  ];

  const filters: Filter[] = [
    { name: 'language', label: 'admin.col.language', options: (settings?.languages ?? []).map((value) => ({ value, label: value })) },
    { name: 'missing', label: 'admin.texts.missing', options: [{ value: 'true', label: t('admin.table.yes') }] },
  ];

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.texts.title')}</h1>
        <p className="hint">{t('admin.texts.lead')}</p>
      </header>
      <DataTable
        table="texts"
        caption="admin.texts.title"
        columns={columns}
        filters={filters}
        rowKey={(row) => `${String(row['key'])}/${String(row['language'])}`}
        reloadKey={reloadKey}
        onRowClick={(row) => setOpenRow(row as unknown as TextRow)}
        rowLabel={(row) => t('admin.texts.rowLabel', { key: String(row['key']), language: String(row['language']) })}
      />
      {openRow ? (
        <TextDialog
          row={openRow}
          maxLength={settings?.max_length}
          onClose={() => setOpenRow(null)}
          onSaved={() => setReloadKey((value) => value + 1)}
        />
      ) : null}
    </>
  );
}

function TextDialog({ row, maxLength, onClose, onSaved }: { row: TextRow; maxLength?: number; onClose: () => void; onSaved: () => void }) {
  const { t } = useAdminText();
  const { api, onError } = useAdmin();
  const { reload } = useTexts();
  const [text, setText] = useState(row.text ?? '');
  const [reason, setReason] = useState('');
  const [failure, setFailure] = useState<EditFailure | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);
    setSaved(false);

    if (reason.trim() === '') {
      setFailure({ fields: { reason: ['required'] }, conflict: false, problems: [], message: 'admin.error.validation' });
      return;
    }

    setBusy(true);

    try {
      await api.changeText(row.key, row.language, text, row.version, reason.trim());
      setSaved(true);
      reload();
      onSaved();
    } catch (error) {
      onError(error);
      setFailure(describeFailure(error));
    } finally {
      setBusy(false);
    }
  };

  const onReload = () => {
    onSaved();
    onClose();
  };

  return (
    <AdminDialog title={t('admin.texts.editTitle', { key: row.key, language: row.language })} onClose={onClose}>
      <form className="admin-form admin-card" onSubmit={(event) => void onSubmit(event)} noValidate>
        <CardField label={t('admin.col.text')} codes={failure?.fields['text']}>
          <textarea className="input" rows={3} maxLength={maxLength} value={text} onChange={(event) => setText(event.target.value)} />
        </CardField>
        <FailureBanner failure={failure} onReload={onReload} />
        {saved ? <Alert kind="success">{t('admin.texts.saved')}</Alert> : null}
        <ReasonAndActions
          reason={reason}
          onReason={setReason}
          codes={failure?.fields['reason']}
          busy={busy}
          canSave={!saved && text.trim() !== ''}
          onCancel={onClose}
        />
      </form>
    </AdminDialog>
  );
}
