import { ApiError } from '../api/http';
import { errorKey, fieldErrors } from './adminContext';
import type { AdminMessageKey } from './i18n/uk';

/**
 * Why a change of a card was refused: the codes of the fields, a conflict of versions, the
 * problems of the generation rules or another error (task 00009, decisions 4 and 6).
 */
export interface EditFailure {
  fields: Record<string, string[]>;
  conflict: boolean;
  problems: string[];
  message: AdminMessageKey | null;
}

/**
 * Reads a refused change.
 */
export function describeFailure(failure: unknown): EditFailure {
  const empty: EditFailure = { fields: {}, conflict: false, problems: [], message: null };

  if (failure instanceof ApiError && failure.status === 409) {
    return { ...empty, conflict: true };
  }

  if (failure instanceof ApiError && failure.code === 'world_config_invalid') {
    const problems = failure.details['problems'];

    return { ...empty, problems: Array.isArray(problems) ? problems.map(String) : [], message: 'admin.error.configInvalid' };
  }

  const fields = fieldErrors(failure);

  return Object.keys(fields).length > 0 ? { ...empty, fields, message: 'admin.error.validation' } : { ...empty, message: errorKey(failure) };
}

/**
 * Parses a number typed in a card; a comma is a decimal separator too.
 */
export function parseNumber(text: string): number | null {
  const normalized = text.trim().replace(',', '.');

  if (normalized === '') {
    return null;
  }

  const value = Number(normalized);

  return Number.isFinite(value) ? value : null;
}
