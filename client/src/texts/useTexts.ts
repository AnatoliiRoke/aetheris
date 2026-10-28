import { useContext } from 'react';
import { TextsContext, type TextsValue } from './context';

/**
 * The texts of the game data from the server.
 */
export function useTexts(): TextsValue {
  const value = useContext(TextsContext);

  if (!value) {
    throw new Error('useTexts must be used inside TextsProvider.');
  }

  return value;
}
