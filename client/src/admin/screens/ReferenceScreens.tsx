import { useState, type FormEvent } from 'react';
import { Alert } from '../../components/Alert';
import type { ObjectTypeRow, Row, RuleRow, WorldReference } from '../adminApi';
import { useAdmin } from '../adminContext';
import { AdminDialog } from '../AdminDialog';
import { DataTable, type Column, type Filter } from '../DataTable';
import { CardField, FailureBanner, ReasonAndActions, WorldName } from '../EditParts';
import { describeFailure, parseNumber, type EditFailure } from '../editFailure';
import { formatNumber, useAdminText, useWorldName } from '../i18n/useAdminText';
import { classLabel } from '../labels';
import { useReference } from '../useReference';

/** The classes of the object types, for the filter. */
const objectClasses = ['star', 'planet', 'gas_giant', 'asteroid_field', 'nebula'];

/**
 * The reason, the busy state and the refusal of a change of a reference row.
 */
function useChange(onDone: () => void) {
  const { onError } = useAdmin();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<EditFailure | null>(null);

  const run = async (action: () => Promise<void>, invalid: Record<string, string[]> = {}) => {
    const fields = { ...invalid };

    if (reason.trim() === '') {
      fields['reason'] = ['required'];
    }

    if (Object.keys(fields).length > 0) {
      setFailure({ fields, conflict: false, problems: [], message: 'admin.error.validation' });
      return;
    }

    setBusy(true);
    setFailure(null);

    try {
      await action();
      setReason('');
      onDone();
    } catch (error) {
      onError(error);
      setFailure(describeFailure(error));
    } finally {
      setBusy(false);
    }
  };

  return { reason, setReason, busy, failure, run };
}

function range(language: 'uk' | 'en', minimum: number | null, maximum: number | null, digits: number): string {
  return minimum === null || maximum === null ? '—' : `${formatNumber(Number(minimum), language, digits)} – ${formatNumber(Number(maximum), language, digits)}`;
}

/**
 * The object types of the generation (task 00009, decision 7): the weight, the mass range of a
 * field type and whether a player may own it. A type is switched off by weight zero; it is
 * never deleted, so the objects of the type stay valid.
 */
export function ObjectTypesScreen() {
  const { t, language } = useAdminText();
  const [openRow, setOpenRow] = useState<ObjectTypeRow | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'code', label: 'admin.col.name', sort: 'name', render: (row) => <WorldName nameKey={row['name_key'] as string} code={row['code'] as string} /> },
    { key: 'object_class', label: 'admin.col.class', sort: 'object_class', render: (row) => classLabel(t, String(row['object_class'])) },
    { key: 'generation_weight', label: 'admin.col.weight', sort: 'weight' },
    {
      key: 'mass',
      label: 'admin.col.massRange',
      render: (row) => range(language, row['mass_min'] as number | null, row['mass_max'] as number | null, 0),
    },
    { key: 'is_colonizable', label: 'admin.col.colonizable' },
    { key: 'object_count', label: 'admin.col.objectCount' },
  ];

  const filters: Filter[] = [
    { name: 'object_class', label: 'admin.col.class', options: objectClasses.map((value) => ({ value, label: classLabel(t, value) })) },
  ];

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.objectTypes.title')}</h1>
        <p className="hint">{t('admin.objectTypes.lead')}</p>
      </header>
      <DataTable
        table="objectTypes"
        caption="admin.objectTypes.title"
        columns={columns}
        filters={filters}
        reloadKey={reloadKey}
        onRowClick={(row) => setOpenRow(row as unknown as ObjectTypeRow)}
        rowLabel={(row) => t('admin.card.open', { name: String(row['code']) })}
      />
      {openRow ? (
        <ObjectTypeDialog
          row={openRow}
          onClose={() => setOpenRow(null)}
          onSaved={() => {
            setOpenRow(null);
            setReloadKey((value) => value + 1);
          }}
        />
      ) : null}
    </>
  );
}

