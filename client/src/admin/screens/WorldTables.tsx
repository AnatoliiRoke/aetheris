import { useState } from 'react';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import type { Row, Share } from '../adminApi';
import { adminRoutes } from '../adminRoutes';
import { DataTable, type Column, type Filter } from '../DataTable';
import { WorldName } from '../EditParts';
import type { AdminMessageKey } from '../i18n/uk';
import { formatNumber, useAdminText, useWorldName } from '../i18n/useAdminText';
import { classLabel } from '../labels';
import { useReference } from '../useReference';
import { BodyCardDialog, FieldCardDialog, SystemCardDialog } from './ObjectCards';

/**
 * The filters of a table read from the address, for the links between the tables.
 */
function useQueryFilters(names: string[]): Record<string, string> {
  const { query } = useRouter();

  return Object.fromEntries(names.flatMap((name) => (query.get(name) ? [[name, query.get(name)!]] : [])));
}

function useYesNo(): { value: string; label: string }[] {
  const { t } = useAdminText();

  return [
    { value: 'true', label: t('admin.table.yes') },
    { value: 'false', label: t('admin.table.no') },
  ];
}

/**
 * The resources of a body or a field by their names, the code in the tooltip (task 00009,
 * decisions 9 and 10): a field shows its shares in per cent and the empty rock, a body the
 * richness multiplier of every deposit.
 */
function Shares({ shares, percent }: { shares: Share[]; percent: boolean }) {
  const { t, language } = useAdminText();
  const name = useWorldName();

  if (shares.length === 0) {
    return <>—</>;
  }

  const total = shares.reduce((sum, share) => sum + Number(share.coefficient), 0);

  return (
    <ul className="admin-shares">
      {shares.map((share) => (
        <li key={share.resource_id} title={share.resource_code}>
          {name(share.resource_name_key, share.resource_code)}{' '}
          {percent ? `${formatNumber(Number(share.coefficient) * 100, language, 1)}%` : `×${formatNumber(Number(share.coefficient), language, 3)}`}
        </li>
      ))}
      {percent ? <li className="hint">{t('admin.card.rock', { percent: formatNumber(Math.max(0, 100 - total * 100), language, 1) })}</li> : null}
    </ul>
  );
}

/** The x, y and z columns of a table (task 00009, decision 1). */
function coordinateColumns(language: 'uk' | 'en'): Column<Row>[] {
  return (['x', 'y', 'z'] as const).map((axis) => ({
    key: axis,
    label: `admin.col.${axis}` as AdminMessageKey,
    render: (row: Row) => formatNumber(Number(row[axis]), language, 2),
  }));
}

/**
 * Opens the card of a row and reloads the table after a change.
 */
function useCardOf() {
  const [openId, setOpenId] = useState<number | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  return {
    openId,
    open: (row: Row) => setOpenId(Number(row['id'])),
    close: () => setOpenId(null),
    reloadKey,
    changed: () => setReloadKey((value) => value + 1),
  };
}

function ScreenTitle({ title, lead }: { title: AdminMessageKey; lead: AdminMessageKey }) {
  const { t } = useAdminText();

  return (
    <header className="admin-screen__head">
      <h1 className="admin-screen__title">{t(title)}</h1>
      <p className="hint">{t(lead)}</p>
    </header>
  );
}

/**
 * The sectors (task 00007, decision 2).
 */
