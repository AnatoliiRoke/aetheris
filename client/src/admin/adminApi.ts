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

/**
 * A reference row of the world. The code is neutral and only shown as a hint; the name and the
 * description are texts of the database by their keys (task 00022).
 */
export interface ReferenceRow {
  id: number;
  code: string;
  name_key: string;
  description_key?: string;
  /** The class of an object type. */
  group_code: string | null;
  /** The resource group of a resource. */
  group_id?: number | null;
  image_key?: string | null;
}

/** The reference tables of the world, for the filters. */
export interface WorldReference {
  resource_groups?: ReferenceRow[];
  resources: ReferenceRow[];
  object_types: ReferenceRow[];
  size_classes: ReferenceRow[];
}

/** A text of the screen «Тексти»: a key in one language, or the gap where its text is missing. */
export interface TextRow {
  key: string;
  language: string;
  text: string | null;
  version: number;
  updated_by: string | null;
  updated_at: string | null;
}

/** The languages of the texts and the largest length of one text. */
export interface TextSettings {
  languages: string[];
  max_length: number;
}

/** A text in every language. */
export type TextsByLanguage = Record<string, string>;

/** A new resource: the server gives the code (task 00022, decision 6). */
export interface ResourceInput {
  resource_group_id: number;
  name: TextsByLanguage;
  description: TextsByLanguage;
  reason: string;
}

/** A row of any admin table. */
export type Row = Record<string, unknown>;

/** A resource with its coefficient: a share of a field or the richness multiplier of a deposit. */
export interface Share {
  resource_id: number;
  resource_code: string;
  resource_name_key: string;
  coefficient: number;
}

/** A resource with its coefficient as the admin panel sends it. */
export interface ShareInput {
  resource_id: number;
  coefficient: number;
}

/** The card of an asteroid field (task 00009). */
export interface FieldCard {
  id: number;
  version: number;
  celestial_object_id: number;
  name: string;
  object_type_id: number;
  object_type_code: string;
  object_type_name_key: string;
  sector_id: number;
  system_id: number | null;
  system_name: string | null;
  x: number;
  y: number;
  z: number;
  initial_mass: number;
  mass: number;
  depleted_at: string | null;
  composition_ids: Share[];
  bounds: { max_composition_share: number; sector_size: number; sector_depth: number };
}

/** The new state of a field. */
export interface FieldUpdate {
  version: number;
  reason: string;
  name: string;
  object_type_id: number;
  x: number;
  y: number;
  z: number;
  mass: number;
  initial_mass: number;
  depleted: boolean;
  composition_ids: ShareInput[];
}

/** A size class with its range. */
export interface SizeClass {
  id: number;
  code: string;
  name_key: string;
  size_min: number;
  size_max: number;
}

/** The card of a body: a planet or a gas giant. */
export interface BodyCard {
  id: number;
  version: number;
  celestial_object_id: number;
  name: string;
  owner_name: string | null;
  owner_player_id: string | null;
  object_type_id: number;
  object_type_code: string;
  object_type_name_key: string;
  object_class: string;
  sector_id: number;
  system_id: number;
  system_name: string;
  orbit: number | null;
  x: number;
  y: number;
  z: number;
  size_class_id: number;
  size_class_code: string;
  size_class_name_key: string;
  size: number;
  deposit_ids: Share[];
  bounds: {
    orbit_min: number;
    orbit_max: number;
    taken_orbit_ids: { orbit: number; name: string }[];
    deposit_coefficient_min: number;
    deposit_coefficient_max: number;
    allowed_resource_ids: number[];
    size_class_ids: SizeClass[];
  };
}

/** The new state of a body; the owner is not part of it. */
export interface BodyUpdate {
  version: number;
  reason: string;
  name: string;
  object_type_id: number;
  size_class_id: number;
  size: number;
  orbit: number;
  deposit_ids: ShareInput[];
}

/** The card of a star system. */
export interface SystemCard {
  id: number;
  version: number;
  sector_id: number;
  sector_name: string;
  number: number;
  name: string;
  x: number;
  y: number;
  z: number;
  in_nebula: boolean;
  star_type_id: number | null;
  star_type_code: string | null;
  star_type_name_key: string | null;
  bounds: { sector_size: number; sector_depth: number };
}

/** The new state of a star system. */
export interface SystemUpdate {
  version: number;
  reason: string;
  name: string;
  x: number;
  y: number;
  z: number;
  star_type_id: number;
}

/** A row of the object types. */
export interface ObjectTypeRow {
  id: number;
  version: number;
  code: string;
  name_key: string;
  object_class: string;
  generation_weight: number;
  mass_min: number | null;
  mass_max: number | null;
  is_colonizable: boolean;
  object_count: number;
}

