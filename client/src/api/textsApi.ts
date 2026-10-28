import type { HttpClient } from './http';

/**
 * The texts of the game data in one language (task 00022): the names and descriptions of the
 * reference rows, by their localisation keys.
 */
export interface TextPack {
  language: string;
  version: number;
  texts: Record<string, string>;
}

/**
 * The texts of the game data, kept by the server in the database.
 */
export interface TextsApi {
  pack(language: string): Promise<TextPack>;
}

/**
 * The texts over HTTP. The server answers an unchanged pack with 304 by its `ETag`, so asking
 * again costs little.
 */
export function createTextsApi(http: HttpClient): TextsApi {
  return {
    pack: (language) => http.get<TextPack>(`/api/v1/texts/${encodeURIComponent(language)}`),
  };
}
