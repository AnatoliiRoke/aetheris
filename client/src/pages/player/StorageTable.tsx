import type { ReactNode } from 'react';

/**
 * A column of a table of the storage: the heading and whether the numbers are on the right.
 */
export interface StorageColumn {
  id: string;
  header: string;
  numeric?: boolean;
}

/**
 * A row of a table of the storage: the key and the cells in the order of the columns.
 */
export interface StorageRow {
  key: string | number;
  cells: ReactNode[];
}

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
          {rows.map((row) => (
            <tr key={row.key}>
              {row.cells.map((cell, index) => (
                <td className={columns[index]?.numeric ? 'num' : undefined} key={columns[index]?.id ?? index}>
                  {cell}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