/** The new values of an object type. */
export interface ObjectTypeUpdate {
  version: number;
  reason: string;
  generation_weight: number;
  mass_min: number | null;
  mass_max: number | null;
  is_colonizable: boolean;
}

/** A row of the composition rules: one rule with its group. */
export interface RuleRow {
  id: number;
  version: number;
  rule_group_id: number;
  rule_group_version: number;
  rule_group_code: string;
  object_type_id: number;
  object_type_code: string;
  object_type_name_key: string;
  object_class: string;
  min_picks: number;
  max_picks: number;
  extra_pick_probability: number;
  picks_all: boolean;
  resource_id: number;
  resource_code: string;
  resource_name_key: string;
  weight: number;
  coefficient_min: number;
  coefficient_max: number;
  rich_coefficient_min: number | null;
  rich_coefficient_max: number | null;
  is_active: boolean;
}

/** The values of a rule, new or changed. */
export interface RuleInput {
  version: number;
  reason: string;
  rule_group_id: number;
  resource_id: number;
  weight: number;
  coefficient_min: number;
  coefficient_max: number;
  rich_coefficient_min: number | null;
  rich_coefficient_max: number | null;
  is_active: boolean;
}

/** The new values of a rule group. */
export interface RuleGroupUpdate {
  version: number;
  reason: string;
  min_picks: number;
  max_picks: number;
  extra_pick_probability: number;
  picks_all: boolean;
}

/** The tables of the admin panel and their routes. */
export const tableRoutes = {
  sectors: '/world/sectors',
  systems: '/world/systems',
  objects: '/world/objects',
  fields: '/world/fields',
  objectTypes: '/world/object-types',
  rules: '/world/generation-rules',
  runs: '/world/generation-runs',
  config: '/config',
  audit: '/audit',
  texts: '/texts',
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
  fieldCard(fieldId: number): Promise<FieldCard>;
  updateField(fieldId: number, update: FieldUpdate): Promise<FieldCard>;
  bodyCard(planetId: number): Promise<BodyCard>;
  updateBody(planetId: number, update: BodyUpdate): Promise<BodyCard>;
  systemCard(systemId: number): Promise<SystemCard>;
  updateSystem(systemId: number, update: SystemUpdate): Promise<SystemCard>;
  updateObjectType(objectTypeId: number, update: ObjectTypeUpdate): Promise<void>;
  updateRule(ruleId: number, update: RuleInput): Promise<void>;
  addRule(rule: RuleInput): Promise<void>;
  updateRuleGroup(ruleGroupId: number, update: RuleGroupUpdate): Promise<void>;
  textSettings(): Promise<TextSettings>;
  changeText(key: string, language: string, text: string, version: number, reason: string): Promise<void>;
  createResource(input: ResourceInput): Promise<void>;
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
    fieldCard(fieldId) {
      return http.get<FieldCard>(`${adminPrefix}/world/fields/${fieldId}`);
    },
    updateField(fieldId, update) {
      return http.put<FieldCard>(`${adminPrefix}/world/fields/${fieldId}`, update);
    },
    bodyCard(planetId) {
      return http.get<BodyCard>(`${adminPrefix}/world/objects/${planetId}`);
    },
    updateBody(planetId, update) {
      return http.put<BodyCard>(`${adminPrefix}/world/objects/${planetId}`, update);
    },
    systemCard(systemId) {
      return http.get<SystemCard>(`${adminPrefix}/world/systems/${systemId}`);
    },
    updateSystem(systemId, update) {
      return http.put<SystemCard>(`${adminPrefix}/world/systems/${systemId}`, update);
    },
    async updateObjectType(objectTypeId, update) {
      await http.put(`${adminPrefix}/world/object-types/${objectTypeId}`, update);
    },
    async updateRule(ruleId, update) {
      await http.put(`${adminPrefix}/world/generation-rules/${ruleId}`, update);
    },
    async addRule(rule) {
      await http.post(`${adminPrefix}/world/generation-rules`, rule);
    },
    async updateRuleGroup(ruleGroupId, update) {
      await http.put(`${adminPrefix}/world/generation-rule-groups/${ruleGroupId}`, update);
    },
    async textSettings() {
      const answer = await http.get<TextSettings>(`${adminPrefix}/texts?page=1&page_size=1`);
      return { languages: answer.languages, max_length: answer.max_length };
    },
    async changeText(key, language, text, version, reason) {
      await http.put(`${adminPrefix}/texts`, { key, language, text, version, reason });
    },
    async createResource(input) {
      await http.post(`${adminPrefix}/world/resources`, input);
    },
  };
}
