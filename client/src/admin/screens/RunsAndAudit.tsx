import type { GenerationRun, Row } from '../adminApi';
import { DataTable, type Column, type Filter } from '../DataTable';
import { formatMoment, useAdminText } from '../i18n/useAdminText';
import { classLabel, statusLabel } from '../labels';

/**
 * The history of the generation runs: who, when, with what, and the result (task 00007,
 * decision 3).
 */
export function RunsScreen() {
  const { t, language } = useAdminText();

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'status', label: 'admin.col.status', render: (row) => statusLabel(t, row['status'] as GenerationRun['status']) },
    { key: 'initiator_id', label: 'admin.col.initiator' },
    { key: 'created_at', label: 'admin.col.createdAt', render: (row) => formatMoment(row['created_at'] as string, language) },
    { key: 'finished_at', label: 'admin.col.finishedAt', render: (row) => formatMoment(row['finished_at'] as string | null, language) },
    { key: 'sector_count', label: 'admin.generation.sectorCount' },
    { key: 'seed', label: 'admin.col.seed' },
    { key: 'rule_set', label: 'admin.generation.ruleSet' },
    {
      key: 'summary',
      label: 'admin.generation.summary',
      render: (row) => {
        const summary = (row as unknown as GenerationRun).summary;
        const parts = [
          t('admin.run.summarySectors', { count: summary.sector_ids.length }),
          t('admin.run.summarySystems', { count: summary.system_count }),
          ...Object.entries(summary.object_counts_by_class).map(([objectClass, count]) => `${classLabel(t, objectClass)}: ${count}`),
        ];
        return parts.join('; ');
      },
    },
    { key: 'error', label: 'admin.col.error' },
  ];

  const filters: Filter[] = [
    {
      name: 'status',
      label: 'admin.col.status',
      options: (['queued', 'running', 'succeeded', 'failed'] as const).map((status) => ({ value: status, label: statusLabel(t, status) })),
    },
  ];

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.runs.title')}</h1>
        <p className="hint">{t('admin.runs.lead')}</p>
      </header>
      <DataTable table="runs" caption="admin.runs.title" columns={columns} filters={filters} />
    </>
  );
}

/**
 * The journal of the administrators, read only (task 00007, decision 1).
 */
export function AuditScreen() {
  const { t, language } = useAdminText();

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id' },
    { key: 'at', label: 'admin.col.at', render: (row) => formatMoment(row['at'] as string, language) },
    { key: 'admin_name', label: 'admin.col.admin' },
    { key: 'action', label: 'admin.col.action' },
    { key: 'target', label: 'admin.col.target' },
    { key: 'old_value', label: 'admin.col.oldValue' },
    { key: 'new_value', label: 'admin.col.newValue' },
    { key: 'reason', label: 'admin.col.reason' },
  ];

  const filters: Filter[] = [
    {
      name: 'changes_only',
      label: 'admin.audit.changesOnly',
      options: [{ value: 'true', label: t('admin.table.yes') }],
    },
  ];

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.audit.title')}</h1>
        <p className="hint">{t('admin.audit.lead')}</p>
      </header>
      <DataTable table="audit" caption="admin.audit.title" columns={columns} filters={filters} initialFilters={{ changes_only: 'true' }} />
    </>
  );
}
