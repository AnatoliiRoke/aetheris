import { Fragment, useEffect, useState, type FormEvent, type KeyboardEvent, type MouseEvent, type ReactNode } from 'react';
import { Alert } from '../components/Alert';
import type { Page, Row, TableName, TableQuery } from './adminApi';
import { errorKey, useAdmin } from './adminContext';
import type { AdminMessageKey } from './i18n/uk';
import { useAdminText } from './i18n/useAdminText';

/** A column of an admin table. */
export interface Column<T extends Row> {
  key: string;
  label: AdminMessageKey;
  /** The sort column of the server, when the column can be sorted. */
  sort?: string;
  /** An explanation of the column, shown when the pointer rests on the header. */
  hint?: AdminMessageKey;
  render?: (row: T) => ReactNode;
}

/** A filter of an admin table: a choice or a number. */
export interface Filter {
  name: string;
  label: AdminMessageKey;
  options?: { value: string; label: string }[];
}

interface DataTableProps<T extends Row> {
  table: TableName;
  caption: AdminMessageKey;
  columns: Column<T>[];
  filters?: Filter[];
  /** The filters set from outside, for example the sector of a link. */
  initialFilters?: Record<string, string>;
  /** Shows the row under its line, for example a history. */
  expanded?: (row: T, reload: () => void) => ReactNode;
  /** A key of the row; `id` by default. */
  rowKey?: (row: T) => string;
  /** Changes when the table must load again. */
  reloadKey?: number;
  /** Opens the card of a row: a click on the row or Enter on it (task 00009, decision 2). */
  onRowClick?: (row: T) => void;
  /** The text of the button that opens the card, for the keyboard and the screen readers. */
  rowLabel?: (row: T) => string;
}

/**
 * An admin table: the search, the filters, the order and the paging all run on the server
 * (task 00007, decision 2).
 */
