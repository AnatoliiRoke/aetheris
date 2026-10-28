import { createContext } from 'react';

/**
 * Finds the text of a key of the game data: the current language, then the fallback language;
 * `undefined` when neither has one.
 */
export type EntityText = (key: string | null | undefined) => string | undefined;

/**
 * What the components get from the provider of the texts.
 */
export interface TextsValue {
  /** The text of a key, or `undefined`. */
  text: EntityText;
  /** The text of a key, or the key itself when no language has a text. */
  name: (key: string | null | undefined, fallback?: string) => string;
  /** Asks the server for the texts again, for example after the admin panel changed one. */
  reload: () => void;
}

export const TextsContext = createContext<TextsValue | null>(null);
