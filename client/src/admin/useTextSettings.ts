import { useEffect, useState } from 'react';
import type { TextSettings } from './adminApi';
import { useAdmin } from './adminContext';

/**
 * The languages of the texts of the game data and the largest length of one text, for the
 * screens that edit texts (task 00022).
 */
export function useTextSettings(): TextSettings | null {
  const { api, onError } = useAdmin();
  const [settings, setSettings] = useState<TextSettings | null>(null);

  useEffect(() => {
    let active = true;

    api.textSettings().then(
      (answer) => {
        if (active) {
          setSettings(answer);
        }
      },
      (failure: unknown) => onError(failure),
    );

    return () => {
      active = false;
    };
  }, [api, onError]);

  return settings;
}