export function DataTable<T extends Row>({
  table,
  caption,
  columns,
  filters = [],
  initialFilters = {},
  expanded,
  rowKey = (row) => String(row['id']),
  reloadKey = 0,
  onRowClick,
  rowLabel,
}: DataTableProps<T>) {
  const { t } = useAdminText();
  const { api, onError } = useAdmin();
  const [query, setQuery] = useState<TableQuery>({ page: 1, filters: initialFilters });
  const [searchText, setSearchText] = useState('');
  const [result, setResult] = useState<{ key: string; page: Page<T> | null; error: AdminMessageKey | null }>({ key: '', page: null, error: null });
  const [openKey, setOpenKey] = useState<string | null>(null);
  const [version, setVersion] = useState(0);
  const requestKey = JSON.stringify([table, query, reloadKey, version]);
  const loading = result.key !== requestKey;
  const { page, error } = result;

  useEffect(() => {
    let active = true;

    api.table<T>(table, query).then(
      (answer) => {
        if (active) {
          setResult({ key: requestKey, page: answer, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setResult((current) => ({ key: requestKey, page: current.page, error: errorKey(failure) }));
        }
      },
    );

    return () => {
      active = false;
    };
  }, [api, table, query, onError, requestKey]);

  const reload = () => setVersion((value) => value + 1);
  const pages = page ? Math.max(1, Math.ceil(page.total / page.page_size)) : 1;

  const onSearch = (event: FormEvent) => {
    event.preventDefault();
    setQuery((current) => ({ ...current, page: 1, search: searchText.trim() || undefined }));
  };

  const onSort = (sort: string) => {
    setQuery((current) => ({
      ...current,
      page: 1,
      sort,
      direction: current.sort === sort && current.direction !== 'desc' ? 'desc' : 'asc',
    }));
  };

  const onFilter = (name: string, value: string) => {
    setQuery((current) => ({ ...current, page: 1, filters: { ...current.filters, [name]: value || undefined } }));
  };

  return (
    <section className="admin-table" aria-busy={loading}>
      <form className="admin-table__tools" onSubmit={onSearch} role="search">
        <label className="admin-field">
          <span>{t('admin.table.search')}</span>
          <input
            className="input"
            type="search"
            value={searchText}
            placeholder={t('admin.table.searchHint')}
            onChange={(event) => setSearchText(event.target.value)}
          />
        </label>
        {filters.map((filter) => (
          <label className="admin-field" key={filter.name}>
            <span>{t(filter.label)}</span>
            {filter.options ? (
              <select className="input" value={query.filters?.[filter.name] ?? ''} onChange={(event) => onFilter(filter.name, event.target.value)}>
                <option value="">{t('admin.table.all')}</option>
                {filter.options.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            ) : (
              <input
                className="input"
                type="number"
                min={1}
                value={query.filters?.[filter.name] ?? ''}
                onChange={(event) => onFilter(filter.name, event.target.value)}
              />
            )}
          </label>
        ))}
        <button className="btn btn--secondary" type="submit">
          {t('admin.table.find')}
        </button>
      </form>

      {error ? <Alert kind="error">{t(error)}</Alert> : null}

      <div className="admin-table__scroll">
        <table>
          <caption className="visually-hidden">{t(caption)}</caption>
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  scope="col"
                  title={column.hint ? t(column.hint) : undefined}
                  aria-sort={column.sort && query.sort === column.sort ? (query.direction === 'desc' ? 'descending' : 'ascending') : undefined}
                >
                  {column.sort ? (
                    <button className="admin-table__sort" type="button" onClick={() => onSort(column.sort!)}>
                      {t(column.label)}
                      <span aria-hidden="true">{query.sort === column.sort ? (query.direction === 'desc' ? ' ▼' : ' ▲') : ' ↕'}</span>
                    </button>
                  ) : (
                    t(column.label)
                  )}
                </th>
              ))}
              {expanded ? <th scope="col">{t('admin.table.details')}</th> : null}
            </tr>
          </thead>
          <tbody>
            {page && page.items.length === 0 ? (
              <tr>
                <td colSpan={columns.length + (expanded ? 1 : 0)}>{t('admin.table.empty')}</td>
              </tr>
            ) : null}
            {page?.items.map((row) => {
              const key = rowKey(row);
              const isOpen = openKey === key;

              return (
                <Fragment key={key}>
                  <tr
                    className={onRowClick ? 'admin-table__row--open' : undefined}
                    tabIndex={onRowClick ? 0 : undefined}
                    aria-label={onRowClick && rowLabel ? rowLabel(row) : undefined}
                    onClick={onRowClick ? (event) => openFromClick(event, () => onRowClick(row)) : undefined}
                    onKeyDown={onRowClick ? (event) => openFromKey(event, () => onRowClick(row)) : undefined}
                  >
                    {columns.map((column) => (
                      <td key={column.key}>{column.render ? column.render(row) : displayValue(row[column.key])}</td>
                    ))}
                    {expanded ? (
                      <td>
                        <button className="text-btn" type="button" aria-expanded={isOpen} onClick={() => setOpenKey(isOpen ? null : key)}>
                          {t(isOpen ? 'admin.table.hide' : 'admin.table.show')}
                        </button>
                      </td>
                    ) : null}
                  </tr>
                  {expanded && isOpen ? (
                    <tr className="admin-table__expanded">
                      <td colSpan={columns.length + 1}>{expanded(row, reload)}</td>
                    </tr>
                  ) : null}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>

      <nav className="admin-table__pager" aria-label={t('admin.table.pager')}>
        <span>{page ? t('admin.table.total', { total: page.total }) : t('admin.loading')}</span>
        <button
          className="btn btn--secondary"
          type="button"
          disabled={query.page <= 1 || loading}
          onClick={() => setQuery((current) => ({ ...current, page: current.page - 1 }))}
        >
          {t('admin.table.previous')}
        </button>
        <span>{t('admin.table.page', { page: query.page, pages })}</span>
        <button
          className="btn btn--secondary"
          type="button"
          disabled={query.page >= pages || loading}
          onClick={() => setQuery((current) => ({ ...current, page: current.page + 1 }))}
        >
          {t('admin.table.next')}
        </button>
      </nav>
    </section>
  );
}

/**
 * Opens a card from a click on a row, but not from a click on a link or a button inside it.
 */
function openFromClick(event: MouseEvent<HTMLTableRowElement>, open: () => void) {
  if (event.target instanceof Element && event.target.closest('a, button, input, select')) {
    return;
  }

  open();
}

function openFromKey(event: KeyboardEvent<HTMLTableRowElement>, open: () => void) {
  if (event.key === 'Enter' && event.target === event.currentTarget) {
    event.preventDefault();
    open();
  }
}

/**
 * Shows a value of a cell: an empty value as a dash, a switch as yes or no.
 */
function displayValue(value: unknown): ReactNode {
  if (value === null || value === undefined || value === '') {
    return '—';
  }

  if (typeof value === 'boolean') {
    return value ? '✓' : '—';
  }

  return String(value);
}
