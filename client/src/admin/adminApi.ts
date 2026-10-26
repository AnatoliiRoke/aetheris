import type { HttpClient } from '../api/http';

/**
 * The admin endpoints (`/api/admin`, task 00007). The answers keep the snake_case names of the
 * server: the admin tables show them as they are.
 */
export const adminPrefix = '/api/admin';

/** One page of an admin table. */
export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  page_size: number;
}

/** The query of an admin table: paging, search, order and the filters of the table. */
export interface TableQuery {
  page: number;
  page_size?: number;
  search?: string;
  sort?: string;
  direction?: 'asc' | 'desc';
  filters?: Record<string, string | undefined>;
}

/** The signed-in administrator. */
export interface AdminMe {
  admin_id: string;
  display_name: string;
  role_codes: string[];
  permission_codes: string[];
  idle_minutes: number;
  second_factor: string;
  require_totp: boolean;
}

/** A configuration parameter. */
export interface ConfigParameter {
  key: string;
  value: string;
  type: 'integer' | 'decimal' | 'boolean' | 'text';
  minimum: string | null;
  maximum: string | null;
  default_value: string | null;
  description: string;
  read_only: boolean;
  version: number;
  updated_by: string | null;
  updated_at: string;
}

/** One change of a configuration parameter. */
export interface ConfigChange {
  id: number;
  key: string;
  old_value: string;
  new_value: string;
  version: number;
  reason: string;
  rolled_back_change_id: number | null;
  admin_name: string;
  changed_at: string;
}

/** A record of the journal of the administrators. */
export interface AuditRecord {
  id: number;
  admin_id: string;
  admin_name: string;
  action: string;
  target: string | null;
  old_value: string | null;
  new_value: string | null;
  reason: string | null;
  at: string;
}

/** The time multiplier, shown read-only. */
export interface ClockInfo {
  multiplier: number;
  max_multiplier: number;
  read_only: boolean;
}

/** A rule set of the generation. */
export interface RuleSet {
  code: string;
  rule_group_count: number;
  rule_count: number;
}

/** What a generation run would create. */
export interface GenerationEstimate {
  sector_count: number;
  estimated_system_count: number;
  systems_min: number;
  systems_max: number;
  max_sectors_per_run: number;
  max_systems_per_run: number;
  allowed: boolean;
  active_run_id: number | null;
  status_poll_seconds: number;
  rule_sets: RuleSet[];
}

/** A generation run. */
export interface GenerationRun {
  id: number;
  initiator_id: string;
  sector_count: number;
  seed: number | null;
  rule_set: string;
  estimated_system_count: number;
  status: 'queued' | 'running' | 'succeeded' | 'failed';
  created_at: string;
  started_at: string | null;
  finished_at: string | null;
  summary: {
    sector_ids: number[];
    sectors: { sector_id: number; seed: number }[];
    system_count: number;
    object_counts_by_class: Record<string, number>;
  };
  error: string | null;
}

/** A reference row of the world. */
export interface ReferenceRow {
  id: number;
  code: string;
  name_key: string;
  group_code: string | null;
}

/** The reference tables of the world, for the filters. */
export interface WorldReference {
  resources: ReferenceRow[];
  object_types: ReferenceRow[];
  size_classes: ReferenceRow[];
}

/** A row of any admin table. */
export type Row = Record<string, unknown>;

/** The tables of the admin panel and their routes. */
export const tableRoutes = {
  sectors: '/world/sectors',
  systems: '/world/systems',
  objects: '/world/objects',
  fields: '/world/fields',
  runs: '/world/generation-runs',
  config: '/config',
  audit: '/audit',
} as const;

export type TableName = keyof typeof tableRoutes;

/** The admin endpoints the panel uses. */
export interface AdminApi {
  login(email: string, password: string, code: string): Promise<void>;
  logout(): Promise<void>;
  me(): Promise<AdminMe>;
  table<T extends Row>(table: TableName, query: TableQuery): Promise<Page<T>>;
  configHistory(key: string): Promise<Page<ConfigChange>>;
  changeConfig(key: string, value: string, reason: string): Promise<void>;
  rollbackConfig(key: string, changeId: number, reason: string): Promise<void>;
  clock(): Promise<ClockInfo>;
  reference(): Promise<WorldReference>;
  estimate(sectorCount: number): Promise<GenerationEstimate>;
  startGeneration(sectorCount: number, seed: number | null, ruleSet: string): Promise<GenerationRun>;
  run(runId: number): Promise<GenerationRun>;
}

/**
 * Builds the query string of a table: only the values that are set.
 */
export function tableSearch(query: TableQuery): string {
  const params = new URLSearchParams();
  params.set('page', String(query.page));

  if (query.page_size) {
    params.set('page_size', String(query.page_size));
  }

  if (query.search) {
    params.set('search', query.search);
  }

  if (query.sort) {
    params.set('sort', query.sort);
    params.set('direction', query.direction ?? 'asc');
  }

  for (const [name, value] of Object.entries(query.filters ?? {})) {
    if (value !== undefined && value !== '') {
      params.set(name, value);
    }
  }

  return params.toString();
}

/**
 * The admin endpoints over HTTP.
 */
export function createAdminApi(http: HttpClient): AdminApi {
  return {
    async login(email, password, code) {
      await http.post(`${adminPrefix}/auth/login`, { email, password, code: code || null });
      // The anti-forgery token belongs to the session, which has just changed.
      http.resetCsrf();
    },
    async logout() {
      await http.post(`${adminPrefix}/auth/logout`);
      http.resetCsrf();
    },
    me() {
      return http.get<AdminMe>(`${adminPrefix}/auth/me`);
    },
    table<T extends Row>(table: TableName, query: TableQuery) {
      return http.get<Page<T>>(`${adminPrefix}${tableRoutes[table]}?${tableSearch(query)}`);
    },
    configHistory(key) {
      return http.get<Page<ConfigChange>>(`${adminPrefix}/config/${encodeURIComponent(key)}/history`);
    },
    async changeConfig(key, value, reason) {
      await http.put(`${adminPrefix}/config/${encodeURIComponent(key)}`, { value, reason });
    },
    async rollbackConfig(key, changeId, reason) {
      await http.post(`${adminPrefix}/config/${encodeURIComponent(key)}/rollback`, { change_id: changeId, reason });
    },
    clock() {
      return http.get<ClockInfo>(`${adminPrefix}/clock`);
    },
    reference() {
      return http.get<WorldReference>(`${adminPrefix}/world/reference`);
    },
    estimate(sectorCount) {
      return http.get<GenerationEstimate>(`${adminPrefix}/world/generation/estimate?sector_count=${sectorCount}`);
    },
    startGeneration(sectorCount, seed, ruleSet) {
      return http.post<GenerationRun>(`${adminPrefix}/world/generation-runs`, { sector_count: sectorCount, seed, rule_set: ruleSet });
    },
    run(runId) {
      return http.get<GenerationRun>(`${adminPrefix}/world/generation-runs/${runId}`);
    },
  };
}
