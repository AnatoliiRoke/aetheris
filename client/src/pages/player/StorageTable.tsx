import { Fragment, type ReactNode } from 'react';

/**
 * A column of a table of the storage: the heading and whether the numbers are on the right.
 */
export interface StorageColumn {
  id: string;
  header: string;
  numeric?: boolean;
}

/**
 * A row of a table of the storage: the key and the cells in the order of the columns, or, for a
 * heading of a group, the text that spans the whole row. A row may carry a detail that shows
 * under it and spans the whole table.
 */
export type StorageRow =
  | { key: string | number; cells: ReactNode[]; details?: ReactNode }
  | { key: string | number; heading: string };

/**
 * A table of the storage screen. On a phone the table scrolls inside its own container, so the
 * page never scrolls sideways.
 *
 * @param label The name of the table for a screen reader.
 * @param columns The columns.
 * @param rows The rows.
 */
export function StorageTable({ label, columns, rows }: { label: string; columns: StorageColumn[]; rows: StorageRow[] }) {
  return (
    <div className="planet-table-wrap">
      <table aria-label={label} className="planet-table">
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.id} scope="col">
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) =>
            'heading' in row ? (
              <tr className="planet-table__section" key={row.key}>
                <th colSpan={columns.length} scope="colgroup">
                  {row.heading}
                </th>
              </tr>
            ) : (
              <Fragment key={row.key}>
                <tr>
                  {row.cells.map((cell, index) => (
                    <td className={columns[index]?.numeric ? 'num' : undefined} key={columns[index]?.id ?? index}>
                      {cell}
                    </td>
                  ))}
                </tr>
                {row.details ? (
                  <tr className="planet-table__details">
                    <td colSpan={columns.length}>{row.details}</td>
                  </tr>
                ) : null}
              </Fragment>
            ),
          )}
        </tbody>
      </table>
    </div>
  );
}
