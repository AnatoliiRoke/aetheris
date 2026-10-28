import type { HttpClient } from '../api/http';
import { adminPrefix, type Page } from './adminApi';

/**
 * The reference kinds of the admin panel (task 00023): the server describes every kind with its
 * fields, and the client draws one list and one form for all of them. The answers keep the
 * snake_case names of the server.
 */

/** An option of a select field. */
export interface ReferenceOption {
  value: string;
  label_key: string;
  /** The kind a dependent reference field picks from when this option is chosen. */
  reference_kind?: string;
}

/** A field of a kind: its type says how the form draws it. */
export interface ReferenceField {
  name: string;
  type: 'text' | 'number' | 'select' | 'reference' | 'image' | 'flag' | 'levels' | 'costs' | 'effects' | 'requirements';
  label_key: string;
  is_required: boolean;
  has_languages: boolean;
  in_list: boolean;
  minimum?: number;
  maximum?: number;
  is_decimal: boolean;
  options?: ReferenceOption[];
  reference_kind?: string;
  reference_kind_from?: string;
  /** The columns of a list field. */
  fields?: ReferenceField[];
}

/** The action codes of the endpoints of a kind. */
export interface ReferencePermissions {
  list_read: string;
  record_read: string;
  added: string;
  updated: string;
  archived: string;
  restored: string;
}

/** A reference kind. */
export interface ReferenceKind {
  kind: string;
  code_group: string;
  name_key_prefix: string;
  title_key: string;
  sort_order: number;
  route: string;
  permissions: ReferencePermissions;
  fields: ReferenceField[];
}

/** The kinds the administrator may read, the languages of the texts and the limits of the pictures. */
export interface ReferenceKinds {
  kinds: ReferenceKind[];
  languages: string[];
  max_length: number;
  image_types: string[];
  image_max_bytes: number;
}

/** A value of a field: a text by language, a number, a code, a flag or the rows of a list. */
export type ReferenceValue = string | number | boolean | null | undefined | Record<string, string> | ReferenceValues[];

/** The values of a record or of a row of a list field, by the field name. */
export type ReferenceValues = Record<string, ReferenceValue>;

/** A row of the list of a kind. */
export interface ReferenceRow {
  id: number;
  code: string;
  name_key: string;
  image_key?: string | null;
  is_archived: boolean;
  version: number;
  values: ReferenceValues;
}

/** A record of the form. */
export interface ReferenceRecord {
  id: number;
  code: string;
  name_key: string;
  description_key: string;
  image_key?: string | null;
  is_archived: boolean;
  version: number;
  values: ReferenceValues;
}

/** The read of a list: the page, the search text and the archive filter. */
export interface ReferenceQuery {
  page: number;
  search?: string;
  /** Only the archived rows, only the active ones, or every row. */
  state: 'active' | 'archived' | 'all';
}

/** What a stored change answers. */
export interface ReferenceSaved {
  id: number;
  version: number;
}

/** The reference endpoints the admin panel uses. */
export interface ReferenceApi {
  kinds(): Promise<ReferenceKinds>;
  list(kind: ReferenceKind, query: ReferenceQuery): Promise<Page<ReferenceRow>>;
  read(kind: ReferenceKind, id: number): Promise<ReferenceRecord>;
  create(kind: ReferenceKind, values: ReferenceValues, reason: string): Promise<ReferenceSaved>;
  update(kind: ReferenceKind, id: number, version: number, values: ReferenceValues, reason: string): Promise<ReferenceSaved>;
  setArchived(kind: ReferenceKind, id: number, version: number, archived: boolean, reason: string): Promise<ReferenceSaved>;
  uploadImage(file: Blob): Promise<{ image_key: string }>;
  /** The address of a picture of a row; it is public, the players see the pictures. */
  imageUrl(imageKey: string): string;
}

/**
 * The reference endpoints over HTTP.
 */
export function createReferenceApi(http: HttpClient): ReferenceApi {
  return {
    kinds() {
      return http.get<ReferenceKinds>(`${adminPrefix}/reference/kinds`);
    },
    list(kind, query) {
      const params = new URLSearchParams({ page: String(query.page) });

      if (query.search) {
        params.set('search', query.search);
      }

      if (query.state === 'all') {
        params.set('all', 'true');
      } else {
        params.set('archived', String(query.state === 'archived'));
      }

      return http.get<Page<ReferenceRow>>(`${adminPrefix}${kind.route}?${params.toString()}`);
    },
    read(kind, id) {
      return http.get<ReferenceRecord>(`${adminPrefix}${kind.route}/${id}`);
    },
    create(kind, values, reason) {
      return http.post<ReferenceSaved>(`${adminPrefix}${kind.route}`, { reason, values });
    },
    update(kind, id, version, values, reason) {
      return http.put<ReferenceSaved>(`${adminPrefix}${kind.route}/${id}`, { version, reason, values });
    },
    setArchived(kind, id, version, archived, reason) {
      return http.put<ReferenceSaved>(`${adminPrefix}${kind.route}/${id}/${archived ? 'archive' : 'restore'}`, { version, reason });
    },
    uploadImage(file) {
      return http.upload<{ image_key: string }>(`${adminPrefix}/reference/images`, file);
    },
    imageUrl(imageKey) {
      return http.url(`/api/v1/images/${encodeURIComponent(imageKey)}`);
    },
  };
}