function ObjectTypeDialog({ row, onClose, onSaved }: { row: ObjectTypeRow; onClose: () => void; onSaved: () => void }) {
  const { t } = useAdminText();
  const { api, can } = useAdmin();
  const name = useWorldName();
  const { reason, setReason, busy, failure, run } = useChange(onSaved);
  const isField = row.object_class === 'asteroid_field';
  const [form, setForm] = useState({
    weight: String(row.generation_weight),
    massMin: row.mass_min === null ? '' : String(row.mass_min),
    massMax: row.mass_max === null ? '' : String(row.mass_max),
    colonizable: row.is_colonizable,
  });
  const errors = failure?.fields ?? {};

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();

    const weight = parseNumber(form.weight);
    const massMin = isField ? parseNumber(form.massMin) : null;
    const massMax = isField ? parseNumber(form.massMax) : null;
    const invalid: Record<string, string[]> = {};

    if (weight === null) {
      invalid['generation_weight'] = ['not_a_number'];
    }

    void run(
      () =>
        api.updateObjectType(row.id, {
          version: row.version,
          reason: reason.trim(),
          generation_weight: weight!,
          mass_min: massMin,
          mass_max: massMax,
          is_colonizable: form.colonizable,
        }),
      invalid,
    );
  };

  return (
    <AdminDialog title={t('admin.card.typeTitle', { name: name(row.name_key, row.code) })} onClose={onClose}>
      <form className="admin-form admin-card" onSubmit={onSubmit} noValidate>
        <p className="hint">
          {classLabel(t, row.object_class)} · <code>{row.code}</code> · {t('admin.card.newObjectsOnly')}
        </p>
        <CardField label={t('admin.col.weight')} codes={errors['generation_weight']} hint={t('admin.card.weightHint')}>
          <input className="input" inputMode="numeric" value={form.weight} onChange={(event) => setForm({ ...form, weight: event.target.value })} />
        </CardField>
        {isField ? (
          <div className="admin-form admin-form--inline">
            <CardField label={t('admin.col.massMin')} codes={errors['mass_min']}>
              <input className="input" inputMode="numeric" value={form.massMin} onChange={(event) => setForm({ ...form, massMin: event.target.value })} />
            </CardField>
            <CardField label={t('admin.col.massMax')} codes={errors['mass_max']}>
              <input className="input" inputMode="numeric" value={form.massMax} onChange={(event) => setForm({ ...form, massMax: event.target.value })} />
            </CardField>
          </div>
        ) : null}
        {row.object_class === 'planet' ? (
          <label className="admin-check">
            <input type="checkbox" checked={form.colonizable} onChange={(event) => setForm({ ...form, colonizable: event.target.checked })} />
            <span>{t('admin.col.colonizable')}</span>
          </label>
        ) : null}
        <FailureBanner failure={failure} onReload={onSaved} />
        <ReasonAndActions reason={reason} onReason={setReason} codes={errors['reason']} busy={busy} canSave={can('admin.world_object_type_updated')} onCancel={onClose} />
      </form>
    </AdminDialog>
  );
}

/**
 * The composition rules of the generation (task 00009, decision 7): a row is one resource of
 * a rule group with its weight and the ranges of its coefficient. A rule is changed, added to
 * its group or switched off; the group sets how many resources it picks.
 */
