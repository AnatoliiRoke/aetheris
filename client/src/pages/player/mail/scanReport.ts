/** The immutable report parameters stored with a scan letter. */
export interface ScanReportObject {
  id: number;
  system_id: number | null;
  sector_id: number;
  name: string;
  object_class: string;
  planet_id: number | null;
}

export interface ScanReport {
  snapshot_id: number;
  sector_id: number;
  sector_name?: string;
  x: number;
  y: number;
  z: number;
  radius: number;
  scanned_at: string;
  systems: { id: number; sector_id: number; name: string; has_deposits: boolean; has_gas_giants: boolean; objects: ScanReportObject[] }[];
  free_fields: ScanReportObject[];
}

export function readScanReport(value: string | null): ScanReport | null {
  if (!value) return null;
  try {
    const report = JSON.parse(value) as ScanReport;
    return Array.isArray(report.systems) && Array.isArray(report.free_fields) ? report : null;
  } catch {
    return null;
  }
}
