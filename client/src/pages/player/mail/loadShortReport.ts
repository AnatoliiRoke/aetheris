/**
 * The report of a load that was short of what the order named (author's rule, 14.11.2026): the
 * parameters the server keeps with the letter of the kind `fleet_load_short`.
 */

/** The kind of the letter of the report of a short load. */
export const loadShortKindCode = 'fleet_load_short';

/** One item that was loaded short. */
export interface LoadShortItem {
  kind: 'resource' | 'part';
  id: number;
  mark_level: number;
  has_race_bonus: boolean;
  name_key: string;
  requested: number;
  loaded: number;
  reason_code: 'stock' | 'room';
}

/** The report of a short load. */
export interface LoadShortReport {
  fleet_id: number;
  fleet_name: string;
  planet_id: number;
  planet_name: string | null;
  items: LoadShortItem[];
}

/**
 * Reads the parameters of a letter of a short load, or nothing when they are not the report.
 *
 * @param value The parameters of the letter as a document.
 */
export function readLoadShortReport(value: string | null): LoadShortReport | null {
  if (!value) return null;
  try {
    const report = JSON.parse(value) as LoadShortReport;
    return Array.isArray(report.items) ? report : null;
  } catch {
    return null;
  }
}