export function RulesScreen() {
  const { t, language } = useAdminText();
  const name = useWorldName();
  const reference = useReference();
  const [openRow, setOpenRow] = useState<RuleRow | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    {
      key: 'object_type_code',
      label: 'admin.col.type',
      render: (row) => <WorldName nameKey={row['object_type_name_key'] as string} code={row['object_type_code'] as string} />,
    },
    {
      key: 'rule_group_code',
      label: 'admin.col.ruleGroup',
      render: (row) => <WorldName nameKey={groupKeyOfResource(reference, Number(row['resource_id']))} code={row['rule_group_code'] as string} />,
    },
    {
      key: 'picks',
      label: 'admin.col.picks',
      render: (row) =>
        row['picks_all']
          ? t('admin.rules.picksAll')
          : t('admin.rules.picks', {
              min: Number(row['min_picks']),
              max: Number(row['max_picks']),
              probability: formatNumber(Number(row['extra_pick_probability']), language, 2),
            }),
    },
    { key: 'resource_code', label: 'admin.card.resource', render: (row) => <WorldName nameKey={row['resource_name_key'] as string} code={row['resource_code'] as string} /> },
    { key: 'weight', label: 'admin.col.weight', sort: 'weight' },
    {
      key: 'coefficient',
      label: 'admin.col.coefficientRange',
      render: (row) => range(language, row['coefficient_min'] as number, row['coefficient_max'] as number, 3),
    },
    {
      key: 'rich',
      label: 'admin.col.richRange',
      hint: 'admin.card.richHint',
      render: (row) => range(language, row['rich_coefficient_min'] as number | null, row['rich_coefficient_max'] as number | null, 3),
    },
    { key: 'is_active', label: 'admin.col.active' },
  ];

  const filters: Filter[] = [
    {
      name: 'object_type_id',
      label: 'admin.col.type',
      options: (reference?.object_types ?? []).map((type) => ({ value: String(type.id), label: name(type.name_key, type.code) })),
    },
    {
      name: 'is_active',
      label: 'admin.col.active',
      options: [
        { value: 'true', label: t('admin.table.yes') },
        { value: 'false', label: t('admin.table.no') },
      ],
    },
  ];

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.rules.title')}</h1>
        <p className="hint">{t('admin.rules.lead')}</p>
      </header>
      <DataTable
        table="rules"
        caption="admin.rules.title"
        columns={columns}
        filters={filters}
        reloadKey={reloadKey}
        onRowClick={(row) => setOpenRow(row as unknown as RuleRow)}
        rowLabel={(row) => t('admin.card.open', { name: `${String(row['object_type_code'])} · ${String(row['resource_code'])}` })}
      />
      {openRow ? (
        <RuleDialog
          row={openRow}
          onClose={() => setOpenRow(null)}
          onSaved={() => {
            setOpenRow(null);
            setReloadKey((value) => value + 1);
          }}
        />
      ) : null}
    </>
  );
}

function RuleDialog({ row, onClose, onSaved }: { row: RuleRow; onClose: () => void; onSaved: () => void }) {
  const { t } = useAdminText();
  const name = useWorldName();
  const reference = useReference();
  const [mode, setMode] = useState<'rule' | 'add' | 'group'>('rule');

  return (
    <AdminDialog
      title={t('admin.card.ruleTitle', {
        type: name(row.object_type_name_key, row.object_type_code),
        group: name(groupKeyOfResource(reference, row.resource_id), row.rule_group_code),
      })}
      onClose={onClose}
    >
      <div className="admin-tabs" role="tablist" aria-label={t('admin.rules.title')}>
        {(['rule', 'add', 'group'] as const).map((tab) => (
          <button
            key={tab}
            className={tab === mode ? 'admin__link admin__link--active' : 'admin__link'}
            type="button"
            role="tab"
            aria-selected={tab === mode}
            onClick={() => setMode(tab)}
          >
            {t(tab === 'rule' ? 'admin.rules.tabRule' : tab === 'add' ? 'admin.rules.tabAdd' : 'admin.rules.tabGroup')}
          </button>
        ))}
      </div>
      {mode === 'group' ? <RuleGroupForm row={row} onCancel={onClose} onSaved={onSaved} /> : <RuleForm key={mode} row={row} adding={mode === 'add'} onCancel={onClose} onSaved={onSaved} />}
    </AdminDialog>
  );
}

