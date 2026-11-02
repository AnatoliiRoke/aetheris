import { ApiError } from '../../api/http';
import type { UnmetRequirement } from '../../api/planetApi';
import type { Language } from '../../i18n/context';
import type { EntityText } from '../../texts/context';
import { reasonText, requirementText } from './planetText';

/**
 * A message above the content of a screen: what a command did or why the server refused it.
 */
export type Notice = { kind: 'error' | 'success'; text: string } | null;

/**
 * The prefixes of the error codes of the server that have their own text for the player.
 */
const reasonPrefixes = ['research_', 'planets_'];

/**
 * The message for the player from the error of a command: a refusal of the server has its own
 * text, any other failure gets the general one.
 *
 * @param error The error of the command.
 * @param language The language of the player.
 * @param generic The general text for a failure the player cannot act on.
 * @param prefixes The prefixes of the codes that have their own text; the research ones by default.
 */
export function noticeFromError(error: unknown, language: Language, generic: string, prefixes: string[] = reasonPrefixes): Notice {
  const code = error instanceof ApiError ? error.code : 'internal_error';

  return { kind: 'error', text: prefixes.some((prefix) => code.startsWith(prefix)) ? reasonText(language, code) : generic };
}

/**
 * The refusal of a research. A missing technology or building is named, the same way the
 * building screen names one; every other refusal keeps its own text.
 *
 * @param error The error of the command.
 * @param language The language of the player.
 * @param entities The names of the technologies and the buildings.
 * @param generic The general text for a failure the player cannot act on.
 */
export function researchRefusal(error: unknown, language: Language, entities: EntityText, generic: string): Notice {
  if (error instanceof ApiError && error.code === 'research_requirement_unmet') {
    const lines = unmetRequirements(error.details.unmet_requirements ?? error.details.unmetRequirements).map((requirement) =>
      requirementText(language, requirement, entities),
    );

    if (lines.length > 0) {
      return { kind: 'error', text: lines.join('. ') };
    }
  }

  return noticeFromError(error, language, generic);
}

function unmetRequirements(value: unknown): UnmetRequirement[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.flatMap((entry) => {
    if (entry === null || typeof entry !== 'object') {
      return [];
    }

    const fields = entry as Record<string, unknown>;
    const reasonCode = textField(fields, 'reasonCode', 'reason_code');

    if (!reasonCode) {
      return [];
    }

    const targetNameKey = textField(fields, 'targetNameKey', 'target_name_key');

    return [
      {
        requirementId: numberField(fields, 'requirementId', 'requirement_id'),
        requirementType: textField(fields, 'requirementType', 'requirement_type') ?? '',
        targetId: numberField(fields, 'targetId', 'target_id'),
        targetLevel: numberField(fields, 'targetLevel', 'target_level'),
        reasonCode,
        targetNameKey,
        isClosed: Boolean(fields.isClosed ?? fields.is_closed),
      },
    ];
  });
}

function textField(fields: Record<string, unknown>, camelName: string, snakeName: string): string | null {
  const fieldText = fields[camelName] ?? fields[snakeName];

  return typeof fieldText === 'string' && fieldText !== '' ? fieldText : null;
}

function numberField(fields: Record<string, unknown>, camelName: string, snakeName: string): number {
  const fieldNumber = fields[camelName] ?? fields[snakeName];

  return typeof fieldNumber === 'number' ? fieldNumber : 0;
}