export function SectorsScreen() {
  const { t, language } = useAdminText();
  const yesNo = useYesNo();

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'name', label: 'admin.col.name', sort: 'name' },
    { key: 'seed', label: 'admin.col.seed' },
    { key: 'layout_index', label: 'admin.col.layoutIndex' },
    { key: 'is_newbie', label: 'admin.col.newbie' },
    { key: 'system_count', label: 'admin.col.systems', sort: 'system_count' },
    { key: 'body_count', label: 'admin.col.bodies' },
    { key: 'owned_count', label: 'admin.col.owned' },
    { key: 'field_count', label: 'admin.col.fields' },
    {
      key: 'created_at',
      label: 'admin.col.createdAt',
      sort: 'created_at',
      render: (row) => new Date(String(row['created_at'])).toLocaleString(language === 'uk' ? 'uk-UA' : 'en-GB'),
    },
    {
      key: 'links',
      label: 'admin.col.open',
      render: (row) => (
        <span className="admin-links">
          <Link to={`${adminRoutes.systems}?sector_id=${String(row['id'])}`}>{t('admin.nav.systems')}</Link>
          <Link to={`${adminRoutes.objects}?sector_id=${String(row['id'])}`}>{t('admin.nav.objects')}</Link>
          <Link to={`${adminRoutes.fields}?sector_id=${String(row['id'])}`}>{t('admin.nav.fields')}</Link>
        </span>
      ),
    },
  ];

  const filters: Filter[] = [{ name: 'is_newbie', label: 'admin.col.newbie', options: yesNo }];

  return (
    <>
      <ScreenTitle title="admin.sectors.title" lead="admin.sectors.lead" />
      <DataTable table="sectors" caption="admin.sectors.title" columns={columns} filters={filters} />
    </>
  );
}

/**
 * The star systems.
 */
export function SystemsScreen() {
  const { t, language } = useAdminText();
  const yesNo = useYesNo();
  const reference = useReference();
  const card = useCardOf();
  const initialFilters = useQueryFilters(['sector_id']);

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'name', label: 'admin.col.name', sort: 'name' },
    { key: 'sector_name', label: 'admin.col.sector' },
    { key: 'number', label: 'admin.col.number', sort: 'number' },
    ...coordinateColumns(language),
    {
      key: 'star_type_name_key',
      label: 'admin.col.starType',
      render: (row) => <WorldName nameKey={row['star_type_name_key'] as string | null} code={row['star_type_code'] as string | null} />,
    },
    { key: 'in_nebula', label: 'admin.col.inNebula' },
    { key: 'newbie_count', label: 'admin.col.newbies' },
    { key: 'body_count', label: 'admin.col.bodies', sort: 'body_count' },
    { key: 'field_count', label: 'admin.col.fields' },
    {
      key: 'links',
      label: 'admin.col.open',
      render: (row) => (
        <span className="admin-links">
          <Link to={`${adminRoutes.objects}?system_id=${String(row['id'])}`}>{t('admin.nav.objects')}</Link>
          <Link to={`${adminRoutes.fields}?system_id=${String(row['id'])}`}>{t('admin.nav.fields')}</Link>
        </span>
      ),
    },
  ];

  const filters: Filter[] = [
    { name: 'sector_id', label: 'admin.col.sectorId' },
    { name: 'in_nebula', label: 'admin.col.inNebula', options: yesNo },
  ];

  return (
    <>
      <ScreenTitle title="admin.systems.title" lead="admin.systems.lead" />
      <DataTable
        key={JSON.stringify(initialFilters)}
        table="systems"
        caption="admin.systems.title"
        columns={columns}
        filters={filters}
        initialFilters={initialFilters}
        reloadKey={card.reloadKey}
        onRowClick={card.open}
        rowLabel={(row) => t('admin.card.open', { name: String(row['name']) })}
      />
      {card.openId !== null ? <SystemCardDialog systemId={card.openId} reference={reference} onClose={card.close} onSaved={card.changed} /> : null}
    </>
  );
}

/**
 * The bodies: the planets and the gas giants with their deposits.
 */