function RuleForm({ row, adding, onCancel, onSaved }: { row: RuleRow; adding: boolean; onCancel: () => void; onSaved: () => void }) {
  const { t } = useAdminText();
  const { api, can } = useAdmin();
  const name = useWorldName();
  const reference = useReference();
  const { reason, setReason, busy, failure, run } = useChange(onSaved);
  const isPlanet = row.object_class === 'planet';
  const text = (value: number | null) => (value === null ? '' : String(value));
  const [form, setForm] = useState({
    resourceId: row.resource_id,
    weight: String(row.weight),
    coefficientMin: String(row.coefficient_min),
    coefficientMax: String(row.coefficient_max),
    richMin: text(row.rich_coefficient_min),
    richMax: text(row.rich_coefficient_max),
    active: adding ? true : row.is_active,
  });
  const errors = failure?.fields ?? {};

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();

    const numbers = {
      weight: parseNumber(form.weight),
      coefficient_min: parseNumber(form.coefficientMin),
      coefficient_max: parseNumber(form.coefficientMax),
    };
    const invalid: Record<string, string[]> = {};

    for (const [field, value] of Object.entries(numbers)) {
      if (value === null) {
        invalid[field] = ['not_a_number'];
      }
    }

    const rule = {
      version: row.version,
      reason: reason.trim(),
      rule_group_id: row.rule_group_id,
      resource_id: form.resourceId,
      weight: numbers.weight!,
      coefficient_min: numbers.coefficient_min!,
      coefficient_max: numbers.coefficient_max!,
      rich_coefficient_min: isPlanet ? parseNumber(form.richMin) : null,
      rich_coefficient_max: isPlanet ? parseNumber(form.richMax) : null,
      is_active: form.active,
    };

    void run(() => (adding ? api.addRule(rule) : api.updateRule(row.id, rule)), invalid);
  };

  return (
    <form className="admin-form admin-card" onSubmit={onSubmit} noValidate>
      <p className="hint">{t(adding ? 'admin.rules.addLead' : 'admin.card.newObjectsOnly')}</p>
      <CardField label={t('admin.card.resource')} codes={errors['resource_id']}>
        <select className="input" value={form.resourceId} onChange={(event) => setForm({ ...form, resourceId: Number(event.target.value) })}>
          {(reference?.resources ?? [{ id: row.resource_id, code: row.resource_code, name_key: row.resource_name_key, group_code: null }]).map((resource) => (
            <option key={resource.id} value={resource.id} title={resource.code}>
              {name(resource.name_key, resource.code)}
            </option>
          ))}
        </select>
      </CardField>
      <CardField label={t('admin.col.weight')} codes={errors['weight']} hint={t('admin.card.weightHint')}>
        <input className="input" inputMode="numeric" value={form.weight} onChange={(event) => setForm({ ...form, weight: event.target.value })} />
      </CardField>
      <div className="admin-form admin-form--inline">
        <CardField label={t('admin.col.coefficientMin')} codes={errors['coefficient_min']}>
          <input className="input" inputMode="decimal" value={form.coefficientMin} onChange={(event) => setForm({ ...form, coefficientMin: event.target.value })} />
        </CardField>
        <CardField label={t('admin.col.coefficientMax')} codes={errors['coefficient_max']}>
          <input className="input" inputMode="decimal" value={form.coefficientMax} onChange={(event) => setForm({ ...form, coefficientMax: event.target.value })} />
        </CardField>
      </div>
      {isPlanet ? (
        <div className="admin-form admin-form--inline">
          <CardField label={t('admin.col.richMin')} codes={errors['rich_coefficient_min']}>
            <input className="input" inputMode="decimal" value={form.richMin} onChange={(event) => setForm({ ...form, richMin: event.target.value })} />
          </CardField>
          <CardField label={t('admin.col.richMax')} codes={errors['rich_coefficient_max']}>
            <input className="input" inputMode="decimal" value={form.richMax} onChange={(event) => setForm({ ...form, richMax: event.target.value })} />
          </CardField>
        </div>
      ) : null}
      {isPlanet ? <p className="hint">{t('admin.card.richHint')}</p> : null}
      <label className="admin-check">
        <input type="checkbox" checked={form.active} onChange={(event) => setForm({ ...form, active: event.target.checked })} />
        <span>{t('admin.col.active')}</span>
      </label>
      <FailureBanner failure={failure} onReload={onSaved} />
      <ReasonAndActions
        reason={reason}
        onReason={setReason}
        codes={errors['reason']}
        busy={busy}
        canSave={can(adding ? 'admin.world_generation_rule_added' : 'admin.world_generation_rule_updated')}
        onCancel={onCancel}
      />
    </form>
  );
}

