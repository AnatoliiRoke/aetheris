import { useEffect, useState } from 'react';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import type { Row, WorldReference } from '../adminApi';
import { useAdmin } from '../adminContext';
import { adminRoutes } from '../adminRoutes';
import { DataTable, type Column, type Filter } from '../DataTable';
import type { AdminMessageKey } from '../i18n/uk';
import { classLabel } from '../labels';
import { useAdminText } from '../i18n/useAdminText';

interface Share {
  resource_id: number;
  resource_code: string;
  coefficient: number;
}

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
 * The reference tables of the world, for the filters of the type.
 */
function useReference(): WorldReference | null {
  const { api, onError } = useAdmin();
  const [reference, setReference] = useState<WorldReference | null>(null);

  useEffect(() => {
    let active = true;

    api.reference().then(
      (answer) => {
        if (active) {
          setReference(answer);
        }
      },
      (failure: unknown) => onError(failure),
    );

    return () => {
      active = false;
    };
  }, [api, onError]);

  return reference;
}

/**
 * The resources of a body or a field: a deposit shows its coefficient, a field its share in
 * per cent.
 */
function Shares({ shares, percent }: { shares: Share[]; percent: boolean }) {
  if (shares.length === 0) {
    return <>—</>;
  }

  return (
    <ul className="admin-shares">
      {shares.map((share) => (
        <li key={share.resource_id}>
          {share.resource_code} {percent ? `${(Number(share.coefficient) * 100).toFixed(1)}%` : `×${Number(share.coefficient).toFixed(3)}`}
        </li>
      ))}
    </ul>
  );
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
  const { t } = useAdminText();
  const yesNo = useYesNo();
  const initialFilters = useQueryFilters(['sector_id']);

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'name', label: 'admin.col.name', sort: 'name' },
    { key: 'sector_name', label: 'admin.col.sector' },
    { key: 'number', label: 'admin.col.number', sort: 'number' },
    {
      key: 'position',
      label: 'admin.col.position',
      render: (row) => `${Number(row['x']).toFixed(1)}, ${Number(row['y']).toFixed(1)}, ${Number(row['z']).toFixed(1)}`,
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
      <DataTable key={JSON.stringify(initialFilters)} table="systems" caption="admin.systems.title" columns={columns} filters={filters} initialFilters={initialFilters} />
    </>
  );
}

/**
 * The bodies: the planets and the gas giants with their deposits.
 */
export function ObjectsScreen() {
  const { t } = useAdminText();
  const yesNo = useYesNo();
  const reference = useReference();
  const initialFilters = useQueryFilters(['sector_id', 'system_id']);

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'name', label: 'admin.col.name', sort: 'name' },
    { key: 'object_class', label: 'admin.col.class', render: (row) => classLabel(t, String(row['object_class'])) },
    { key: 'object_type_code', label: 'admin.col.type' },
    { key: 'system_name', label: 'admin.col.system' },
    { key: 'orbit', label: 'admin.col.orbit', sort: 'orbit' },
    { key: 'size_class_code', label: 'admin.col.sizeClass' },
    { key: 'size', label: 'admin.col.size', sort: 'size' },
    { key: 'owner_player_id', label: 'admin.col.owner' },
    { key: 'deposit_ids', label: 'admin.col.deposits', render: (row) => <Shares shares={row['deposit_ids'] as Share[]} percent={false} /> },
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
        .map((type) => ({ value: String(type.id), label: type.code })),
    },
    { name: 'owned', label: 'admin.col.owned', options: yesNo },
  ];

  return (
    <>
      <ScreenTitle title="admin.objects.title" lead="admin.objects.lead" />
      <DataTable key={JSON.stringify(initialFilters)} table="objects" caption="admin.objects.title" columns={columns} filters={filters} initialFilters={initialFilters} />
    </>
  );
}

/**
 * The asteroid fields with their composition.
 */
export function FieldsScreen() {
  const { t, language } = useAdminText();
  const yesNo = useYesNo();
  const initialFilters = useQueryFilters(['sector_id', 'system_id']);

  const columns: Column<Row>[] = [
    { key: 'id', label: 'admin.col.id', sort: 'id' },
    { key: 'name', label: 'admin.col.name', sort: 'name' },
    { key: 'object_type_code', label: 'admin.col.type' },
    { key: 'sector_id', label: 'admin.col.sectorId' },
    { key: 'system_name', label: 'admin.col.system', render: (row) => (row['system_name'] ? String(row['system_name']) : t('admin.fields.free')) },
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
      <DataTable key={JSON.stringify(initialFilters)} table="fields" caption="admin.fields.title" columns={columns} filters={filters} initialFilters={initialFilters} />
    </>
  );
}