export function ObjectsScreen() {
  const { t, language } = useAdminText();
  const name = useWorldName();
  const yesNo = useYesNo();
  const reference = useReference();
  const card = useCardOf();
  const initialFilters = useQueryFilters(['sector_id', 'system_id']);

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'name', label: 'admin.col.name', sort: 'name' },
    { key: 'object_class', label: 'admin.col.class', render: (row) => classLabel(t, String(row['object_class'])) },
    {
      key: 'object_type_code',
      label: 'admin.col.type',
      render: (row) => <WorldName nameKey={row['object_type_name_key'] as string} code={row['object_type_code'] as string} />,
    },
    { key: 'system_name', label: 'admin.col.system' },
    { key: 'orbit', label: 'admin.col.orbit', sort: 'orbit' },
    ...coordinateColumns(language),
    {
      key: 'size_class_code',
      label: 'admin.col.sizeClass',
      render: (row) => <WorldName nameKey={row['size_class_name_key'] as string} code={row['size_class_code'] as string} />,
    },
    { key: 'size', label: 'admin.col.size', sort: 'size' },
    { key: 'owner_player_id', label: 'admin.col.owner' },
    {
      key: 'deposit_ids',
      label: 'admin.col.depositMultiplier',
      hint: 'admin.card.multiplierHint',
      render: (row) => <Shares shares={row['deposit_ids'] as Share[]} percent={false} />,
    },
  ];

  const filters: Filter[] = [
    { name: 'sector_id', label: 'admin.col.sectorId' },
    { name: 'system_id', label: 'admin.col.systemId' },
    {
      name: 'object_class',
      label: 'admin.col.class',
      options: ['planet', 'gas_giant'].map((value) => ({ value, label: classLabel(t, value) })),
    },
    {
      name: 'object_type_id',
      label: 'admin.col.type',
      options: (reference?.object_types ?? [])
        .filter((type) => type.group_code === 'planet' || type.group_code === 'gas_giant')
        .map((type) => ({ value: String(type.id), label: name(type.name_key, type.code) })),
    },
    { name: 'owned', label: 'admin.col.owned', options: yesNo },
  ];

  return (
    <>
      <ScreenTitle title="admin.objects.title" lead="admin.objects.lead" />
      <DataTable
        key={JSON.stringify(initialFilters)}
        table="objects"
        caption="admin.objects.title"
        columns={columns}
        filters={filters}
        initialFilters={initialFilters}
        reloadKey={card.reloadKey}
        onRowClick={card.open}
        rowLabel={(row) => t('admin.card.open', { name: String(row['name']) })}
      />
      {card.openId !== null ? <BodyCardDialog planetId={card.openId} reference={reference} onClose={card.close} onSaved={card.changed} /> : null}
    </>
  );
}

/**
 * The asteroid fields with their composition.
 */
export function FieldsScreen() {
  const { t, language } = useAdminText();
  const yesNo = useYesNo();
  const reference = useReference();
  const card = useCardOf();
  const initialFilters = useQueryFilters(['sector_id', 'system_id']);

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'name', label: 'admin.col.name', sort: 'name' },
    {
      key: 'object_type_code',
      label: 'admin.col.type',
      render: (row) => <WorldName nameKey={row['object_type_name_key'] as string} code={row['object_type_code'] as string} />,
    },
    { key: 'sector_id', label: 'admin.col.sectorId' },
    { key: 'system_name', label: 'admin.col.system', render: (row) => (row['system_name'] ? String(row['system_name']) : t('admin.fields.free')) },
    ...coordinateColumns(language),
    { key: 'initial_mass', label: 'admin.col.initialMass' },
    { key: 'mass', label: 'admin.col.mass', sort: 'mass' },
    {
      key: 'depleted_at',
      label: 'admin.col.depletedAt',
      render: (row) => (row['depleted_at'] ? new Date(String(row['depleted_at'])).toLocaleString(language === 'uk' ? 'uk-UA' : 'en-GB') : '—'),
    },
    { key: 'composition_ids', label: 'admin.col.composition', render: (row) => <Shares shares={row['composition_ids'] as Share[]} percent /> },
  ];

  const filters: Filter[] = [
    { name: 'sector_id', label: 'admin.col.sectorId' },
    { name: 'system_id', label: 'admin.col.systemId' },
    { name: 'free', label: 'admin.fields.freeFilter', options: yesNo },
    { name: 'depleted', label: 'admin.fields.depletedFilter', options: yesNo },
  ];

  return (
    <>
      <ScreenTitle title="admin.fields.title" lead="admin.fields.lead" />
      <DataTable
        key={JSON.stringify(initialFilters)}
        table="fields"
        caption="admin.fields.title"
        columns={columns}
        filters={filters}
        initialFilters={initialFilters}
        reloadKey={card.reloadKey}
        onRowClick={card.open}
        rowLabel={(row) => t('admin.card.open', { name: String(row['name']) })}
      />
      {card.openId !== null ? <FieldCardDialog fieldId={card.openId} reference={reference} onClose={card.close} onSaved={card.changed} /> : null}
    </>
  );
}