function RuleGroupForm({ row, onCancel, onSaved }: { row: RuleRow; onCancel: () => void; onSaved: () => void }) {
  const { t } = useAdminText();
  const { api, can } = useAdmin();
  const { reason, setReason, busy, failure, run } = useChange(onSaved);
  const [form, setForm] = useState({
    minPicks: String(row.min_picks),
    maxPicks: String(row.max_picks),
    probability: String(row.extra_pick_probability),
    picksAll: row.picks_all,
  });
  const errors = failure?.fields ?? {};

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();

    const numbers = { min_picks: parseNumber(form.minPicks), max_picks: parseNumber(form.maxPicks), extra_pick_probability: parseNumber(form.probability) };
    const invalid: Record<string, string[]> = {};

    for (const [field, value] of Object.entries(numbers)) {
      if (value === null) {
        invalid[field] = ['not_a_number'];
      }
    }

    void run(
      () =>
        api.updateRuleGroup(row.rule_group_id, {
          version: row.rule_group_version,
          reason: reason.trim(),
          min_picks: numbers.min_picks!,
          max_picks: numbers.max_picks!,
          extra_pick_probability: numbers.extra_pick_probability!,
          picks_all: form.picksAll,
        }),
      invalid,
    );
  };

  return (
    <form className="admin-form admin-card" onSubmit={onSubmit} noValidate>
      <Alert kind="info">{t('admin.rules.groupLead')}</Alert>
      <div className="admin-form admin-form--inline">
        <CardField label={t('admin.col.minPicks')} codes={errors['min_picks']}>
          <input className="input" inputMode="numeric" value={form.minPicks} onChange={(event) => setForm({ ...form, minPicks: event.target.value })} />
        </CardField>
        <CardField label={t('admin.col.maxPicks')} codes={errors['max_picks']}>
          <input className="input" inputMode="numeric" value={form.maxPicks} onChange={(event) => setForm({ ...form, maxPicks: event.target.value })} />
        </CardField>
        <CardField label={t('admin.col.extraPick')} codes={errors['extra_pick_probability']}>
          <input className="input" inputMode="decimal" value={form.probability} onChange={(event) => setForm({ ...form, probability: event.target.value })} />
        </CardField>
      </div>
      <label className="admin-check">
        <input type="checkbox" checked={form.picksAll} onChange={(event) => setForm({ ...form, picksAll: event.target.checked })} />
        <span>{t('admin.col.picksAll')}</span>
      </label>
      <p className="hint">{t('admin.rules.picksAllHint')}</p>
      <FailureBanner failure={failure} onReload={onSaved} />
      <ReasonAndActions
        reason={reason}
        onReason={setReason}
        codes={errors['reason']}
        busy={busy}
        canSave={can('admin.world_generation_rule_group_updated')}
        onCancel={onCancel}
      />
    </form>
  );
}

/**
 * The name key of the resource group of a resource: a rule group picks the resources of one
 * group, so it is named by that group (task 00022); its own code is neutral.
 */
function groupKeyOfResource(reference: WorldReference | null, resourceId: number): string | undefined {
  const groupId = reference?.resources.find((resource) => resource.id === resourceId)?.group_id;

  return reference?.resource_groups?.find((group) => group.id === groupId)?.name_key;
}
